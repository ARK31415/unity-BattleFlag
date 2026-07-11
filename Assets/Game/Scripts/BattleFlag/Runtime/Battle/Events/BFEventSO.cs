using UnityEngine;
using UnityEngine.Events;

namespace BF.Game.Runtime.Battle.Events
{
    /// <summary>
    /// 事件通道基类（ScriptableObject 事件），内联到战斗层以消除对框架层的依赖。
    /// 订阅者通过 Register/Unregister 注册回调，触发者通过 Raise 发送事件。
    /// </summary>
    public abstract class BFEventSO : ScriptableObject
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
}
