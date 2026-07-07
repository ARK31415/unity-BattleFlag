using System;
using BF.Framework.Core.Events;
using UnityEngine;
using UnityEngine.Events;

namespace BF.Game.Runtime.Battle.Events
{
    /// <summary>
    /// 单位事件数据。
    /// </summary>
    [Serializable]
    public struct BFUnitEventData
    {
        /// <summary>
        /// 触发事件的单位运行时 ID。
        /// </summary>
        public string UnitId;

        /// <summary>
        /// 事件类型：Moved, Attacked, Damaged, Killed, Selected, Deselected。
        /// </summary>
        public string EventType;

        /// <summary>
        /// 目标位置或目标单位 ID（可选）。
        /// </summary>
        public string TargetId;

        /// <summary>
        /// 数值参数（如伤害值、治疗值）。
        /// </summary>
        public int Value;
    }

    /// <summary>
    /// 单位事件通道。
    /// 广播单位移动、攻击、受伤、阵亡、选中等事件。
    /// </summary>
    [CreateAssetMenu(fileName = "BFUnitEventSO", menuName = "BF/Events/Unit Event SO")]
    public class BFUnitEventSO : BFBaseEventSO
    {
        [Serializable]
        private class BFUnitUnityEvent : UnityEvent<BFUnitEventData> { }

        [SerializeField] private BFUnitUnityEvent _onRaised = new();

        public void Register(UnityAction<BFUnitEventData> callback) => _onRaised.AddListener(callback);
        public void Unregister(UnityAction<BFUnitEventData> callback) => _onRaised.RemoveListener(callback);
        public void Raise(BFUnitEventData data) => _onRaised?.Invoke(data);

        protected override int GetListenerCount() =>
            _onRaised != null ? _onRaised.GetPersistentEventCount() : 0;

        public override void RemoveAllListeners() => _onRaised?.RemoveAllListeners();
    }
}
