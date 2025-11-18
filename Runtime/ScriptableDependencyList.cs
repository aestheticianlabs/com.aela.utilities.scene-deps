using System.Collections.Generic;
using UnityEngine;

namespace AeLa.Utilities.SceneDeps
{
	public abstract class ScriptableDependencyList : ScriptableObject, IDependencyList
	{
		public abstract void GetDependencies(string scenePath, List<string> arraySegment);
	}
}