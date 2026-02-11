using System.Collections.Generic;
using UnityEngine;

namespace AeLa.Utilities.SceneDeps
{
	public abstract class SceneDependencyList : ScriptableSceneDependencyProvider
	{
		/// <summary>
		/// Dependencies from this list will be added first, if any
		/// </summary>
		[Tooltip("Dependencies from this list will be added first, if any")]
		public SceneDependencyList[] InheritDependencies;

		/// <summary>
		/// The dependency scenes to load before matched scenes.
		/// </summary>
		public SceneField[] Dependencies;

		protected abstract bool CheckMatchesScene(string scenePath);

		public override void GetDependencies(string scenePath, List<string> dependencies)
		{
			if (!CheckMatchesScene(scenePath)) return;

			AddInheritedDependencies(scenePath, dependencies);
			AddDependencies(scenePath, dependencies);
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