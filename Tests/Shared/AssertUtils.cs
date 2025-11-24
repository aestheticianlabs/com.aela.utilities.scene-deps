using System;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.SceneManagement;

namespace AeLa.Utilities.SceneDeps.Tests.Shared
{
	public static class AssertUtils
	{
		public static async UniTask ThrowsException<TException>(this UniTask task) where TException : Exception
		{
			var caught = false;
			try
			{
				await task;
			}
			catch (TException)
			{
				caught = true;
			}

			Assert.IsTrue(caught, $"Did not catch {typeof(TException)}");
		}

		public static void IsLoaded(string scenePath)
		{
			var scene = SceneManager.GetSceneByPath(scenePath);
			Assert.IsTrue(scene.IsValid() && scene.isLoaded, $"{scenePath} not loaded");
		}
	}
}