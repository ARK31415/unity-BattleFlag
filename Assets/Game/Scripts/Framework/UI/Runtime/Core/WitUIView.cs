using System;
using UnityEngine;

namespace Wit.Framework.UI
{
    /// <summary>
    /// 所有框架窗口的运行时基类，提供打开、关闭、交互状态和上下文接收等公开生命周期合同。
    /// 项目层窗口 prefab 必须挂载继承自 WitUIView 的组件。
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    [DisallowMultipleComponent]
    public class WitUIView : MonoBehaviour
    {
        /// <summary>打开该窗口时使用的配置 key。</summary>
        public string Key => _definition?.Key ?? string.Empty;

        /// <summary>该窗口对应的配置定义。</summary>
        public WitUIWindowDefinition Definition => _definition;

        /// <summary>当前是否处于打开状态。</summary>
        public bool IsOpen { get; private set; }

        /// <summary>打开或重开时传入的上下文对象，由项目层自行解析。</summary>
        public object Context { get; private set; }

        private WitUIWindowDefinition _definition;
        private CanvasGroup _canvasGroup;

        /// <summary>
        /// 由 UIManager 在实例化后调用，传入窗口定义和可选的上下文对象。
        /// </summary>
        public virtual void Open(string key, object context, WitUIWindowDefinition definition)
        {
            _definition = definition;
            Context = NormalizeContext(context);
            IsOpen = true;

            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();

            gameObject.SetActive(true);
            OnOpened(Context);
        }

        /// <summary>
        /// 由 UIManager 在复用已打开或缓存窗口时调用，只刷新业务上下文，不重新执行首次打开逻辑。
        /// </summary>
        public virtual void Reopen(object context)
        {
            Context = NormalizeContext(context);
            IsOpen = true;

            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();

            gameObject.SetActive(true);
            OnReopened(Context);
        }

        /// <summary>
        /// 由 UIManager 调用以关闭该窗口。
        /// 子类可重写以执行关闭前逻辑，但必须调用 base.Close()。
        /// </summary>
        public virtual void Close()
        {
            OnClosing();
            IsOpen = false;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 设置自身 CanvasGroup 的交互状态。
        /// </summary>
        public void SetFrameworkInteractable(bool interactable)
        {
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();

            _canvasGroup.interactable = interactable;
            _canvasGroup.blocksRaycasts = interactable;
            OnInteractableChanged(interactable);
        }

        /// <summary>
        /// 将调用方传入的 context 标准化为该 View 期望的形态。
        /// </summary>
        public virtual object NormalizeContext(object context) => context;

        /// <summary>
        /// 在 Open/Reopen 前校验 context 是否能被该 View 接收。
        /// </summary>
        public virtual bool CanAcceptContext(object context, out string error)
        {
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// 打开完成后调用，子类可在此接收 context 并初始化 UI 控件。
        /// </summary>
        protected virtual void OnOpened(object context) { }

        /// <summary>
        /// 窗口复用后调用，子类可在此刷新 context 驱动的显示数据。
        /// </summary>
        protected virtual void OnReopened(object context) { }

        /// <summary>
        /// 关闭前调用，子类可在此释放资源或取消订阅。
        /// </summary>
        protected virtual void OnClosing() { }

        /// <summary>
        /// 交互状态变化时调用。
        /// </summary>
        protected virtual void OnInteractableChanged(bool interactable) { }

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }
    }

    /// <summary>
    /// 无参数窗口的显式 Context。业务窗口不再把 null 作为标准无参语义。
    /// </summary>
    public sealed class WitEmptyContext
    {
        public static readonly WitEmptyContext Instance = new();

        private WitEmptyContext() { }
    }

    /// <summary>
    /// 正式业务窗口的强类型 Context 基类，负责在框架入口处完成 Context 类型校验。
    /// </summary>
    public class WitUIView<TContext> : WitUIView
    {
        public override object NormalizeContext(object context)
        {
            if (context == null && typeof(TContext) == typeof(WitEmptyContext))
                return WitEmptyContext.Instance;

            return context;
        }

        public override bool CanAcceptContext(object context, out string error)
        {
            object normalized = NormalizeContext(context);
            if (normalized is TContext)
            {
                error = string.Empty;
                return true;
            }

            string received = normalized == null ? "null" : normalized.GetType().Name;
            error = $"窗口 '{Key}' 需要 Context 类型 {typeof(TContext).Name}，实际收到 {received}。";
            return false;
        }

        protected sealed override void OnOpened(object context)
        {
            OnOpened((TContext)NormalizeContext(context));
        }

        protected sealed override void OnReopened(object context)
        {
            OnReopened((TContext)NormalizeContext(context));
        }

        /// <summary>
        /// 强类型窗口首次打开完成后调用，适合初始化、订阅事件和首次刷新。
        /// </summary>
        protected virtual void OnOpened(TContext context) { }

        /// <summary>
        /// 强类型窗口被重复打开或缓存复用后调用，适合刷新显示数据。
        /// </summary>
        protected virtual void OnReopened(TContext context) { }
    }
}
