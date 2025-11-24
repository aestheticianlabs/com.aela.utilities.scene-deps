using UnityEngine;
using UnityEngine.SceneManagement;

namespace AeLa.Utilities.SceneDeps.Tests.Shared
{
	public static class TestUtils
	{
		public static string AsPath(string name) => $"Assets/SceneDepsTests/Scenes/{name}.unity";

		public static GameObject AddSceneFlagObject(string sceneName)
		{
			var activeScene = SceneManager.GetActiveScene();
			SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));
			var go = new GameObject($"{sceneName} Flag");
			SceneManager.SetActiveScene(activeScene);
			return go;
		}
	}
}