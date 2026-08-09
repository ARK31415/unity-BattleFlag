using System;
using BF.Game.Battle.Domain;
using BF.Game.Battle.Domain.Events;
using BF.Game.Battle.Rules.Battle;
using BF.Game.Runtime.Battle.Input;
using BF.Game.Runtime.Battle.Managers;
using BF.Game.Runtime.Battle.Units;
using UnityEngine;
using DomainBattleSession = BF.Game.Battle.Domain.BFBattleSession;
using DomainBattleResult = BF.Game.Battle.Domain.BattleResult;
using RuntimeBattleResult = BF.Game.Runtime.Battle.BattleResult;

namespace BF.Game.Runtime.Battle.Flow
{
    /// <summary>
    /// 战斗胜负流程协调器。
    ///
    /// 只从 BattleSession.Context 的规则状态读取存活单位，负责把已确定的胜负
    /// 交给 BFBattleProgressRules 完成；不维护 Runtime 生存状态的第二份副本。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BFBattleOutcomeCoordinator : MonoBehaviour
    {
        [SerializeField] private BFBattleTurnManager _turnManager;

        private DomainBattleSession _battleSession;
        private BFBattleProgressRules _battleProgressRules;
        private BFBattleSelectionController _selectionController;
        /// <summary>当前规则结果的表现投影；不在适配层保存第二份结果状态。</summary>
        public RuntimeBattleResult Result => ToRuntimeResult(_battleSession?.Context.Result);

        /// <summary>战斗结果确定时触发，供过渡门面转发旧 C# 回调。</summary>
        public event Action<RuntimeBattleResult> BattleEnded;

        /// <summary>绑定胜负流程所需的规则会话和阶段依赖。</summary>
        public void SetDependencies(
            BFBattleTurnManager turnManager,
            DomainBattleSession battleSession)
        {
            SetDependencies(turnManager, battleSession, null);
        }

        /// <summary>绑定胜负判定所需的规则会话和临时选择状态。</summary>
        public void SetDependencies(
            BFBattleTurnManager turnManager,
            DomainBattleSession battleSession,
            BFBattleSelectionController selectionController)
        {
            _turnManager = turnManager;
            _battleSession = battleSession;
            _selectionController = selectionController;
            _battleProgressRules = battleSession == null
                ? null
                : new BFBattleProgressRules(battleSession);

        }

        /// <summary>根据当前规则状态评估并完成战斗。</summary>
        public void Evaluate()
        {
            if (_battleSession == null || _battleProgressRules == null)
            {
                // 过渡门面原有测试和外部诊断使用该稳定前缀；职责已由本协调器实际执行。
                Debug.LogWarning("[BFBattleOutcomeCoordinator] Cannot evaluate battle end without a BattleSession.");
                return;
            }
            if (_battleSession.State != BFBattleSessionState.Running ||
                _battleSession.Context.Result != null)
                return;

            var playerAlive = false;
            var enemyAlive = false;
            foreach (var unit in _battleSession.Context.Units.Values)
            {
                if (unit == null || !unit.IsAlive)
                    continue;

                if (unit.Faction == BFUnitFaction.Player)
                    playerAlive = true;
                else if (unit.Faction == BFUnitFaction.Enemy)
                    enemyAlive = true;
            }

            if (playerAlive && enemyAlive)
                return;

            var battleId = _battleSession.Context.BattleId;
            var totalTurns = _battleSession.Context.TurnNumber;
            _turnManager?.TransitionToResolution();

            var domainResult = playerAlive
                ? DomainBattleResult.Victory(battleId, totalTurns)
                : DomainBattleResult.Defeat(battleId, totalTurns);
            _battleProgressRules.CompleteBattle(domainResult);
            // 选择是表现/输入临时状态，不属于已完成的战斗；即使行动锁尚未释放也必须清理。
            _selectionController?.ClearSelection();
            BattleEnded?.Invoke(Result);
        }

        private static RuntimeBattleResult ToRuntimeResult(DomainBattleResult result)
        {
            if (result == null || !result.HasResult)
                return null;

            return result.WinnerFaction == BFUnitFaction.Player
                ? RuntimeBattleResult.Victory(result.BattleId, result.TotalTurns)
                : RuntimeBattleResult.Defeat(result.BattleId, result.TotalTurns);
        }
    }
}
