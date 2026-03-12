using System;
using System.Collections.Generic;
using System.Threading;
using AeLa.Utilities.Pooling;
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
		/// <summary>
		/// Holds references to dependencies loaded by <see cref="LoadDependenciesAsync"/>.
		/// Release to decrement the reference count for associated dependencies.
		/// Dependencies will be unloaded if their reference count is 0 after release.
		/// </summary>
		public class Handle : IDisposable
		{
			private PooledArray<string> scenes;
			public Span<string> Scenes => scenes.Span;

			private bool isDisposed;

			public bool IsReleased { get; private set; }

			public bool IsValid => !isDisposed && scenes.Array != null;

			public Handle(PooledArray<string> scenes)
			{
				this.scenes = scenes;
				isDisposed = false;
				IsReleased = false;
			}

			/// <summary>
			/// Releases the handle and unloads any dependency scenes that have no more references.
			/// </summary>
			/// <param name="handle"></param>
			public void Release()
			{
				if (IsReleased) return;
				ReleaseAsync().Forget();
			}

			/// <summary>
			/// Releases the handle and unloads any dependency scenes that have no more references.
			/// </summary>
			/// <param name="handle"></param>
			public async UniTask ReleaseAsync()
			{
				if (IsReleased || !IsValid) return;
				IsReleased = true;
				await ReleaseInternalAsync(this);
			}

			/// <summary>
			/// Disposes the handle and releases it if not already released.
			/// </summary>
			/// <param name="handle"></param>
			public void Dispose()
			{
				if (isDisposed) return;

				Release();
				isDisposed = true;
				scenes.Dispose();
			}
		}

		private static Dictionary<string, SceneInstance> pathToInstance = new();

		/// <summary>
		/// Loaded dependencies and their reference counts
		/// </summary>
		private static Dictionary<string, int> loadedDependencies = new();

		private static Dictionary<string, AsyncOperationHandle<SceneInstance>> loadingScenes = new();

		private static Queue<Handle> releaseQueue = new();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatic()
		{
			pathToInstance = new();
			loadedDependencies = new();
			releaseQueue = new();
		}

		/// <summary>
		/// Additively loads all the dependency scenes in the provided <see cref="GroupedDependencyChain"/>
		/// in parallel, then activates them once all of their dependencies are loaded and activated.
		/// </summary>
		/// <param name="groups">The <see cref="GroupedDependencyChain"/> to load.</param>
		/// <returns>A handle that must be used to release references to the dependency scenes</returns>
		public static async UniTask<Handle> LoadDependenciesAsync(
			GroupedDependencyChain groups,
			CancellationToken ct = default
		)
		{
			// Below we load and activate the groups in order.
			// Scenes within a group are loaded/activated simultaneously.
			using var refs = PooledList<string>.Get();
			using (ListPool<Exception>.Get(out var exceptions))
			{
				for (int i = 0; i < groups.Count; i++)
				{
					var scenes = groups.GetGroup(i);
					var loadScenesTasks = new UniTask[scenes.Count];
					for (int j = 0; j < scenes.Count; j++)
					{
						loadScenesTasks[j] = LoadDependencyScene(scenes[j], exceptions);
					}

					await UniTask.WhenAll(loadScenesTasks);

					refs.List.AddRange(scenes);
				}

				if (exceptions.Count > 0)
				{
					throw new AggregateException(exceptions);
				}
			}

			ct.ThrowIfCancellationRequested();

			// increment reference counts
			foreach (var scene in refs)
			{
				loadedDependencies[scene]++;
			}

			return new(refs.ToPooledArray());

			async UniTask LoadDependencyScene(string scene, List<Exception> exceptions)
			{
				// already loaded
				if (loadedDependencies.ContainsKey(scene))
				{
					return;
				}

				// wait for existing load operation if any
				if (loadingScenes.TryGetValue(scene, out var existingOperation))
				{
					await existingOperation;
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

					// try to load & activate the scene
					var op = Addressables.LoadSceneAsync(scene, LoadSceneMode.Additive);
					loadingScenes.Add(scene, op);
					sceneInstance = await op;
					loadingScenes.Remove(scene);

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
				loadedDependencies.Add(path, 0);
				pathToInstance[path] = sceneInstance;
			}
		}

		/// <summary>
		/// Loads all the dependencies for the provided scene.
		/// Dependencies are determined by calling <see cref="GetDependenciesAsync"/>.
		/// </summary>
		/// <param name="scenePath">The path of the scene--this should be the same as the addressables key.</param>
		/// <returns>A handle that must be used to release references to the dependency scenes</returns>
		public static async UniTask<Handle> LoadDependenciesAsync(
			string scenePath,
			CancellationToken ct = default
		) => await LoadDependenciesAsync(await GetDependenciesAsync(scenePath), ct);

		/// <summary>
		/// Releases the handle and unloads any dependency scenes that have no more references.
		/// </summary>
		/// <param name="handle"></param>
		/// <exception cref="InvalidOperationException">Thrown if scene is not loaded or already has 0 references.</exception>
		public static UniTask ReleaseAsync(Handle handle)
		{
			return handle.ReleaseAsync();
		}

		private static UniTask ReleaseInternalAsync(Handle handle)
		{
			if (!handle.IsValid) return UniTask.CompletedTask;

			releaseQueue.Enqueue(handle);

			return releaseQueue.Count == 1 ? ProcessReleaseQueueAsync() : WaitForReleaseQueueAsync();
		}

		/// <summary>
		/// Waits for any queued handle release operations to finish.
		/// </summary>
		public static async UniTask WaitForReleaseQueueAsync()
		{
			while (releaseQueue.Count > 0)
			{
				await UniTask.Yield();
			}
		}

		private static async UniTask ProcessReleaseQueueAsync()
		{
			while (releaseQueue.TryPeek(out var handle))
			{
				// update reference counts
				for (var i = 0; i < handle.Scenes.Length; i++)
				{
					var scene = handle.Scenes[i];
					if (!loadedDependencies.TryGetValue(scene, out var referenceCount))
					{
						throw new InvalidOperationException("Attempt to unload dependency scene that is not loaded.");
					}

					if (referenceCount == 0)
					{
						throw new InvalidOperationException("Reference count is already 0");
					}

					loadedDependencies[scene]--;
				}

				// unload scenes with no references
				using var toUnload = PooledList<string>.Get();
				foreach (var (scene, referenceCount) in loadedDependencies)
				{
					if (referenceCount > 0) continue;
					toUnload.Add(scene);
				}

				foreach (var scene in toUnload)
				{
					await UnloadDependencyAsync(pathToInstance[scene]);
				}

				handle.Dispose();
				releaseQueue.Dequeue();
			}
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