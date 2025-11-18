using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace AeLa.Utilities.SceneDeps
{
	/// <summary>
	/// Evaluates dependencies for scenes and caches previously evaluated dependencies for future lookup.
	/// </summary>
	internal struct DependencyCache : IDisposable
	{
		private Dictionary<string, List<string>> cache;
		private IList<ISceneDependencyProvider> dependencyLists;

		public DependencyCache(IList<ISceneDependencyProvider> dependencyLists)
		{
			this.dependencyLists = dependencyLists;
			cache = DictionaryPool<string, List<string>>.Get();
		}

		/// <summary>
		/// Returns the immediate dependencies for the provided scene.
		/// If this is the first time this scene's dependencies have been evaluated,
		/// they will be determined from <see cref="dependencyLists"/> and the results will be cached for future calls.
		/// </summary>
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

		/// <summary>
		/// Empties the cache and releases pooled lists/dictionaries.
		/// </summary>
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
}