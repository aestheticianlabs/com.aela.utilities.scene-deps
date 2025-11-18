using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace AeLa.Utilities.SceneDeps
{
	/// <summary>
	/// Provides all <see cref="ISceneDependencyProvider"/> assets marked as Addressable and
	/// with the label <see cref="DependencyListLabel"/>.
	/// </summary>
	public static class DependencyListsProvider
	{
		public const string DependencyListLabel = "SceneDependencyList";
		private static AsyncOperationHandle<IList<ISceneDependencyProvider>> assetsHandle;

		/// <summary>
		/// Loads all <see cref="ISceneDependencyProvider"/> assets marked as Addressable
		/// with the label <see cref="DependencyListLabel"/>.
		/// Results are cached, so Addressable loading will only happen if the handle is not already cached.
		/// </summary>
		/// <returns></returns>
		/// <exception cref="Exception"></exception>
		public static async UniTask<IList<ISceneDependencyProvider>> GetDependencyListsAsync()
		{
			if (!assetsHandle.IsValid())
			{
				assetsHandle = Addressables.LoadAssetsAsync<ISceneDependencyProvider>(DependencyListLabel);
			}

			if (!assetsHandle.IsDone)
			{
				await assetsHandle;
			}

			if (assetsHandle.Status == AsyncOperationStatus.Failed)
			{
				throw assetsHandle.OperationException;
			}

			return assetsHandle.Result;
		}

		/// <summary>
		/// Releases the <see cref="ISceneDependencyProvider"/> assets handle cached by <see cref="GetDependencyListsAsync"/>
		/// </summary>
		/// <exception cref="Exception"></exception>
		public static void ReleaseAssetHandle()
		{
			if (!assetsHandle.IsValid()) throw new("The assets handle is invalid.");
			if (assetsHandle.Status != AsyncOperationStatus.Succeeded) return;

			assetsHandle.Release();
		}
	}
}