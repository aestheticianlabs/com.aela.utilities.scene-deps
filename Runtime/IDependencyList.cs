using System.Collections.Generic;

namespace AeLa.Utilities.SceneDeps
{
	public interface IDependencyList
	{
		void GetDependencies(string scenePath, List<string> dependencies);
	}
}