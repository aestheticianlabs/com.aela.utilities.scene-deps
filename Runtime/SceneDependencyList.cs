using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AeLa.Utilities.SceneDeps
{
	/// <summary>
	/// Assigns dependencies for scenes matching a regex pattern.
	/// </summary>
	[CreateAssetMenu(menuName = "Scene Dependencies/Scene Dependency List")]
	public class SceneDependencyList : ScriptableSceneDependencyProvider
	{
		/// <summary>
		/// Dependencies from this list will be added first, if any
		/// </summary>
		[Tooltip("Dependencies from this list will be added first, if any")]
		public SceneDependencyList[] InheritDependencies;

		/// <summary>
		/// Regex used to match scenes by path to this list.
		/// </summary>
		[Tooltip("Regex used to match scenes by path to this list.")]
		public string MatchScenes;

		/// <summary>
		/// The dependency scenes to load before matched scenes.
		/// </summary>
		public SceneField[] Dependencies;

		public override void GetDependencies(string scenePath, List<string> dependencies)
		{
			if (!CheckMatchesScene(scenePath)) return;

			AddInheritedDependencies(scenePath, dependencies);
			AddDependencies(scenePath, dependencies);
		}

		protected virtual bool CheckMatchesScene(string scenePath)
		{
			return Regex.IsMatch(scenePath, MatchScenes);
		}

		protected virtual void AddInheritedDependencies(string scenePath, List<string> dependencies)
		{
			if (InheritDependencies == null) return;

			// add inherited dependencies first
			foreach (var list in InheritDependencies)
			{
				list.GetDependencies(scenePath, dependencies);
			}
		}

		protected virtual void AddDependencies(string scenePath, List<string> dependencies)
		{
			if (Dependencies == null) return;

			// add our dependencies
			foreach (var scene in Dependencies)
			{
				dependencies.Add(scene.ScenePath);
			}
		}
	}
}