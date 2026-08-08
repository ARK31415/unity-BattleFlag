using System.Collections;
using System.Collections.Generic;
using BF.Game.Battle.Domain;
using BF.Game.Battle.Domain.Events;
using BF.Game.Battle.Domain.Units;
using BF.Game.Battle.Rules.Units;
using BF.Game.Runtime.Battle.Commands;
using BF.Game.Runtime.Battle.Managers;
using BF.Game.Runtime.Battle.Units;
using UnityEngine;
using DomainBattleSession = BF.Game.Battle.Domain.BFBattleSession;
using DomainSessionState = BF.Game.Battle.Domain.BFBattleSessionState;

namespace BF.Game.Runtime.Battle.Flow
{
    /// <summary>
    /// 战斗行动统一协调器。
    ///
    /// 玩家输入和敌方 AI 都通过该组件进入 Move、Attack、Wait 流程；
    /// 规则校验和结算仍由 Rules 负责，本组件只协调适配层流程和生命周期。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BFBattleActionCoordinator : MonoBehaviour
    {
        [SerializeField] private BFBattleUnitManager _unitManager;
        [SerializeField] private BFBattleMovementCoordinator _movementCoordinator;
        [SerializeField] private BFBattleBoardManager _boardManager;
        [SerializeField] private BFBattleResolutionManager _resolutionManager;
        [SerializeField] private BFBattleTurnManager _turnManager;

        private DomainBattleSession _battleSession;
        private BFUnitStateRules _unitStateRules;
        private Coroutine _activeAttackWait;
        private UnitRuntime _activeAttackUnit;

        /// <summary>当前是否存在正在执行的战斗行动。</summary>
        public bool IsActionLocked => _unitManager != null && _unitManager.IsActionLocked;

        /// <summary>绑定统一行动协调器及其适配依赖。</summary>
        public void SetDependencies(
            BFBattleUnitManager unitManager,
            BFBattleMovementCoordinator movementCoordinator,
            BFBattleResolutionManager resolutionManager,
            BFBattleTurnManager turnManager,
            BFBattleBoardManager boardManager)
        {
            _unitManager = unitManager;
            _movementCoordinator = movementCoordinator;
            _resolutionManager = resolutionManager;
            _turnManager = turnManager;
            _boardManager = boardManager;
            _resolutionManager?.SetActionCoordinator(this);
        }

        /// <summary>查询指定单位当前规则 AP 内可达的表现格。</summary>
        public List<Vector2Int> GetReachableCellsForUnit(UnitRuntime unit)
        {
            if (_boardManager == null || !_unitManager.IsCurrentSessionUnit(unit))
                return new List<Vector2Int>();

            var position = unit.RuleState.GridPosition;
            return _boardManager.GetReachableCells(
                new Vector2Int(position.X, position.Y),
                unit.RuleState.Attributes.RemainingActionPoints,
                unit.RuntimeId);
        }

        /// <summary>查询当前选中玩家单位可攻击的敌方表现对象。</summary>
        public List<UnitRuntime> GetAttackableTargets()
        {
            var targets = new List<UnitRuntime>();
            var selected = _unitManager?.SelectedUnit;
            if (selected == null || !_unitManager.IsCurrentSessionUnit(selected))
                return targets;

            var position = selected.RuleState.GridPosition;
            var range = selected.RuleState.Attributes.EffectiveAttackRange;
            foreach (var unit in _unitManager.GetAliveUnitsByFaction(UnitFaction.Enemy))
            {
                if (unit == selected) continue;
                if (GetManhattanDistance(position, unit.RuleState.GridPosition) <= range)
                    targets.Add(unit);
            }

            return targets;
        }

        /// <summary>查询玩家是否仍有至少一个合法移动或攻击意图。</summary>
        public bool PlayerHasLegalAction()
        {
            if (_boardManager == null || _unitManager == null)
                return false;

            var players = _unitManager.GetAliveUnitsByFaction(UnitFaction.Player);
            var enemies = _unitManager.GetAliveUnitsByFaction(UnitFaction.Enemy);
            foreach (var unit in players)
            {
                var attributes = unit.RuleState.Attributes;
                if (attributes.RemainingActionPoints <= 0) continue;

                if (GetReachableCellsForUnit(unit).Count > 0)
                    return true;
                if (attributes.RemainingActionPoints < attributes.EffectiveAttackCost)
                    continue;

                foreach (var enemy in enemies)
                {
                    if (GetManhattanDistance(unit.RuleState.GridPosition, enemy.RuleState.GridPosition) <=
                        attributes.EffectiveAttackRange)
                        return true;
                }
            }

            return false;
        }

        /// <summary>兼容 EditMode 装配测试的单位门面注入入口。</summary>
        public void SetUnitManager(BFBattleUnitManager unitManager)
        {
            _unitManager = unitManager;
        }

        /// <summary>绑定当前战斗会话的规则行动入口。</summary>
        public void SetBattleSession(DomainBattleSession session)
        {
            _battleSession = session;
            _unitStateRules = session == null ? null : new BFUnitStateRules(session.Context);
        }

        /// <summary>
        /// 重置当前会话内所有存活单位的回合资源。
        /// 规则写入由本协调器持有的规则入口完成，门面不再直接修改规则状态。
        /// </summary>
        public void ResetAllUnitsForNewTurn()
        {
            if (_unitManager == null || _unitStateRules == null ||
                _battleSession == null || _battleSession.State != DomainSessionState.Running)
                return;

            foreach (var unit in _unitManager.AllUnits)
            {
                if (!_unitManager.IsCurrentSessionUnit(unit))
                    continue;

                if (_unitStateRules.TryResetTurnResources(unit.RuntimeId))
                    unit.RefreshRuleStateProjection();
            }
        }

        /// <summary>
        /// 清理所有尚未提交的行动表现；已经提交的规则结果不回滚。
        /// </summary>
        public void CleanupInterruptedActions()
        {
            CleanupInterruptedAttack();

            foreach (var unit in _unitManager?.AllUnits ?? new List<UnitRuntime>())
            {
                if (unit == null) continue;

                unit.Combat.ClearQueuedAttack();
                _resolutionManager?.ClearPendingAttack(unit);
                if (_unitStateRules != null && _unitManager.IsCurrentSessionUnit(unit) &&
                    unit.RuleState.ActionState == BFUnit_ActionState.Attack &&
                    _unitStateRules.TryChangeActionState(unit.RuntimeId, BFUnit_ActionState.Idle))
                {
                    unit.RefreshRuleStateProjection();
                }
            }

            SetActionLocked(false);
        }

        /// <summary>设置由协调器拥有的表现行动锁。</summary>
        public void SetActionLocked(bool value)
        {
            _unitManager?.SetActionLockedForCoordinator(value);
        }

        /// <summary>
        /// 清理攻击等待协程和未提交的攻击表现。
        /// 已经由 Rules 提交的攻击结果不会在这里回滚。
        /// </summary>
        public void CleanupInterruptedAttack()
        {
            if (_activeAttackWait != null)
            {
                StopCoroutine(_activeAttackWait);
                _activeAttackWait = null;
            }

            var unit = _activeAttackUnit;
            _activeAttackUnit = null;
            if (unit != null)
            {
                _resolutionManager?.ClearPendingAttack(unit);
                HandleAttackResolutionFailed(unit);
            }
        }

        /// <summary>将当前选择提交为移动行动。</summary>
        public bool TryMoveSelected(Vector2Int targetCell)
        {
            if (_unitManager == null || _movementCoordinator == null ||
                !CanPlayerAct(_unitManager.SelectedUnit))
                return false;

            return _movementCoordinator.TryMove(
                _unitManager.SelectedUnit,
                targetCell,
                refreshPlayerLegalActions: true,
                clearSelectionWhenActed: true);
        }

        /// <summary>将当前选择提交为攻击行动。</summary>
        public bool TryAttackSelected(UnitRuntime target)
        {
            if (_unitManager == null || !CanPlayerAct(_unitManager.SelectedUnit) ||
                target == null || !_unitManager.IsCurrentSessionUnit(target) || !target.RuleState.IsAlive ||
                target.Identity.Faction == _unitManager.SelectedUnit.Identity.Faction)
                return false;

            return TryAttackInternal(_unitManager.SelectedUnit, target);
        }

        /// <summary>将当前选择提交为等待行动。</summary>
        public bool TryWaitSelected()
        {
            if (_unitManager == null || !CanPlayerAct(_unitManager.SelectedUnit))
                return false;

            var unit = _unitManager.SelectedUnit;
            if (!TryWaitInternal(unit))
                return false;

            _unitManager.DeselectUnitIgnoringLockForCoordinator();
            _turnManager?.RefreshPlayerLegalActions();
            return true;
        }

        /// <summary>提交一个指定敌方单位的移动意图，供敌方 AI 使用。</summary>
        public bool TryMove(UnitRuntime unit, Vector2Int targetCell)
        {
            if (_unitManager == null || _movementCoordinator == null || !CanEnemyAct(unit))
                return false;

            return _movementCoordinator.TryMove(
                unit,
                targetCell,
                refreshPlayerLegalActions: false,
                clearSelectionWhenActed: false);
        }

        /// <summary>提交一个指定敌方单位的攻击意图，供敌方 AI 使用。</summary>
        public bool TryAttack(UnitRuntime attacker, UnitRuntime target)
        {
            return _unitManager != null && CanEnemyAct(attacker) &&
                   target != null && _unitManager.IsCurrentSessionUnit(target) && target.RuleState.IsAlive &&
                   target.Identity.Faction == UnitFaction.Player &&
                   TryAttackInternal(attacker, target);
        }

        /// <summary>提交一个指定敌方单位的等待意图，供敌方 AI 使用。</summary>
        public bool TryWait(UnitRuntime unit)
        {
            return _unitManager != null && CanEnemyAct(unit) && TryWaitInternal(unit);
        }

        /// <summary>等待指定单位的攻击表现生命周期结束。</summary>
        public IEnumerator WaitForAttack(UnitRuntime attacker)
        {
            return WaitForAttackCompletionCoroutine(attacker);
        }

        /// <summary>接收攻击规则结算结果并发布已经成立的领域事实。</summary>
        public void HandleAttackResolved(BFAttackResolveResult result)
        {
            if (!result.Succeeded || result.Attacker == null || result.Target == null)
                return;

            result.Attacker.Combat.ClearQueuedAttack();
            result.Attacker.RefreshRuleStateProjection();

            if (result.Attacker.RuleState.IsAlive)
                result.Attacker.StateMachine.ChangeState(result.Attacker.StateMachine.IdleState);

            if (_unitManager.SelectedUnit != null &&
                _unitManager.SelectedUnit.RuleState.Attributes.RemainingActionPoints <= 0)
                _unitManager.DeselectUnitIgnoringLockForCoordinator();

            SetActionLocked(_unitManager.EnemyActionControllerIsExecuting);
            _turnManager?.RefreshPlayerLegalActions();

            _battleSession?.Publish(new BFAttackResolvedEvent(
                _battleSession.Context.BattleId,
                result.Attacker.RuntimeId,
                result.Target.RuntimeId,
                result.FinalDamage,
                result.TargetRemainingHp,
                result.TargetWasKilled,
                _battleSession.Context.TurnNumber));

            if (result.TargetWasKilled)
            {
                _battleSession?.Publish(new BFUnitDefeatedEvent(
                    _battleSession.Context.BattleId,
                    result.Target.RuntimeId,
                    ToDomainFaction(result.Target.Identity.Faction),
                    result.Attacker.RuntimeId,
                    _battleSession.Context.TurnNumber));
            }

            _unitManager.CheckBattleEndCondition();
        }

        /// <summary>清理未产生规则结果的攻击。</summary>
        public void HandleAttackResolutionFailed(UnitRuntime attacker)
        {
            if (attacker == null)
                return;

            attacker.Combat.ClearQueuedAttack();
            if (_unitStateRules?.TryChangeActionState(
                    attacker.RuntimeId,
                    BFUnit_ActionState.Idle) == true)
                attacker.RefreshRuleStateProjection();

            if (attacker.RuleState.IsAlive)
                attacker.StateMachine.ChangeState(attacker.StateMachine.IdleState);

            if (_unitManager != null)
                SetActionLocked(_unitManager.EnemyActionControllerIsExecuting);
            _turnManager?.RefreshPlayerLegalActions();
        }

        private bool TryAttackInternal(UnitRuntime attacker, UnitRuntime target)
        {
            if (_unitManager.IsBoardSyncFaulted || !CanAct(attacker) || target == null ||
                !_unitManager.IsCurrentSessionUnit(target) || !target.RuleState.IsAlive ||
                attacker.Identity.Faction == target.Identity.Faction)
                return false;

            var cost = attacker.RuleState.Attributes.EffectiveAttackCost;
            if (cost <= 0 || attacker.RuleState.Attributes.RemainingActionPoints < cost)
                return false;
            if (_resolutionManager == null || _unitStateRules == null)
                return false;
            if (!_resolutionManager.TryQueueAttack(attacker, target))
                return false;
            if (!attacker.Combat.BeginQueuedAttack(target))
            {
                _resolutionManager.ClearPendingAttack(attacker);
                return false;
            }

            var result = _unitStateRules.TryStartAttack(
                new AttackRequest(attacker.RuntimeId, target.RuntimeId, cost));
            if (!result.Succeeded)
            {
                _resolutionManager.ClearPendingAttack(attacker);
                attacker.Combat.ClearQueuedAttack();
                return false;
            }

            attacker.RefreshRuleStateProjection();
            attacker.StateMachine.AttackState.SetTarget(target);
            attacker.StateMachine.ChangeState(attacker.StateMachine.AttackState);
            _unitManager.RaiseUnitActionEventForCoordinator(attacker, "Attacked", target.RuntimeId, cost);
            SetActionLocked(true);
            _activeAttackUnit = attacker;
            _activeAttackWait = StartCoroutine(WaitForAttackCompletionCoroutine(attacker));
            return true;
        }

        private bool TryWaitInternal(UnitRuntime unit)
        {
            if (_battleSession == null || _unitStateRules == null || !CanAct(unit))
                return false;
            if (_unitStateRules.TryWait(new WaitRequest(unit.RuntimeId)).Succeeded == false)
                return false;

            unit.RefreshRuleStateProjection();
            _battleSession.Publish(new BFUnitWaitedEvent(
                _battleSession.Context.BattleId,
                unit.RuntimeId,
                _battleSession.Context.TurnNumber));
            _turnManager?.RefreshPlayerLegalActions();
            return true;
        }

        private bool CanPlayerAct(UnitRuntime unit)
        {
            return CanAct(unit) && unit.Identity.Faction == UnitFaction.Player &&
                   _turnManager != null && _turnManager.CurrentPhase == BattlePhase.PlayerTurn;
        }

        private bool CanEnemyAct(UnitRuntime unit)
        {
            return CanAct(unit) && unit.Identity.Faction == UnitFaction.Enemy &&
                   _turnManager != null && _turnManager.CurrentPhase == BattlePhase.EnemyTurn;
        }

        private bool CanAct(UnitRuntime unit)
        {
            return _unitManager != null && _battleSession != null &&
                   _battleSession.State == DomainSessionState.Running &&
                   !_unitManager.IsBoardSyncFaulted &&
                   (!_unitManager.IsActionLocked || _unitManager.EnemyActionControllerIsExecuting) &&
                   _unitManager.IsCurrentSessionUnit(unit) &&
                   unit.RuleState.IsAlive && unit.gameObject.activeInHierarchy &&
                   unit.RuleState.Attributes.RemainingActionPoints > 0;
        }

        private IEnumerator WaitForAttackCompletionCoroutine(UnitRuntime unit)
        {
            const float timeoutSeconds = 5f;
            var elapsed = 0f;

            while (unit != null && unit.RuleState.IsAlive && unit.gameObject.activeInHierarchy &&
                   (unit.StateMachine.CurrentState is BFUnit_PresentationAttackState ||
                    unit.Combat.HasQueuedAttack ||
                    (_resolutionManager != null && _resolutionManager.HasPendingAttack(unit))) &&
                   elapsed < timeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (elapsed >= timeoutSeconds)
            {
                Debug.LogWarning($"[BFBattleActionCoordinator] {unit.Identity.DisplayName} 攻击表现等待超时，请检查动画事件。");
                _resolutionManager?.ClearPendingAttack(unit);
                HandleAttackResolutionFailed(unit);
            }
            else if (unit != null && unit.IsRuleBound &&
                     unit.RuleState.ActionState == BFUnit_ActionState.Attack)
            {
                _resolutionManager?.ClearPendingAttack(unit);
                HandleAttackResolutionFailed(unit);
            }

            if (_activeAttackUnit == unit)
                _activeAttackUnit = null;
            _activeAttackWait = null;
        }

        private static BFUnitFaction ToDomainFaction(UnitFaction faction)
        {
            return faction switch
            {
                UnitFaction.Player => BFUnitFaction.Player,
                UnitFaction.Enemy => BFUnitFaction.Enemy,
                _ => BFUnitFaction.None
            };
        }

        private static int GetManhattanDistance(BFGridPosition first, BFGridPosition second)
        {
            return Mathf.Abs(first.X - second.X) + Mathf.Abs(first.Y - second.Y);
        }

        private void OnDisable()
        {
            CleanupInterruptedAttack();
        }

        private static IEnumerator EmptyCoroutine()
        {
            yield break;
        }
    }
}
