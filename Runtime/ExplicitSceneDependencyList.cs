using System.Collections.Generic;
using UnityEngine;

namespace AeLa.Utilities.SceneDeps
{
	/// <summary>
	/// Assigns dependencies for specific scenes
	/// </summary>
	[CreateAssetMenu(menuName = "Scene Dependencies/Explicit Scene Dependency List")]
	public class ExplicitSceneDependencyList : SceneDependencyList
	{
		/// <summary>
		/// Scenes that should have these dependencies
		/// </summary>
		[Tooltip("Scenes that should have these dependencies")]
		[SerializeField] private SceneField[] scenes;

		private HashSet<string> paths;

		protected override bool CheckMatchesScene(string scenePath)
		{
			if (paths == null)
			{
				paths = new();
				foreach (var scene in scenes)
				{
					paths.Add(scene.ScenePath);
				}
			}

			return paths.Contains(scenePath);
		}
	}
}