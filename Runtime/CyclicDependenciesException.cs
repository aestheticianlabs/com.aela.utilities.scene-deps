using System;

namespace AeLa.Utilities.SceneDeps
{
	/// <summary>
	/// Thrown if a cycle is found while evaluating the scene dependency graph.
	/// </summary>
	public class CyclicDependenciesException : Exception
	{
		internal CyclicDependenciesException() : base("Cycle detected while evaluating dependency chain.")
		{
		}
	}
}