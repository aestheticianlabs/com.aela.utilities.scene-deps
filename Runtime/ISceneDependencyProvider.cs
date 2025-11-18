using System.Collections.Generic;

namespace AeLa.Utilities.SceneDeps
{
	/// <summary>
	/// Provides scene dependencies.
	/// </summary>
	public interface ISceneDependencyProvider
	{
		/// <summary>
		/// Adds dependencies for <see cref="scenePath"/> to <see cref="dependencies"/>.
		/// </summary>
		/// <param name="scenePath">The path of the scene--this should be the same as the addressables key.</param>
		/// <param name="dependencies">A list of dependencies that this method will add to.</param>
		void GetDependencies(string scenePath, List<string> dependencies);
	}
}