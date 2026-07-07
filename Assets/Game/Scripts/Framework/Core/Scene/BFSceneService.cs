using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BF.Framework.Core.Scene
{
    /// <summary>
    /// 统一场景加载服务。
    /// 通过 GameSceneSO 资产驱动场景切换，替代裸字符串场景名。
    /// 第一版使用 Unity SceneManager API，后续可接入 Addressables。
    /// </summary>
    public class BFSceneService
    {
        /// <summary>
        /// 最新活跃的场景 SO。
        /// </summary>
        public GameSceneSO ActiveScene { get; private set; }

        /// <summary>
        /// 通过 GameSceneSO 资产异步加载场景（推荐）。
        /// </summary>
        public async Task<AsyncOperation> LoadSceneAsync(GameSceneSO scene)
        {
            if (scene == null)
            {
                Debug.LogError("[BFSceneService] LoadSceneAsync: scene 参数为 null。");
                return null;
            }

            if (string.IsNullOrEmpty(scene.ScenePath))
            {
                Debug.LogError($"[BFSceneService] LoadSceneAsync: GameSceneSO [{scene.name}] 的场景路径为空。请确保已在 Inspector 中拖入场景资产。");
                return null;
            }

            LoadSceneMode loadMode = scene.Mode == GameSceneSO.LoadMode.Additive
                ? LoadSceneMode.Additive
                : LoadSceneMode.Single;

            Debug.Log($"[BFSceneService] 开始加载场景: {scene.SceneName} (模式: {loadMode})");

            AsyncOperation operation = SceneManager.LoadSceneAsync(scene.ScenePath, loadMode);

            if (operation == null)
            {
                Debug.LogError($"[BFSceneService] SceneManager.LoadSceneAsync 返回 null，场景路径: {scene.ScenePath}");
                return null;
            }

            operation.allowSceneActivation = false;

            while (operation.progress < 0.9f)
            {
                await Task.Yield();
            }

            operation.allowSceneActivation = true;

            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (scene.SetActiveOnLoad)
            {
                var loadedScene = SceneManager.GetSceneByPath(scene.ScenePath);
                if (loadedScene.IsValid())
                {
                    SceneManager.SetActiveScene(loadedScene);
                }
            }

            ActiveScene = scene;
            Debug.Log($"[BFSceneService] 场景加载完成: {scene.SceneName}");
            return operation;
        }

        /// <summary>
        /// 通过场景名称异步加载场景（向后兼容，已弃用）。
        /// </summary>
        [Obsolete("推荐使用 LoadSceneAsync(GameSceneSO scene) 重载版本。")]
        public async Task<AsyncOperation> LoadSceneAsync(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[BFSceneService] LoadSceneAsync: sceneName 为空。");
                return null;
            }

            Debug.Log($"[BFSceneService] 开始加载场景（已弃用 API）: {sceneName}");

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

            if (operation == null)
            {
                Debug.LogError($"[BFSceneService] SceneManager.LoadSceneAsync 返回 null，场景名: {sceneName}");
                return null;
            }

            operation.allowSceneActivation = false;

            while (operation.progress < 0.9f)
            {
                await Task.Yield();
            }

            operation.allowSceneActivation = true;

            while (!operation.isDone)
            {
                await Task.Yield();
            }

            Debug.Log($"[BFSceneService] 场景加载完成: {sceneName}");
            return operation;
        }
    }
}
