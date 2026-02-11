using System.Text.RegularExpressions;
using UnityEngine;

namespace AeLa.Utilities.SceneDeps
{
	/// <summary>
	/// Assigns dependencies for scenes matching a regex pattern.
	/// </summary>
	[CreateAssetMenu(menuName = "Scene Dependencies/Regex Scene Dependency List")]
	public class RegexSceneDependencyList : SceneDependencyList
	{
		/// <summary>
		/// Regex used to match scenes by path to this list.
		/// </summary>
		[Tooltip("Regex used to match scenes by path to this list.")]
		public string MatchScenes;

		protected override bool CheckMatchesScene(string scenePath)
		{
			return Regex.IsMatch(scenePath, MatchScenes);
		}
	}
}