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

        /// <summary>打开时传入的上下文对象，由项目层自行解析。</summary>
        public object Context { get; private set; }

        private WitUIWindowDefinition _definition;
        private CanvasGroup _canvasGroup;

        /// <summary>
        /// 由 UIManager 在实例化后调用，传入窗口定义和可选的上下文对象。
        /// </summary>
        public virtual void Open(string key, object context, WitUIWindowDefinition definition)
        {
            _definition = definition;
            Context = context;
            IsOpen = true;

            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();

            gameObject.SetActive(true);
            OnOpened(context);
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
        /// 打开完成后调用，子类可在此接收 context 并初始化 UI 控件。
        /// </summary>
        protected virtual void OnOpened(object context) { }

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
}
