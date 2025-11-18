using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AeLa.Utilities.Pool;
using UnityEngine.Pool;

namespace AeLa.Utilities.SceneDeps
{
	public static class SceneDependencies
	{
		/// <summary>
		/// Loads all the additive dependencies for the provided scene.
		/// </summary>
		/// <param name="scenePath"></param>
		public static async void LoadDependencies(string scenePath)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Returns the fully-resolved <see cref="GroupedDependencyChain"/> for the provided scene.
		/// </summary>
		public static async Task<GroupedDependencyChain> GetDependencies(string scenePath) =>
			GetDependencies(scenePath, await DependencyListProvider.GetDependencyLists());

		/// <summary>
		/// Returns the fully-resolved <see cref="GroupedDependencyChain"/> for the provided scene.
		/// </summary>
		public static GroupedDependencyChain GetDependencies(
			string scenePath, IReadOnlyList<IDependencyList> dependencyLists
		)
		{
			using var dependencyCache = new DependencyCache(dependencyLists);

			using var _ = ListPool<string>.Get(out var depsTopo);
			GetPostOrderDependencies(scenePath, dependencyCache, depsTopo);

			// group dependencies in post-order topo list by depth
			return new(depsTopo, dependencyCache);
		}

		private static void GetPostOrderDependencies(
			string scenePath, DependencyCache dependencyCache, List<string> order
		)
		{
			using var _ = HashSetPool<string>.Get(out var visiting);
			using var __ = HashSetPool<string>.Get(out var visited);
			using var ___ = StackPool<(string scenePath, bool expanded)>.Get(out var search);

			// DFS to generate the post-order topological sort and check for cycles
			search.Push((scenePath, false));
			while (search.TryPop(out var current))
			{
				if (!current.expanded)
				{
					// mark as visiting and check for cycles
					if (!visiting.Add(current.scenePath))
					{
						throw new CyclicDependenciesException();
					}

					// expand children of this node and add to search
					search.Push((current.scenePath, true));
					foreach (var dependency in dependencyCache.GetImmediateDependencies(current.scenePath))
					{
						if (visited.Contains(dependency))
						{
							continue;
						}

						search.Push((dependency, false));
					}
				}
				else
				{
					// finalize node after all children have been visited
					visited.Add(current.scenePath);
					order.Add(current.scenePath);
				}
			}
		}
	}

	internal struct DependencyCache : IDisposable
	{
		private Dictionary<string, List<string>> cache;
		private IReadOnlyList<IDependencyList> dependencyLists;

		public DependencyCache(IReadOnlyList<IDependencyList> dependencyLists)
		{
			this.dependencyLists = dependencyLists;
			cache = DictionaryPool<string, List<string>>.Get();
		}

		public List<string> GetImmediateDependencies(string scenePath)
		{
			if (cache == null)
			{
				throw new ObjectDisposedException(nameof(DependencyCache));
			}

			if (!cache.TryGetValue(scenePath, out var dependencies))
			{
				dependencies = ListPool<string>.Get();
				foreach (var list in dependencyLists)
				{
					list.GetDependencies(scenePath, dependencies);
				}

				cache[scenePath] = dependencies;
			}

			return dependencies;
		}

		public void Dispose()
		{
			if (cache == null)
			{
				throw new ObjectDisposedException(nameof(DependencyCache));
			}

			foreach (var list in cache.Values)
			{
				ListPool<string>.Release(list);
			}

			DictionaryPool<string, List<string>>.Release(cache);
			cache = null;
		}
	}


	/// <summary>
	/// Thrown if a cycle is found while evaluating the scene dependency graph.
	/// </summary>
	public class CyclicDependenciesException : Exception
	{
		internal CyclicDependenciesException() : base("Cycle detected while evaluating dependency chain.")
		{
		}
	}

	public static class DependencyListProvider
	{
		public static async Task<IReadOnlyList<IDependencyList>> GetDependencyLists()
		{
			throw new NotImplementedException();
		}
	}
}