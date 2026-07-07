using System;
using BF.Framework.Core.Events;
using UnityEngine;
using UnityEngine.Events;

namespace BF.Game.Runtime.Battle.Events
{
    /// <summary>
    /// 回合阶段枚举。
    /// </summary>
    public enum BFTurnPhase
    {
        None,
        TurnStarted,
        PlayerTurnStarted,
        EnemyTurnStarted,
        TurnEnded,
    }

    /// <summary>
    /// 回合事件数据。
    /// </summary>
    [Serializable]
    public struct BFTurnEventData
    {
        public BFTurnPhase Phase;
        public int TurnNumber;
        public int RoundNumber;
    }

    /// <summary>
    /// 回合事件通道。
    /// 广播回合开始、玩家回合、敌方回合、回合结束等阶段变化。
    /// </summary>
    [CreateAssetMenu(fileName = "BFTurnEventSO", menuName = "BF/Events/Turn Event SO")]
    public class BFTurnEventSO : BFBaseEventSO
    {
        [Serializable]
        private class BFTurnUnityEvent : UnityEvent<BFTurnEventData> { }

        [SerializeField] private BFTurnUnityEvent _onRaised = new();

        public void Register(UnityAction<BFTurnEventData> callback) => _onRaised.AddListener(callback);
        public void Unregister(UnityAction<BFTurnEventData> callback) => _onRaised.RemoveListener(callback);
        public void Raise(BFTurnEventData data) => _onRaised?.Invoke(data);

        protected override int GetListenerCount() =>
            _onRaised != null ? _onRaised.GetPersistentEventCount() : 0;

        public override void RemoveAllListeners() => _onRaised?.RemoveAllListeners();
    }
}
