using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AeLa.Utilities.SceneDeps
{
	[CreateAssetMenu(menuName = "Scene Dependencies/Scene Dependency List")]
	public class SceneDependencyList : ScriptableDependencyList
	{
		public SceneDependencyList[] InheritDependencies;

		/// <summary>
		/// Regex used to match scenes by path to this list.
		/// </summary>
		[Tooltip("Regex used to match scenes by path to this list.")]
		public string MatchScenes;

		/// <summary>
		/// The dependency scenes to load before matched scenes.
		/// </summary>
		public SceneReference[] Dependencies;

		public override void GetDependencies(string scenePath, List<string> dependencies)
		{
			if (!Regex.IsMatch(MatchScenes, scenePath)) return;

			// add inherited dependencies first
			foreach (var list in InheritDependencies)
			{
				list.GetDependencies(scenePath, dependencies);
			}

			// add our dependencies
			foreach (var scene in Dependencies)
			{
				dependencies.Add(scene.ScenePath);
			}
		}
	}
}