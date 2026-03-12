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
		private SceneDependencies.Handle currentHandle;
		private SceneDependencies.Handle previousHandle;

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
			// unlaod any dependencies we may still have loaded
			currentHandle?.Dispose();
			previousHandle?.Dispose();

			SceneTransitionManager.OnBeforeLoad -= OnBeforeLoad;
			SceneTransitionManager.OnAfterUnload -= OnAfterUnload;
		}

		protected virtual void OnBeforeLoad(string scene)
		{
			LoadDependencies(scene).Forget();
		}

		protected virtual void OnAfterUnload(string scene)
		{
			UnloadPreviousDeps().Forget();
		}

		protected virtual async UniTask LoadDependencies(string scene)
		{
			// start a blocking operation to wait for our dependencies to be loaded
			using (SceneTransitionManager.Instance.BlockingOperations.StartOperation())
			{
				if (previousHandle is { IsValid: true })
				{
					Debug.LogError("A previous scene's dependencies were still loaded. Unloading those first.");
					await previousHandle.ReleaseAsync();
					previousHandle = null;
				}

				previousHandle = currentHandle;
				currentHandle = await SceneDependencies.LoadDependenciesAsync(scene);
			}
		}

		protected virtual async UniTask UnloadPreviousDeps()
		{
			if (previousHandle is not { IsValid: true }) return;

			// start a blocking operation to wait for old dependencies to be unloaded
			using (SceneTransitionManager.Instance.BlockingOperations.StartOperation())
			{
				await previousHandle.ReleaseAsync();
				previousHandle = null;
			}
		}
	}
}