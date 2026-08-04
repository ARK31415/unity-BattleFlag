using System;
using System.Collections.Generic;
using BF.Game.Battle.Domain.Events;
using BF.Game.Runtime.Battle;

namespace BF.Game.Runtime.Battle.Events
{
    /// <summary>
    /// 将本场战斗的领域事件单向转发到现有 ScriptableObject 事件通道。
    ///
    /// 适配器只负责协议转换，不修改规则状态，也不把 SO 事件反向接回战斗总线。
    /// 它属于 Unity 运行时适配边界，不是领域事件总线本身。
    /// </summary>
    public sealed class BFBattleEventToSOAdapter : IDisposable
    {
        private readonly List<IDisposable> _subscriptions = new();
        private bool _isDisposed;

        /// <summary>
        /// 订阅本场战斗的五类领域事件，并将其转换为旧 SO 事件数据。
        ///
        /// 订阅回调只捕获传入的 SO 通道引用；所有订阅令牌由适配器持有，
        /// 适配器释放时会统一解除这些回调，避免闭包继续持有 Unity 对象引用。
        /// </summary>
        /// <param name="session">提供战斗事件的当前战斗会话。</param>
        /// <param name="battleEventChannel">战斗结果和开始事件的旧 SO 通道，可为空。</param>
        /// <param name="turnEventChannel">回合阶段事件的旧 SO 通道，可为空。</param>
        /// <param name="unitEventChannel">单位攻击和击败事件的旧 SO 通道，可为空。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="session" /> 为空时抛出。</exception>
        public BFBattleEventToSOAdapter(
            BFBattleSession session,
            BFBattleEventSO battleEventChannel,
            BFTurnEventSO turnEventChannel,
            BFUnitEventSO unitEventChannel)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));

            _subscriptions.Add(session.Subscribe<BFBattleStartedEvent>(eventData =>
                RaiseBattleStarted(battleEventChannel, eventData)));
            _subscriptions.Add(session.Subscribe<BFBattlePhaseChangedEvent>(eventData =>
                RaisePhaseChanged(turnEventChannel, eventData)));
            _subscriptions.Add(session.Subscribe<BFAttackResolvedEvent>(eventData =>
                RaiseAttackResolved(unitEventChannel, eventData)));
            _subscriptions.Add(session.Subscribe<BFUnitDefeatedEvent>(eventData =>
                RaiseUnitDefeated(unitEventChannel, eventData)));
            _subscriptions.Add(session.Subscribe<BFBattleCompletedEvent>(eventData =>
                RaiseBattleCompleted(battleEventChannel, eventData)));
        }

        /// <summary>
        /// 解除适配器创建的全部领域事件订阅。
        ///
        /// 释放操作幂等；它不会清理传入的 SO 通道，也不会释放所属战斗会话。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            for (var index = _subscriptions.Count - 1; index >= 0; index--)
            {
                _subscriptions[index].Dispose();
            }

            _subscriptions.Clear();
        }

        /// <summary>
        /// 将战斗开始领域事件转换为旧战斗 SO 事件。
        /// </summary>
        /// <param name="channel">目标 SO 通道。</param>
        /// <param name="eventData">战斗开始领域事件数据。</param>
        private static void RaiseBattleStarted(BFBattleEventSO channel, BFBattleStartedEvent eventData)
        {
            channel?.Raise(new BFBattleEventData
            {
                EventType = BFBattleEventType.BattleStarted,
                BattleId = eventData.BattleId,
                WinnerFaction = string.Empty
            });
        }

        /// <summary>
        /// 将领域阶段变化转换为旧回合 SO 阶段事件。
        /// </summary>
        /// <param name="channel">目标 SO 通道。</param>
        /// <param name="eventData">阶段变化领域事件数据。</param>
        private static void RaisePhaseChanged(BFTurnEventSO channel, BFBattlePhaseChangedEvent eventData)
        {
            if (channel == null) return;

            var phase = eventData.CurrentPhase switch
            {
                BFBattlePhase.PlayerTurn => BFTurnPhase.PlayerTurnStarted,
                BFBattlePhase.EnemyTurn => BFTurnPhase.EnemyTurnStarted,
                _ => BFTurnPhase.None
            };

            if (phase == BFTurnPhase.None) return;

            channel.Raise(new BFTurnEventData
            {
                Phase = phase,
                TurnNumber = eventData.TurnNumber,
                RoundNumber = eventData.RoundNumber
            });
        }

        /// <summary>
        /// 将攻击结算领域事件转换为旧单位 SO 受伤事件。
        /// </summary>
        /// <param name="channel">目标 SO 通道。</param>
        /// <param name="eventData">攻击结算领域事件数据。</param>
        private static void RaiseAttackResolved(BFUnitEventSO channel, BFAttackResolvedEvent eventData)
        {
            channel?.Raise(new BFUnitEventData
            {
                EventType = "Damaged",
                UnitId = eventData.TargetId,
                // 兼容旧 SO 合同：TargetId 实际保存攻击者 ID。
                TargetId = eventData.AttackerId,
                Value = eventData.FinalDamage
            });
        }

        /// <summary>
        /// 将通用单位击败领域事实转换为旧单位 SO 击败事件。
        /// </summary>
        /// <param name="channel">目标 SO 通道。</param>
        /// <param name="eventData">单位击败领域事件数据。</param>
        private static void RaiseUnitDefeated(BFUnitEventSO channel, BFUnitDefeatedEvent eventData)
        {
            channel?.Raise(new BFUnitEventData
            {
                EventType = "Killed",
                UnitId = eventData.UnitId
            });
        }

        /// <summary>
        /// 将战斗完成领域事件转换为旧战斗 SO 胜负事件。
        /// </summary>
        /// <param name="channel">目标 SO 通道。</param>
        /// <param name="eventData">战斗完成领域事件数据。</param>
        private static void RaiseBattleCompleted(BFBattleEventSO channel, BFBattleCompletedEvent eventData)
        {
            if (channel == null) return;

            var eventType = eventData.WinnerFaction == BFUnitFaction.Player
                ? BFBattleEventType.Victory
                : BFBattleEventType.Defeat;

            channel.Raise(new BFBattleEventData
            {
                EventType = eventType,
                BattleId = eventData.BattleId,
                WinnerFaction = eventData.WinnerFaction.ToString()
            });
        }
    }
}
