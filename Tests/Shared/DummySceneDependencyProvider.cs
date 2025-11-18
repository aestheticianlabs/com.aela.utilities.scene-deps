using System.Collections.Generic;

namespace AeLa.Utilities.SceneDeps.Tests.Shared
{
	public class DummySceneDependencyProvider : ISceneDependencyProvider
	{
		public readonly string Match;
		public readonly string[] Dependencies;

		public DummySceneDependencyProvider(string match, params string[] dependencies)
		{
			Match = match;
			Dependencies = dependencies;
		}

		public void GetDependencies(string scenePath, List<string> dependencies)
		{
			if (Match != null && scenePath != Match) return;
			dependencies.AddRange(Dependencies);
		}
	}
}