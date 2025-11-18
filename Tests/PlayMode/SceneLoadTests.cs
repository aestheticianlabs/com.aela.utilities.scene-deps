using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using AeLa.Utilities.SceneDeps.Tests.Shared;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AeLa.Utilities.SceneDeps.Tests.PlayMode
{
	public class SceneLoadTests
	{
		private SceneInstance loadedMainScene;

		[UnityTearDown]
		public IEnumerator TearDown() => UniTask.ToCoroutine(async () =>
			{
				await UnloadMainScene();
				await SceneDependencies.UnloadAllAsync();

				for (var i = 1; i < SceneManager.sceneCount; i++)
				{
					var scene = SceneManager.GetSceneAt(i);
					Debug.LogError($"Scene {scene.name} was not unloaded");
				}
			}
		);

		[UnityTest]
		public IEnumerator LoadDependentScenes_FromCodeDefinedList()
		{
			///       A
			///     /   \
			///   B(1)  C(2)
			///   /       \
			/// D(0)     E(1)
			///             \
			///            F(0)
			var dependencyLists = new List<ISceneDependencyProvider>
			{
				new DummySceneDependencyProvider(AsPath("A"), AsPath("B"), AsPath("C")),
				new DummySceneDependencyProvider(AsPath("B"), AsPath("D")),
				new DummySceneDependencyProvider(AsPath("C"), AsPath("E")),
				new DummySceneDependencyProvider(AsPath("E"), AsPath("F"))
			};

			var groups = SceneDependencies.GetDependencies(AsPath("A"), dependencyLists);

			return Test().ToCoroutine();

			async UniTask Test()
			{
				// weird behavior: if we LoadMainScene before LoadDependencies, LoadDependencies gets stuck waiting for the main scene to load. There's a hint as to why here: https://discussions.unity.com/t/addressables-assetloading-is-blocked-by-async-scene-loading/741632/5
				await SceneDependencies.LoadDependenciesAsync(groups);
				await LoadMainScene("A");

				// scene count includes test run scene
				Assert.AreEqual(7, SceneManager.loadedSceneCount);
			}
		}

		[UnityTest]
		public IEnumerator UnusedDependenciesUnloaded()
		{
			///       A
			///     /   \
			///   B(1)  C(2)
			///   /       \
			/// D(0)     E(1)
			///             \
			///            F(0)
			var dependencyLists = new List<ISceneDependencyProvider>
			{
				new DummySceneDependencyProvider(AsPath("A"), AsPath("B"), AsPath("C")),
				new DummySceneDependencyProvider(AsPath("B"), AsPath("D")),
				new DummySceneDependencyProvider(AsPath("C"), AsPath("E")),
				new DummySceneDependencyProvider(AsPath("E"), AsPath("F"))
			};

			var groups = SceneDependencies.GetDependencies(AsPath("A"), dependencyLists);

			return Test().ToCoroutine();

			async UniTask Test()
			{
				// weird behavior: if we LoadMainScene before LoadDependencies, LoadDependencies gets stuck waiting for the main scene to load. There's a hint as to why here: https://discussions.unity.com/t/addressables-assetloading-is-blocked-by-async-scene-loading/741632/5
				await SceneDependencies.LoadDependenciesAsync(groups);
				await LoadMainScene("A");

				// scene count includes test runn scene
				Assert.AreEqual(7, SceneManager.loadedSceneCount);

				await UnloadMainScene();
				await SceneDependencies.LoadDependenciesAsync(GroupedDependencyChain.Empty);

				Debug.Break();
				await UniTask.Yield();

				Assert.AreEqual(1, SceneManager.sceneCount);
			}
		}

		[UnityTest]
		public IEnumerator LoadDependentScenes_SameDependenciesPreserved()
		{
			///       A
			///     /   \
			///   B(1)  C(2)
			///   /       \
			/// D(0)     E(1)
			///             \
			///            F(0)
			var depList1 = new List<ISceneDependencyProvider>
			{
				new DummySceneDependencyProvider(AsPath("A"), AsPath("B"), AsPath("C")),
				new DummySceneDependencyProvider(AsPath("B"), AsPath("D")),
				new DummySceneDependencyProvider(AsPath("C"), AsPath("E")),
				new DummySceneDependencyProvider(AsPath("E"), AsPath("F"))
			};

			///      A 1
			///     /   \
			///   B(1)  G(1)
			///   /		  \
			/// D(0)     H(0)
			var depList2 = new List<ISceneDependencyProvider>
			{
				new DummySceneDependencyProvider(AsPath("A 1"), AsPath("B"), AsPath("G")),
				new DummySceneDependencyProvider(AsPath("B"), AsPath("D")),
				new DummySceneDependencyProvider(AsPath("G"), AsPath("H"))
			};

			return Test().ToCoroutine();

			async UniTask Test()
			{
				// weird behavior: if we LoadMainScene before LoadDependencies, LoadDependencies gets stuck waiting for the main scene to load. There's a hint as to why here: https://discussions.unity.com/t/addressables-assetloading-is-blocked-by-async-scene-loading/741632/5
				await SceneDependencies.LoadDependenciesAsync(
					SceneDependencies.GetDependencies(AsPath("A"), depList1)
				);
				await LoadMainScene("A");

				// scene count includes test run scene
				Assert.AreEqual(7, SceneManager.loadedSceneCount);

				await UnloadMainScene();

				// create flag objects in scenes B and D to make sure they remain loaded
				var bFlag = AddSceneFlagObject("B");
				var dFlag = AddSceneFlagObject("D");

				await SceneDependencies.LoadDependenciesAsync(
					SceneDependencies.GetDependencies(AsPath("A 1"), depList2)
				);

				Assert.AreEqual(5, SceneManager.loadedSceneCount, "Not all dependencies were loaded");

				Assert.IsTrue(bFlag, "Scene B was unloaded");
				Assert.IsTrue(dFlag, "Scene D was unloaded");
			}

			GameObject AddSceneFlagObject(string sceneName)
			{
				SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));
				return new($"{sceneName} Flag");
			}
		}

		[UnityTest]
		public IEnumerator LoadDependentScenes_FromProviderAssets() => UniTask.ToCoroutine(async () =>
			{
				/// Dependencies should be configured like this in the editor
				///       A
				///     /   \
				///   B(1)  C(2)
				///   /       \
				/// D(0)     E(1)
				///             \
				///            F(0)

				// weird behavior: if we LoadMainScene before LoadDependencies, LoadDependencies gets stuck waiting for the main scene to load. There's a hint as to why here: https://discussions.unity.com/t/addressables-assetloading-is-blocked-by-async-scene-loading/741632/5
				await SceneDependencies.LoadDependenciesAsync(AsPath("A"));
				await LoadMainScene("A");

				// scene count includes test run scene
				Assert.AreEqual(7, SceneManager.loadedSceneCount);
			}
		);

		[UnityTest]
		public IEnumerator LoadDependentScenes_Cancel_ThrowsException()
		{
			///       A
			///     /   \
			///   B(1)  C(2)
			///   /       \
			/// D(0)     E(1)
			///             \
			///            F(0)
			var dependencyLists = new List<ISceneDependencyProvider>
			{
				new DummySceneDependencyProvider(AsPath("A"), AsPath("B"), AsPath("C")),
				new DummySceneDependencyProvider(AsPath("B"), AsPath("D")),
				new DummySceneDependencyProvider(AsPath("C"), AsPath("E")),
				new DummySceneDependencyProvider(AsPath("E"), AsPath("F"))
			};

			var groups = SceneDependencies.GetDependencies(AsPath("A"), dependencyLists);

			return Test().ThrowsException<OperationCanceledException>().ToCoroutine();

			UniTask Test()
			{
				var cts = new CancellationTokenSource();
				var task = SceneDependencies.LoadDependenciesAsync(groups, cts.Token);
				cts.Cancel();
				return task;
			}
		}

		[UnityTest]
		public IEnumerator LoadDependentScenes_Cancel_UnloadWorks()
		{
			///       A
			///     /   \
			///   B(1)  C(2)
			///   /       \
			/// D(0)     E(1)
			///             \
			///            F(0)
			var dependencyLists = new List<ISceneDependencyProvider>
			{
				new DummySceneDependencyProvider(AsPath("A"), AsPath("B"), AsPath("C")),
				new DummySceneDependencyProvider(AsPath("B"), AsPath("D")),
				new DummySceneDependencyProvider(AsPath("C"), AsPath("E")),
				new DummySceneDependencyProvider(AsPath("E"), AsPath("F"))
			};

			var groups = SceneDependencies.GetDependencies(AsPath("A"), dependencyLists);

			return Test().ToCoroutine();

			async UniTask Test()
			{
				var cts = new CancellationTokenSource();
				var task = SceneDependencies.LoadDependenciesAsync(groups, cts.Token);
				cts.Cancel();

				await task.ThrowsException<OperationCanceledException>();

				// ReSharper disable once MethodSupportsCancellation
				await SceneDependencies.UnloadAllAsync();
				Assert.AreEqual(1, SceneManager.sceneCount);
			}
		}

		private async UniTask LoadMainScene(string name)
		{
			var op = Addressables.LoadSceneAsync(AsPath(name), LoadSceneMode.Additive, false);
			await op.Task;

			loadedMainScene = op.Result;
			await loadedMainScene.ActivateAsync();
			SceneManager.SetActiveScene(loadedMainScene.Scene);
		}

		private async UniTask UnloadMainScene()
		{
			if (loadedMainScene.Scene.IsValid() && loadedMainScene.Scene.isLoaded)
			{
				await Addressables.UnloadSceneAsync(loadedMainScene);
			}
		}

		private string AsPath(string name) => $"Assets/SceneDepsTests/Scenes/{name}.unity";
	}
}