using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace BF.Framework.Core.Events
{
    /// <summary>
    /// 事件通道基类（ScriptableObject 事件）。
    /// 订阅者通过 Register/Unregister 注册回调，触发者通过 Raise 发送事件。
    /// 不依赖全局 Singleton，所有通道以资产实例形式在 Inspector 中绑定。
    /// </summary>
    public abstract class BFBaseEventSO : ScriptableObject
    {
#if UNITY_EDITOR
        [TextArea(2, 4)]
        [SerializeField] private string _description = string.Empty;
#endif

        /// <summary>
        /// 已注册的监听器数量（调试用）。
        /// </summary>
        public int ListenerCount => GetListenerCount();

        protected abstract int GetListenerCount();

        /// <summary>
        /// 移除所有监听器。
        /// </summary>
        public abstract void RemoveAllListeners();
    }

    /// <summary>
    /// 无参事件通道。
    /// </summary>
    [CreateAssetMenu(fileName = "BFVoidEventSO", menuName = "BF/Events/Void Event SO")]
    public class BFVoidEventSO : BFBaseEventSO
    {
        private readonly UnityEvent _onRaised = new();

        public void Register(UnityAction callback) => _onRaised.AddListener(callback);
        public void Unregister(UnityAction callback) => _onRaised.RemoveListener(callback);
        public void Raise() => _onRaised?.Invoke();

        protected override int GetListenerCount() =>
            _onRaised != null ? _onRaised.GetPersistentEventCount() : 0;

        public override void RemoveAllListeners() => _onRaised?.RemoveAllListeners();
    }
}
