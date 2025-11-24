using System;
using AeLa.Utilities.SceneTransition;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using UnityEngine;

namespace AeLa.Utilities.SceneDeps.SceneTransitionManagerSupport
{
	/// <summary>
	/// Add this to your initialization scene to add automagic
	/// scene dependency load/unload to AeLa Scene Transition Manager.
	/// </summary>
	[PublicAPI]
	// ReSharper disable once InconsistentNaming
	public class STM_SceneDependencyManager : MonoBehaviour
	{
		/// <summary>
		/// Whether the previous scene's dependencies should be unloaded if they're not in the next scene's dependency list.
		/// Disabling can be useful for intermediary transitions.
		/// </summary>
		public static bool UnloadUnusedDependencies = true;

		public static DisableUnloadUnusedScope DisableUnloadUnusedScoped() =>
			DisableUnloadUnusedScope.Create();

		protected virtual void Awake()
		{
			if (!Singleton<STM_SceneDependencyManager>.CheckInstance(this, true))
			{
				return;
			}

			Singleton<STM_SceneDependencyManager>.SetInstance(this);
			DontDestroyOnLoad(this);

			SceneTransitionManager.OnBeforeLoad += OnBeforeLoad;
			SceneTransitionManager.OnAfterUnload += OnAfterUnload;
		}

		private void OnDestroy()
		{
			SceneTransitionManager.OnBeforeLoad -= OnBeforeLoad;
			SceneTransitionManager.OnAfterUnload -= OnAfterUnload;
		}

		protected virtual void OnBeforeLoad(string scene)
		{
			LoadDependencies(scene).Forget();
		}

		protected virtual void OnAfterUnload(string scene)
		{
			UnloadPreviousDeps(scene).Forget();
		}

		protected virtual async UniTask LoadDependencies(string scene)
		{
			// start a blocking operation to wait for our dependencies to be loaded
			using (SceneTransitionManager.Instance.BlockingOperations.StartOperation())
			{
				await SceneDependencies.LoadDependenciesAsync(scene, unloadUnusedDependencies: false);
			}
		}

		protected virtual async UniTask UnloadPreviousDeps(string scene)
		{
			if (!UnloadUnusedDependencies) return;

			// start a blocking operation to wait for old dependencies to be unloaded
			using (SceneTransitionManager.Instance.BlockingOperations.StartOperation())
			{
				await SceneDependencies.UnloadUnusedDependenciesAsync();
			}
		}

		public struct DisableUnloadUnusedScope : IDisposable
		{
			public static DisableUnloadUnusedScope Create()
			{
				UnloadUnusedDependencies = false;
				return new();
			}

			public void Dispose()
			{
				UnloadUnusedDependencies = true;
			}
		}
	}
}