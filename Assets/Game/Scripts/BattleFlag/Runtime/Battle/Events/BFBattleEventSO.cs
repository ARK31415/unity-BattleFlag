using System;
using BF.Framework.Core.Events;
using UnityEngine;
using UnityEngine.Events;

namespace BF.Game.Runtime.Battle.Events
{
    /// <summary>
    /// 战斗事件类型。
    /// </summary>
    public enum BFBattleEventType
    {
        None,
        BattleStarted,
        BattleEnded,
        Victory,
        Defeat
    }

    /// <summary>
    /// 战斗事件数据。
    /// </summary>
    [Serializable]
    public struct BFBattleEventData
    {
        public BFBattleEventType EventType;
        public string BattleId;
        public string WinnerFaction;
    }

    /// <summary>
    /// 战斗事件通道。
    /// 广播战斗开始、结束、胜利、失败等事件。
    /// </summary>
    [CreateAssetMenu(fileName = "BFBattleEventSO", menuName = "BF/Events/Battle Event SO")]
    public class BFBattleEventSO : BFBaseEventSO
    {
        [Serializable]
        private class BFBattleUnityEvent : UnityEvent<BFBattleEventData> { }

        [SerializeField] private BFBattleUnityEvent _onRaised = new();

        public void Register(UnityAction<BFBattleEventData> callback) => _onRaised.AddListener(callback);
        public void Unregister(UnityAction<BFBattleEventData> callback) => _onRaised.RemoveListener(callback);
        public void Raise(BFBattleEventData data) => _onRaised?.Invoke(data);

        protected override int GetListenerCount() =>
            _onRaised != null ? _onRaised.GetPersistentEventCount() : 0;

        public override void RemoveAllListeners() => _onRaised?.RemoveAllListeners();
    }
}
