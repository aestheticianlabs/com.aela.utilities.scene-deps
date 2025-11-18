using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace AeLa.Utilities.SceneDeps
{
	/// <summary>
	/// Organizes scene dependencies into load phase groups.
	/// Each group is guaranteed to be safe to load
	/// as long as any groups with a lower index have already been loaded.
	/// </summary>
	public struct GroupedDependencyChain : IDisposable
	{
		public static readonly GroupedDependencyChain Empty = default;

		private Dictionary<int, List<string>> groups;

		/// <summary>
		/// Returns the number of groups in the chain.
		/// </summary>
		public int Count => groups?.Count ?? 0;

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

		/// <summary>
		/// Returns a list of scene paths for the provided group index.
		/// </summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public IReadOnlyList<string> GetGroup(int index)
		{
			return groups[index];
		}

		public void Dispose()
		{
			if (groups == null) return;

			foreach (var list in groups.Values)
			{
				ListPool<string>.Release(list);
			}

			DictionaryPool<int, List<string>>.Release(groups);
			groups = null;
		}
	}
}