using System;
using System.Collections.Generic;
using System.Threading;
using AeLa.Utilities.Pool;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Pool;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace AeLa.Utilities.SceneDeps
{
	public static class SceneDependencies
	{
		private static Dictionary<string, SceneInstance> pathToInstance = new();
		private static HashSet<string> loadedDependencies = new();
		private static HashSet<string> currentDependencies = new();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatic()
		{
			pathToInstance = new();
			loadedDependencies = new();
			currentDependencies = new();
		}

		/// <summary>
		/// Additively loads all the dependency scenes in the provided <see cref="GroupedDependencyChain"/>
		/// in parallel, then activates them once all of their dependencies are loaded and activated.
		/// </summary>
		/// <param name="groups">The <see cref="GroupedDependencyChain"/> to load.</param>
		/// <param name="unloadUnusedDependencies">Whether to unload already loaded dependency scenes that are no longer in use after loading the current groups. If false, you will need to call <see cref="UnloadUnusedDependenciesAsync"/> manually.</param>
		public static async UniTask LoadDependenciesAsync(
			GroupedDependencyChain groups,
			bool unloadUnusedDependencies = true,
			CancellationToken ct = default
		)
		{
			currentDependencies.Clear();

			// Below we load the groups as a pipeline, where all scenes are loaded simultaneously,
			// but each group is only activated when the previous group is fully loaded and activated
			//
			// For example:
			// Group 0:   Load -- Activate
			// Group 1:   Load ------------ Activate
			// Group 2:   Load ---------------------- Activate
			// Group 3:   Load -------------------------------- Activate

			using (ListPool<Exception>.Get(out var exceptions))
			{
				// start to parallel load the scenes
				var loadTasks = new UniTask[groups.Count];
				for (int i = 0; i < groups.Count; i++)
				{
					var scenes = groups.GetGroup(i);
					var loadScenesTasks = new UniTask[scenes.Count];
					for (int j = 0; j < scenes.Count; j++)
					{
						loadScenesTasks[j] = LoadDependencyScene(scenes[j], exceptions);
					}

					loadTasks[i] = UniTask.WhenAll(loadScenesTasks);
				}

				// create the pipeline
				var pipeline = UniTask.CompletedTask;
				for (int i = 0; i < groups.Count; i++)
				{
					pipeline = ActivateGroupAfterPrevious(groups.GetGroup(i), loadTasks[i], pipeline, exceptions);
				}

				await pipeline;

				if (exceptions.Count > 0)
				{
					throw new AggregateException(exceptions);
				}

				if (unloadUnusedDependencies)
				{
					await UnloadUnusedDependenciesAsync(ct);
				}
			}

			ct.ThrowIfCancellationRequested();

			return;

			async UniTask ActivateGroupAfterPrevious(
				IReadOnlyList<string> scenes,
				UniTask loadTask, UniTask previousActivation,
				List<Exception> exceptions
			)
			{
				await UniTask.WhenAll(loadTask, previousActivation);

				foreach (var scene in scenes)
				{
					// if a scene load failed, there will not be an instance for it
					// but we still need to finish the load operation for all the
					// other scenes so that the ResourceManager isn't locked up
					if (!pathToInstance.TryGetValue(scene, out var instance)) continue;

					try
					{
						// activation must happen regardless of cancellation, so we do not pass ct
						// ReSharper disable once MethodSupportsCancellation
						await instance.ActivateAsync().ToUniTask();
					}
					catch (Exception e)
					{
						// we need to make sure all scenes are activated or else we'll lock up ResourceManager
						exceptions.Add(e);
					}
				}
			}

			async UniTask LoadDependencyScene(string scene, List<Exception> exceptions)
			{
				// already loaded
				if (loadedDependencies.Contains(scene))
				{
					currentDependencies.Add(scene);
					return;
				}

				SceneInstance sceneInstance;
				try
				{
					if (pathToInstance.ContainsKey(scene))
					{
						throw new(
							$"There is already a handle for {scene} but it is not in the {nameof(loadedDependencies)} set."
						);
					}

					// try to load the scene
					var op = Addressables.LoadSceneAsync(scene, LoadSceneMode.Additive, false);
					sceneInstance = await op;

					if (op.Status == AsyncOperationStatus.Failed)
					{
						Addressables.Release(op);
						throw new($"Failed to load {scene}", op.OperationException);
					}
				}
				catch (Exception e)
				{
					// keep track of exceptions but keep going b/c we need all loading scenes to be fully activated
					// or else we'll lock up the ResourceManager
					exceptions.Add(e);
					return;
				}

				var path = sceneInstance.Scene.path;
				loadedDependencies.Add(path);
				currentDependencies.Add(path);
				pathToInstance[path] = sceneInstance;
			}
		}

		/// <summary>
		/// Loads all the dependencies for the provided scene.
		/// Dependencies are determined by calling <see cref="GetDependenciesAsync"/>.
		/// </summary>
		/// <param name="scenePath">The path of the scene--this should be the same as the addressables key.</param>
		/// <param name="unloadUnusedDependencies">Whether to unload already loaded dependency scenes that are no longer in use after loading the current groups. If false, you will need to call <see cref="UnloadUnusedDependenciesAsync"/> manually.</param>
		public static async UniTask LoadDependenciesAsync(
			string scenePath,
			bool unloadUnusedDependencies = true,
			CancellationToken ct = default
		) => await LoadDependenciesAsync(await GetDependenciesAsync(scenePath), unloadUnusedDependencies, ct);

		/// <summary>
		/// Unloads all currently unused dependencies.
		/// </summary>
		/// <param name="ct"></param>
		public static async UniTask UnloadUnusedDependenciesAsync(CancellationToken ct = default)
		{
			using var _ = HashSetPool<string>.Get(out var toUnload);
			toUnload.UnionWith(loadedDependencies);
			toUnload.ExceptWith(currentDependencies);

			using var __ = ListPool<UniTask>.Get(out var tasks);
			foreach (var scene in toUnload)
			{
				tasks.Add(UnloadDependencyAsync(pathToInstance[scene]));
			}

			await UniTask.WhenAll(tasks);
		}

		/// <summary>
		/// Unloads all scene dependencies.
		/// </summary>
		public static async UniTask UnloadAllAsync()
		{
			using var _ = ListPool<UniTask>.Get(out var tasks);
			using var __ = ListPool<string>.Get(out var deps);
			deps.AddRange(loadedDependencies); // cache loadedDependencies b/c it will be modified by Unload
			foreach (var scene in deps)
			{
				tasks.Add(UnloadDependencyAsync(pathToInstance[scene]));
			}

			await UniTask.WhenAll(tasks);
			currentDependencies.Clear();
		}

		/// <summary>
		/// Returns the fully-resolved <see cref="GroupedDependencyChain"/> for the provided scene
		/// using <see cref="DependencyListsProvider.GetDependencyLists()"/>
		/// </summary>
		/// <param name="scenePath">The path of the scene--this should be the same as the addressables key.</param>
		public static async UniTask<GroupedDependencyChain> GetDependenciesAsync(string scenePath) =>
			GetDependencies(scenePath, await DependencyListsProvider.GetDependencyListsAsync());

		/// <summary>
		/// Returns the fully-resolved <see cref="GroupedDependencyChain"/> for the provided scene.
		/// </summary>
		/// <param name="scenePath">The path of the scene--this should be the same as the addressables key.</param>
		public static GroupedDependencyChain GetDependencies(
			string scenePath, IList<ISceneDependencyProvider> dependencyLists
		)
		{
			using var dependencyCache = new DependencyCache(dependencyLists);

			using var _ = ListPool<string>.Get(out var depsTopo);
			GetPostOrderDependencies(scenePath, dependencyCache, depsTopo);

			// remove root scene from topo before building group
			depsTopo.RemoveAt(depsTopo.Count - 1);

			// group dependencies in post-order topo list by depth
			return new(depsTopo, dependencyCache);
		}

		private static async UniTask UnloadDependencyAsync(SceneInstance instance)
		{
			loadedDependencies.Remove(instance.Scene.path);
			pathToInstance.Remove(instance.Scene.path);
			await Addressables.UnloadSceneAsync(instance);
		}

		/// <summary>
		/// Enumerates the dependencies for the provided scene into a reverse topological order.
		/// </summary>
		/// <param name="scenePath">The path of the scene--this should be the same as the addressables key.</param>
		/// <param name="dependencyCache">Used to evaluated dependencies. May be pre-filled if necessary.</param>
		/// <param name="order">A list to fill with the reversed topological order</param>
		/// <exception cref="CyclicDependenciesException">Thrown if any cycles are detected in the dependency graph</exception>
		private static void GetPostOrderDependencies(
			string scenePath, DependencyCache dependencyCache, List<string> order
		)
		{
			using var _ = HashSetPool<string>.Get(out var visiting);
			using var __ = HashSetPool<string>.Get(out var visited);
			using var ___ = StackPool<(string scenePath, bool expanded)>.Get(out var search);

			// DFS to generate the post-order topological sort and check for cycles
			search.Push((scenePath, false));
			while (search.TryPop(out var current))
			{
				if (!current.expanded)
				{
					// mark as visiting and check for cycles
					if (!visiting.Add(current.scenePath))
					{
						throw new CyclicDependenciesException();
					}

					// expand children of this node and add to search
					search.Push((current.scenePath, true));
					foreach (var dependency in dependencyCache.GetImmediateDependencies(current.scenePath))
					{
						if (visited.Contains(dependency))
						{
							continue;
						}

						search.Push((dependency, false));
					}
				}
				else
				{
					// finalize node after all children have been visited
					visited.Add(current.scenePath);
					visiting.Remove(current.scenePath);
					order.Add(current.scenePath);
				}
			}
		}
	}
}