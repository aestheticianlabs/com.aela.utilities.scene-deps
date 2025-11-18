using System.Collections.Generic;

namespace AeLa.Utilities.SceneDeps.Tests.EditMode
{
	public class UnreliableDependencyList : IDependencyList
	{
		public readonly string Match;
		public readonly string[] FirstCallDependencies;

		public static readonly string[] BadDependencies = { "badDep1", "badDep2", "badDep3" };

		private bool called;

		public UnreliableDependencyList(string match, params string[] firstCallDependencies)
		{
			Match = match;
			FirstCallDependencies = firstCallDependencies;
		}

		public void GetDependencies(string scenePath, List<string> dependencies)
		{
			if (Match != null && scenePath != Match) return;

			dependencies.AddRange(called ? BadDependencies : FirstCallDependencies);
			called = true;
		}
	}
}