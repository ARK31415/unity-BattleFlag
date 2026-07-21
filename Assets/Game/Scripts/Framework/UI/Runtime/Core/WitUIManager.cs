using System.Collections.Generic;
using UnityEngine;

namespace Wit.Framework.UI
{
    /// <summary>
    /// 通用 UGUI 窗口管理器，负责按 key 打开、关闭和返回 UI 窗口。
    /// 通过分层多栈管理 Screen 和 Popup，HUD/Overlay/Toast 不进入返回栈。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WitUIManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private WitUIConfig _config;
        [SerializeField] private WitUIRoot _root;

        private readonly List<WitUIView> _screenStack = new();
        private readonly List<WitUIView> _popupStack = new();
        private readonly Dictionary<string, WitUIView> _openViewsByKey = new();
        private readonly Dictionary<string, WitUIView> _cachedViewsByKey = new();
        private IUIInputCoordinator _inputCoordinator;

        /// <summary>当前处于打开状态的窗口数量。</summary>
        public int OpenViewCount => _openViewsByKey.Count;

        /// <summary>Screen 栈深度。</summary>
        public int ScreenStackCount => _screenStack.Count;

        /// <summary>Popup 栈深度。</summary>
        public int PopupStackCount => _popupStack.Count;

        private void Awake()
        {
            if (_root == null)
                _root = GetComponent<WitUIRoot>();
        }

        /// <summary>
        /// 配置 UIManager 的配置资产、UI 层级根节点和可选的输入协调器。
        /// </summary>
        public void Configure(WitUIConfig config, WitUIRoot root, IUIInputCoordinator inputCoordinator = null)
        {
            _config = config;
            _root = root;
            _inputCoordinator = inputCoordinator;
        }

        /// <summary>
        /// 按窗口 key 打开一个 UI 窗口。
        /// </summary>
        /// <param name="key">配置中注册的窗口 key。</param>
        /// <param name="context">可选的上下文对象，将传递给窗口 View。</param>
        /// <returns>打开结果，成功时包含已打开的 View 引用。</returns>
        public WitUIOpenResult Open(string key, object context = null)
        {
            if (_config == null)
                return WitUIOpenResult.Failure("WitUIConfig 未配置。");
            if (_root == null)
                return WitUIOpenResult.Failure("WitUIRoot 未配置。");
            if (!_config.TryGetWindow(key, out WitUIWindowDefinition definition))
                return WitUIOpenResult.Failure($"未找到窗口 key: {key}");
            if (definition.Prefab == null)
                return WitUIOpenResult.Failure($"窗口 key '{key}' 的 Prefab 为空。");
            if (_root.GetLayerRoot(definition.Layer) == null)
                return WitUIOpenResult.Failure($"层级 '{definition.Layer}' 的 Transform 未配置。");

            // 唯一窗口已打开时直接返回已有实例
            if (definition.Unique && _openViewsByKey.TryGetValue(key, out WitUIView existing))
                return WitUIOpenResult.Failure($"窗口 '{key}' 已打开且配置为唯一窗口。");

            // 尝试从缓存复用
            WitUIView view;
            bool fromCache = _cachedViewsByKey.TryGetValue(key, out view);
            if (fromCache)
            {
                _cachedViewsByKey.Remove(key);
            }
            else
            {
                var instance = Instantiate(definition.Prefab, _root.GetLayerRoot(definition.Layer));
                view = instance.GetComponent<WitUIView>();
                if (view == null)
                {
                    Destroy(instance);
                    return WitUIOpenResult.Failure($"窗口 key '{key}' 的 Prefab 上未挂载 WitUIView 组件。");
                }
            }

            view.Open(key, context, definition);
            _openViewsByKey[key] = view;

            AddToStack(view, definition.Layer);
            UpdateModalBlocker();
            NotifyInputCoordinator();

            return WitUIOpenResult.Success(view);
        }

        /// <summary>
        /// 按 key 关闭指定窗口。
        /// </summary>
        public bool Close(string key)
        {
            if (!_openViewsByKey.TryGetValue(key, out WitUIView view))
                return false;

            RemoveFromStack(view, view.Definition.Layer);
            _openViewsByKey.Remove(key);

            ApplyCacheOrDestroy(view, key);
            UpdateModalBlocker();
            NotifyInputCoordinator();
            return true;
        }

        /// <summary>
        /// 执行返回逻辑：优先关闭顶层 Popup，没有 Popup 时关闭顶层 Screen。
        /// </summary>
        public bool Back()
        {
            if (_popupStack.Count > 0)
            {
                var top = _popupStack[^1];
                return Close(top.Key);
            }
            if (_screenStack.Count > 0)
            {
                var top = _screenStack[^1];
                return Close(top.Key);
            }
            return false;
        }

        /// <summary>
        /// 查询指定 key 的窗口是否已打开。
        /// </summary>
        public bool TryGetOpenView(string key, out WitUIView view)
        {
            return _openViewsByKey.TryGetValue(key, out view);
        }

        private void AddToStack(WitUIView view, WitUILayer layer)
        {
            WitUIView currentTop = null;

            switch (layer)
            {
                case WitUILayer.Screen:
                    currentTop = _screenStack.Count > 0 ? _screenStack[^1] : null;
                    _screenStack.Add(view);
                    break;
                case WitUILayer.Popup:
                    currentTop = _popupStack.Count > 0 ? _popupStack[^1] : null;
                    _popupStack.Add(view);
                    break;
                // HUD, Overlay, Toast 不进入返回栈
            }

            currentTop?.SetFrameworkInteractable(false);
            view.SetFrameworkInteractable(true);
        }

        private void RemoveFromStack(WitUIView view, WitUILayer layer)
        {
            switch (layer)
            {
                case WitUILayer.Screen:
                    _screenStack.Remove(view);
                    if (_screenStack.Count > 0)
                        _screenStack[^1].SetFrameworkInteractable(true);
                    break;
                case WitUILayer.Popup:
                    _popupStack.Remove(view);
                    if (_popupStack.Count > 0)
                        _popupStack[^1].SetFrameworkInteractable(true);
                    break;
            }
        }

        private void ApplyCacheOrDestroy(WitUIView view, string key)
        {
            switch (view.Definition.CachePolicy)
            {
                case WitUICachePolicy.Permanent:
                case WitUICachePolicy.CacheOnClose:
                    view.Close();
                    _cachedViewsByKey[key] = view;
                    break;
                case WitUICachePolicy.DestroyOnClose:
                default:
                    view.Close();
                    _cachedViewsByKey.Remove(key);
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        DestroyImmediate(view.gameObject);
                    else
                        Destroy(view.gameObject);
#else
                    Destroy(view.gameObject);
#endif
                    break;
            }
        }

        private void UpdateModalBlocker()
        {
            if (_root.ModalBlocker == null) return;

            bool hasModal = _popupStack.Count > 0 && _popupStack[^1].Definition.Modal;
            var blocker = _root.ModalBlocker;
            blocker.alpha = hasModal ? 0.5f : 0f;
            blocker.interactable = hasModal;
            blocker.blocksRaycasts = hasModal;
        }

        private void NotifyInputCoordinator()
        {
            bool hasBlockingUI = _popupStack.Count > 0;
            bool hasAnyUI = _openViewsByKey.Count > 0;
            _inputCoordinator?.OnUIStateChanged(hasBlockingUI, hasAnyUI);
        }
    }
}
