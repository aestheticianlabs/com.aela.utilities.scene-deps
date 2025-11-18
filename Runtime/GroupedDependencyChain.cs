using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace AeLa.Utilities.SceneDeps
{
	public struct GroupedDependencyChain : IDisposable
	{
		private Dictionary<int, List<string>> groups;

		public int Count => groups.Count;

		internal GroupedDependencyChain(List<string> depsTopo, DependencyCache dependencyCache)
		{
			using var _ = DictionaryPool<string, int>.Get(out var phase);
			foreach (var scene in depsTopo)
			{
				var dependencies = dependencyCache.GetImmediateDependencies(scene);
				if (dependencies.Count == 0)
				{
					phase[scene] = 0;
					continue;
				}

				var maxDepth = 0;
				foreach (var dep in dependencies)
				{
					maxDepth = Math.Max(maxDepth, phase[dep]);
				}

				phase[scene] = maxDepth + 1;
			}

			groups = DictionaryPool<int, List<string>>.Get();
			foreach (var (scene, group) in phase)
			{
				if (!groups.TryGetValue(group, out var groupList))
				{
					groupList = ListPool<string>.Get();
					groups[group] = groupList;
				}

				groupList.Add(scene);
			}
		}

		public IReadOnlyList<string> GetGroup(int index)
		{
			return groups[index];
		}

		public void Dispose()
		{
			foreach (var list in groups.Values)
			{
				ListPool<string>.Release(list);
			}

			DictionaryPool<int, List<string>>.Release(groups);
			groups = null;
		}
	}
}