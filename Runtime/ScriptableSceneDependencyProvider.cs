using System.Collections.Generic;
using UnityEngine;

namespace AeLa.Utilities.SceneDeps
{
	/// <summary>
	/// An <see cref="ISceneDependencyProvider"/> as a <see cref="ScriptableObject"/>.
	/// </summary>
	public abstract class ScriptableSceneDependencyProvider : ScriptableObject, ISceneDependencyProvider
	{
		public abstract void GetDependencies(string scenePath, List<string> arraySegment);
	}
}