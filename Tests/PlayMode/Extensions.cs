using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AeLa.Utilities.SceneDeps.Tests.PlayMode
{
	public static class Extensions
	{
		public static IEnumerator ToTestCoroutine(this UniTask task)
		{
			return task.ToCoroutine(e =>
				{
					Debug.LogException(e);
					throw e;
				}
			);
		}
	}
}