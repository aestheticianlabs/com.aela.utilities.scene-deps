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
using static AeLa.Utilities.SceneDeps.Tests.Shared.TestUtils;

namespace AeLa.Utilities.SceneDeps.Tests.PlayMode
{
	public class SceneLoadTests
	{
		private SceneInstance loadedMainScene;
		private List<SceneDependencies.Handle> loadedHandles = new();
		private bool disableNotUnloadedCheck;

		[UnityTearDown]
		public IEnumerator TearDown() => UniTask.ToCoroutine(async () =>
			{
				await UnloadMainScene();
				foreach (var handle in loadedHandles)
				{
					await handle.ReleaseAsync();
				}

				loadedHandles.Clear();

				if (!disableNotUnloadedCheck)
				{
					for (var i = 1; i < SceneManager.sceneCount; i++)
					{
						var scene = SceneManager.GetSceneAt(i);
						Debug.LogError($"Scene {scene.name} was not unloaded");
					}
				}

				disableNotUnloadedCheck = false;
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
				// weird behavior: if we LoadMainScene before LoadDependencies, LoadDependencies gets stuck waiting for the main scene to load.
				// There's a hint as to why here: https://discussions.unity.com/t/addressables-assetloading-is-blocked-by-async-scene-loading/741632/5
				loadedHandles.Add(await SceneDependencies.LoadDependenciesAsync(groups));
				await LoadMainScene("A");

				AssertUtils.IsLoaded(AsPath("A"));
				AssertUtils.IsLoaded(AsPath("B"));
				AssertUtils.IsLoaded(AsPath("C"));
				AssertUtils.IsLoaded(AsPath("D"));
				AssertUtils.IsLoaded(AsPath("E"));
				AssertUtils.IsLoaded(AsPath("F"));

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
				// weird behavior: if we LoadMainScene before LoadDependencies, LoadDependencies gets stuck waiting for the main scene to load.
				// There's a hint as to why here: https://discussions.unity.com/t/addressables-assetloading-is-blocked-by-async-scene-loading/741632/5
				var handle = await SceneDependencies.LoadDependenciesAsync(groups);
				loadedHandles.Add(handle);
				await LoadMainScene("A");

				AssertUtils.IsLoaded(AsPath("A"));
				AssertUtils.IsLoaded(AsPath("B"));
				AssertUtils.IsLoaded(AsPath("C"));
				AssertUtils.IsLoaded(AsPath("D"));
				AssertUtils.IsLoaded(AsPath("E"));
				AssertUtils.IsLoaded(AsPath("F"));

				// scene count includes test run scene
				Assert.AreEqual(7, SceneManager.loadedSceneCount);

				await UnloadMainScene();
				await handle.ReleaseAsync();
				loadedHandles.Remove(handle);

				Assert.IsFalse(handle.IsValid, "handle.IsValid");

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
				// weird behavior: if we LoadMainScene before LoadDependencies, LoadDependencies gets stuck waiting for the main scene to load.
				// There's a hint as to why here: https://discussions.unity.com/t/addressables-assetloading-is-blocked-by-async-scene-loading/741632/5
				var handleA = await SceneDependencies.LoadDependenciesAsync(
					SceneDependencies.GetDependencies(AsPath("A"), depList1)
				);
				loadedHandles.Add(handleA);
				await LoadMainScene("A");

				AssertUtils.IsLoaded(AsPath("A"));
				AssertUtils.IsLoaded(AsPath("B"));
				AssertUtils.IsLoaded(AsPath("C"));
				AssertUtils.IsLoaded(AsPath("D"));
				AssertUtils.IsLoaded(AsPath("E"));
				AssertUtils.IsLoaded(AsPath("F"));

				// scene count includes test run scene
				Assert.AreEqual(7, SceneManager.loadedSceneCount);

				await UnloadMainScene();

				// create flag objects in scenes B and D so we can check that they remain loaded
				var bFlag = AddSceneFlagObject("B");
				var dFlag = AddSceneFlagObject("D");

				var handleB =
					await SceneDependencies.LoadDependenciesAsync(
						SceneDependencies.GetDependencies(AsPath("A 1"), depList2)
					);

				loadedHandles.Add(handleB);

				// unload A's unused dependencies
				await handleA.ReleaseAsync();
				loadedHandles.Remove(handleA);

				Assert.AreEqual(5, SceneManager.loadedSceneCount, "Not all dependencies were loaded");

				AssertUtils.IsLoaded(AsPath("B"));
				AssertUtils.IsLoaded(AsPath("D"));
				AssertUtils.IsLoaded(AsPath("g"));
				AssertUtils.IsLoaded(AsPath("H"));

				Assert.IsTrue(bFlag, "Scene B was unloaded");
				Assert.IsTrue(dFlag, "Scene D was unloaded");
			}
		}

		[UnityTest]
		public IEnumerator LoadDependentScenes_DontUnloadUnusedDependencies()
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
				var handleA = await SceneDependencies.LoadDependenciesAsync(
					SceneDependencies.GetDependencies(AsPath("A"), depList1)
				);
				loadedHandles.Add(handleA);
				await LoadMainScene("A");

				AssertUtils.IsLoaded(AsPath("A"));
				AssertUtils.IsLoaded(AsPath("B"));
				AssertUtils.IsLoaded(AsPath("C"));
				AssertUtils.IsLoaded(AsPath("D"));
				AssertUtils.IsLoaded(AsPath("E"));
				AssertUtils.IsLoaded(AsPath("F"));

				// scene count includes test run scene
				Assert.AreEqual(7, SceneManager.loadedSceneCount);

				await UnloadMainScene();

				// create flag objects in scenes to make sure they remain loaded
				var bFlag = AddSceneFlagObject("B");
				var cFlag = AddSceneFlagObject("C");
				var dFlag = AddSceneFlagObject("D");
				var eFlag = AddSceneFlagObject("E");
				var fFlag = AddSceneFlagObject("F");

				var handleB = await SceneDependencies.LoadDependenciesAsync(
					SceneDependencies.GetDependencies(AsPath("A 1"), depList2)
				);

				loadedHandles.Add(handleB);

				Assert.IsTrue(bFlag, "Scene B was unloaded");
				Assert.IsTrue(cFlag, "Scene C was unloaded");
				Assert.IsTrue(dFlag, "Scene D was unloaded");
				Assert.IsTrue(eFlag, "Scene E was unloaded");
				Assert.IsTrue(fFlag, "Scene F was unloaded");

				Assert.AreEqual(8, SceneManager.loadedSceneCount, "Incorrect dependency scene count");

				// make sure unloading later works
				await handleA.ReleaseAsync();
				loadedHandles.Remove(handleA);

				Assert.AreEqual(5, SceneManager.loadedSceneCount, "Not all dependencies were loaded");

				Assert.IsTrue(bFlag, "Scene B was unloaded");
				Assert.IsTrue(dFlag, "Scene D was unloaded");
			}
		}

		[UnityTest]
		public IEnumerator ReleaseTwoHandlesSameTime_DependenciesUnloaded()
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
				// weird behavior: if we LoadMainScene before LoadDependencies, LoadDependencies gets stuck waiting for the main scene to load.
				// There's a hint as to why here: https://discussions.unity.com/t/addressables-assetloading-is-blocked-by-async-scene-loading/741632/5
				var handleA = await SceneDependencies.LoadDependenciesAsync(
					SceneDependencies.GetDependencies(AsPath("A"), depList1)
				);
				loadedHandles.Add(handleA);

				AssertUtils.IsLoaded(AsPath("B"));
				AssertUtils.IsLoaded(AsPath("C"));
				AssertUtils.IsLoaded(AsPath("D"));
				AssertUtils.IsLoaded(AsPath("E"));
				AssertUtils.IsLoaded(AsPath("F"));

				// scene count includes test run scene
				Assert.AreEqual(6, SceneManager.loadedSceneCount);

				var handleB = await SceneDependencies.LoadDependenciesAsync(
					SceneDependencies.GetDependencies(AsPath("A 1"), depList2)
				);

				loadedHandles.Add(handleB);

				Assert.AreEqual(8, SceneManager.loadedSceneCount, "Incorrect dependency scene count");

				// release both handles at the same time
				// ReSharper disable MethodHasAsyncOverload
				handleA.Release();
				await handleB.ReleaseAsync();
				// ReSharper restore MethodHasAsyncOverload

				loadedHandles.Remove(handleA);
				loadedHandles.Remove(handleB);

				Assert.AreEqual(1, SceneManager.loadedSceneCount, "Not all dependencies were loaded");
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
				loadedHandles.Add(await SceneDependencies.LoadDependenciesAsync(AsPath("A")));
				await LoadMainScene("A");

				AssertUtils.IsLoaded(AsPath("A"));
				AssertUtils.IsLoaded(AsPath("B"));
				AssertUtils.IsLoaded(AsPath("C"));
				AssertUtils.IsLoaded(AsPath("D"));
				AssertUtils.IsLoaded(AsPath("E"));
				AssertUtils.IsLoaded(AsPath("F"));

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

			disableNotUnloadedCheck = true;
			return Test().ThrowsException<OperationCanceledException>().ToCoroutine();

			UniTask Test()
			{
				var cts = new CancellationTokenSource();
				var task = SceneDependencies.LoadDependenciesAsync(groups, ct: cts.Token);
				cts.Cancel();
				return task;
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
	}
}