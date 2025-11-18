using UnityEngine;
using UnityEngine.SceneManagement;

namespace AeLa.Utilities.SceneDeps
{
	/// <summary>
	/// Component used to test that a scene's dependencies are loaded before this scene.
	/// </summary>
	public class TestDependencyAssert : MonoBehaviour
	{
		public SceneField[] Dependencies;

		private void Awake()
		{
			foreach (var scene in Dependencies)
			{
				var loadedScene = SceneManager.GetSceneByPath(scene);
				Debug.Assert(
					loadedScene.IsValid() && loadedScene.isLoaded,
					$"{gameObject.scene.name}: Dependency not loaded: {scene.ScenePath}"
				);
			}
		}
	}
}