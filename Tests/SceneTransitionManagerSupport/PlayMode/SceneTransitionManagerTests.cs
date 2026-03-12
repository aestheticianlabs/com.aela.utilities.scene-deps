using System.Collections;
using AeLa.Utilities.Debugging;
using AeLa.Utilities.SceneDeps.Tests.Shared;
using AeLa.Utilities.SceneTransition;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static AeLa.Utilities.SceneDeps.Tests.Shared.TestUtils;

namespace AeLa.Utilities.SceneDeps.SceneTransitionManagerSupport.Tests.PlayMode
{
	public class SceneTransitionManagerTests
	{
		private GameObject managersGameObject;

		[UnitySetUp]
		public IEnumerator SetUp() => UniTask.ToCoroutine(async () =>
			{
				// weird quirk: the test won't finish running if we don't load an empty scene in setup
				// I think this is because Unity does not handle things gracefully if we unload the test runner scene after a test has been started
				SceneManager.LoadScene(0);

				// add a scene transition manager
				managersGameObject = new()
				{
					name = "[ Scene Managers ]",
					hideFlags = HideFlags.DontSave | HideFlags.NotEditable
				};

				var stm = managersGameObject.AddComponent<SceneTransitionManager>();
				stm.LogLevel = LogLevel.Info;
				managersGameObject.AddComponent<STM_SceneDependencyManager>();

				await UniTask.WaitWhile(() => SceneTransitionManager.Instance.IsLoading);
			}
		);

		[UnityTearDown]
		public IEnumerator TearDown() => UniTask.ToCoroutine(async () =>
			{
				Object.Destroy(managersGameObject);

				// wait for processing after STM_SceneDependencyManager destroy
				await UniTask.Yield();
				await SceneDependencies.WaitForReleaseQueueAsync();

				for (var i = 1; i < SceneManager.sceneCount; i++)
				{
					var scene = SceneManager.GetSceneAt(i);
					Debug.LogError($"Scene {scene.name} was not unloaded");
				}
			}
		);

		[UnityTest]
		public IEnumerator SceneTransition_LoadsDependentScenes()
		{
			/// Dependencies should be configured like this in the editor
			///       A
			///     /   \
			///   B(1)  C(2)
			///   /       \
			/// D(0)     E(1)
			///             \
			///            F(0)

			SceneTransitionManager.Instance.ChangeScene(AsPath("A"));

			yield return new WaitWhile(() => SceneTransitionManager.Instance.IsLoading);

			AssertUtils.IsLoaded(AsPath("A"));
			AssertUtils.IsLoaded(AsPath("B"));
			AssertUtils.IsLoaded(AsPath("C"));
			AssertUtils.IsLoaded(AsPath("D"));
			AssertUtils.IsLoaded(AsPath("E"));
			AssertUtils.IsLoaded(AsPath("F"));

			Assert.IsTrue(SceneManager.GetActiveScene().path == AsPath("A"), "Scene A is not the active scene");

			Assert.AreEqual(6, SceneManager.loadedSceneCount);
		}

		[UnityTest]
		public IEnumerator SceneTransition_LoadsDependentScenes_SameDependenciesPreserved()
		{
			/// Dependencies should be configured like this in the editor
			///       A
			///     /   \
			///   B(1)  C(2)
			///   /       \
			/// D(0)     E(1)
			///             \
			///            F(0)
			///
			///      A 1
			///     /   \
			///   B(1)  G(1)
			///   /		  \
			/// D(0)     H(0)

			// load A
			SceneTransitionManager.Instance.ChangeScene(AsPath("A"));

			yield return new WaitWhile(() => SceneTransitionManager.Instance.IsLoading);

			AssertUtils.IsLoaded(AsPath("A"));
			AssertUtils.IsLoaded(AsPath("B"));
			AssertUtils.IsLoaded(AsPath("C"));
			AssertUtils.IsLoaded(AsPath("D"));
			AssertUtils.IsLoaded(AsPath("E"));
			AssertUtils.IsLoaded(AsPath("F"));

			Assert.AreEqual(6, SceneManager.loadedSceneCount);

			// create flag objects in scenes B and D to make sure they remain loaded
			var bFlag = AddSceneFlagObject("B");
			var dFlag = AddSceneFlagObject("D");

			// load A 1
			SceneTransitionManager.Instance.ChangeScene(AsPath("A 1"));
			yield return new WaitWhile(() => SceneTransitionManager.Instance.IsLoading);

			AssertUtils.IsLoaded(AsPath("A 1"));
			AssertUtils.IsLoaded(AsPath("B"));
			AssertUtils.IsLoaded(AsPath("D"));
			AssertUtils.IsLoaded(AsPath("G"));
			AssertUtils.IsLoaded(AsPath("H"));

			Assert.IsTrue(bFlag, "Scene B was unloaded");
			Assert.IsTrue(dFlag, "Scene D was unloaded");

			Assert.AreEqual(5, SceneManager.loadedSceneCount);
		}
	}
}