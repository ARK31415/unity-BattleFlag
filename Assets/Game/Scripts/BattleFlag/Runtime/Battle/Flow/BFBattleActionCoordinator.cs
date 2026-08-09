using System;
using System.Collections;
using System.Collections.Generic;
using BF.Game.Battle.Domain;
using BF.Game.Battle.Domain.Events;
using BF.Game.Battle.Domain.Units;
using BF.Game.Battle.Rules.Battle;
using BF.Game.Battle.Rules.Units;
using BF.Game.Runtime.Battle.AI;
using BF.Game.Runtime.Battle.Commands;
using BF.Game.Runtime.Battle.Factory;
using BF.Game.Runtime.Battle.Input;
using BF.Game.Runtime.Battle.Managers;
using BF.Game.Runtime.Battle.Query;
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
    public sealed class BFBattleActionCoordinator : MonoBehaviour, IBFBattleActionGateway
    {
        [SerializeField] private BFBattleMovementCoordinator _movementCoordinator;
        [SerializeField] private BFBattleBoardManager _boardManager;
        [SerializeField] private BFBattleResolutionManager _resolutionManager;
        [SerializeField] private BFBattleTurnManager _turnManager;
        [SerializeField] private BFBattleSelectionController _selectionController;

        private DomainBattleSession _battleSession;
        private IBFBattleRuntimeLookup _runtimeLookup;
        private BFBattleUnitQuery _unitQuery;
        private BFBattleOutcomeCoordinator _outcomeCoordinator;
        private BFBattleEnemyActionController _enemyActionController;
        private BFUnitStateRules _unitStateRules;
        private BFBattleBoardRules _boardRules;
        private Coroutine _activeAttackWait;
        private UnitRuntime _activeAttackUnit;
        private bool _isActionLocked;

        /// <summary>当前是否存在正在执行的战斗行动。</summary>
        public bool IsActionLocked => _isActionLocked;

        /// <summary>当前选择对应的 Runtime；选择事实仍只由 SelectionController 保存。</summary>
        public UnitRuntime SelectedUnit => ResolveSelectedUnit();

        /// <summary>当前会话是否允许提交规则行动。</summary>
        public bool IsBattleRunning => _battleSession != null &&
                                       _battleSession.State == DomainSessionState.Running;

        /// <summary>当前棋盘适配层是否处于同步故障状态。</summary>
        public bool IsBoardSyncFaulted => _boardManager != null && _boardManager.IsSyncFaulted;

        /// <summary>行动表现完成后通知输入适配层。</summary>

        /// <summary>绑定统一行动协调器及其适配依赖。</summary>
        public void SetDependencies(
            BFBattleMovementCoordinator movementCoordinator,
            BFBattleResolutionManager resolutionManager,
            BFBattleTurnManager turnManager,
            BFBattleBoardManager boardManager,
            BFBattleSelectionController selectionController,
            IBFBattleRuntimeLookup runtimeLookup,
            BFBattleUnitQuery unitQuery,
            BFBattleOutcomeCoordinator outcomeCoordinator,
            BFBattleEnemyActionController enemyActionController)
        {
            _movementCoordinator = movementCoordinator;
            _resolutionManager = resolutionManager;
            _turnManager = turnManager;
            _boardManager = boardManager;
            _selectionController = selectionController;
            _runtimeLookup = runtimeLookup;
            _unitQuery = unitQuery;
            _outcomeCoordinator = outcomeCoordinator;
            _enemyActionController = enemyActionController;
            _resolutionManager?.SetActionCoordinator(this);
        }

        /// <summary>绑定当前战斗会话唯一的棋盘规则服务。</summary>
        public void SetBoardRules(BFBattleBoardRules boardRules)
        {
            _boardRules = boardRules;
        }

        /// <summary>查询指定单位当前规则 AP 内可达的表现格。</summary>
        public List<Vector2Int> GetReachableCellsForUnit(UnitRuntime unit)
        {
            if (_boardManager == null || !IsCurrentSessionUnit(unit))
                return new List<Vector2Int>();

            var position = unit.RuleState.GridPosition;
            var candidates = _boardManager.GetReachableCells(
                new Vector2Int(position.X, position.Y),
                unit.RuleState.Attributes.RemainingActionPoints,
                unit.RuntimeId);
            if (_boardRules == null)
                return new List<Vector2Int>();

            var validCells = new List<Vector2Int>(candidates.Count);
            foreach (var candidate in candidates)
            {
                var candidatePath = _boardManager.FindPath(
                    new Vector2Int(position.X, position.Y),
                    candidate,
                    unit.RuntimeId);
                var validation = _boardRules.ValidateCandidatePath(
                    unit.RuntimeId,
                    new BFGridPosition(candidate.x, candidate.y),
                    ToRulePath(candidatePath));
                if (validation.Succeeded &&
                    validation.ActionPointCost <= unit.RuleState.Attributes.RemainingActionPoints)
                    validCells.Add(candidate);
            }

            return validCells;
        }

        /// <summary>查询当前选中玩家单位可攻击的敌方表现对象。</summary>
        public List<UnitRuntime> GetAttackableTargets()
        {
            var targets = new List<UnitRuntime>();
            var selected = SelectedUnit;
            if (selected == null || !IsCurrentSessionUnit(selected))
                return targets;

            var position = selected.RuleState.GridPosition;
            var range = selected.RuleState.Attributes.EffectiveAttackRange;
            foreach (var unit in GetAliveUnitsByFaction(UnitFaction.Enemy))
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
            if (_boardManager == null || _runtimeLookup == null)
                return false;

            var players = GetAliveUnitsByFaction(UnitFaction.Player);
            var enemies = GetAliveUnitsByFaction(UnitFaction.Enemy);
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
            if (_runtimeLookup == null || _unitStateRules == null ||
                _battleSession == null || _battleSession.State != DomainSessionState.Running)
                return;

            foreach (var unit in _runtimeLookup.Runtimes)
            {
                if (!IsCurrentSessionUnit(unit))
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

            foreach (var unit in _runtimeLookup?.Runtimes ?? Array.Empty<UnitRuntime>())
            {
                if (unit == null) continue;

                unit.Combat.ClearQueuedAttack();
                _resolutionManager?.ClearPendingAttack(unit);
                if (_unitStateRules != null && IsCurrentSessionUnit(unit) &&
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
            _isActionLocked = value;
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
            if (_movementCoordinator == null || !CanPlayerAct(SelectedUnit))
                return false;

            return _movementCoordinator.TryMove(
                SelectedUnit,
                targetCell,
                refreshPlayerLegalActions: true,
                clearSelectionWhenActed: true);
        }

        /// <summary>将当前选择提交为攻击行动。</summary>
        public bool TryAttackSelected(UnitRuntime target)
        {
            if (!CanPlayerAct(SelectedUnit) ||
                target == null || !IsCurrentSessionUnit(target) || !target.RuleState.IsAlive ||
                target.RuleState.Faction == SelectedUnit.RuleState.Faction)
                return false;

            return TryAttackInternal(SelectedUnit, target);
        }

        /// <summary>
        /// 通过 RuntimeId 提交当前玩家选中的攻击目标。
        /// UI 与输入层只传递身份合同，由行动协调器在适配层内部解析 Runtime。
        /// </summary>
        public bool TryAttackSelected(string targetRuntimeId)
        {
            if (string.IsNullOrWhiteSpace(targetRuntimeId) || _runtimeLookup == null ||
                !_runtimeLookup.TryGetRuntime(targetRuntimeId, out var target))
                return false;

            return TryAttackSelected(target);
        }

        /// <summary>将当前选择提交为等待行动。</summary>
        public bool TryWaitSelected()
        {
            if (!CanPlayerAct(SelectedUnit))
                return false;

            var unit = SelectedUnit;
            if (!TryWaitInternal(unit))
                return false;

            _selectionController?.ClearSelection();
            _turnManager?.RefreshPlayerLegalActions();
            return true;
        }

        /// <summary>提交一个指定敌方单位的移动意图，供敌方 AI 使用。</summary>
        public bool TryMove(UnitRuntime unit, Vector2Int targetCell)
        {
            if (_movementCoordinator == null || !CanEnemyAct(unit))
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
            return CanEnemyAct(attacker) &&
                   target != null && IsCurrentSessionUnit(target) && target.RuleState.IsAlive &&
                   target.RuleState.Faction == BFUnitFaction.Player &&
                   TryAttackInternal(attacker, target);
        }

        /// <summary>提交一个指定敌方单位的等待意图，供敌方 AI 使用。</summary>
        public bool TryWait(UnitRuntime unit)
        {
            return CanEnemyAct(unit) && TryWaitInternal(unit);
        }

        /// <summary>
        /// 通过 RuntimeId 提交移动请求，供输入、HUD 或其他适配层消费者使用。
        /// </summary>
        public bool TryMove(string runtimeId, Vector2Int targetCell)
        {
            if (_runtimeLookup == null || !_runtimeLookup.TryGetRuntime(runtimeId, out var unit) ||
                _movementCoordinator == null)
                return false;

            if (unit.RuleState.Faction == BFUnitFaction.Player)
            {
                if (!CanPlayerAct(unit))
                    return false;

                return _movementCoordinator.TryMove(
                    unit,
                    targetCell,
                    refreshPlayerLegalActions: true,
                    clearSelectionWhenActed: true);
            }

            return TryMove(unit, targetCell);
        }

        /// <summary>
        /// 通过 RuntimeId 提交攻击请求，供输入、HUD、AI 或其他适配层消费者使用。
        /// </summary>
        public bool TryAttack(string attackerRuntimeId, string targetRuntimeId)
        {
            if (_runtimeLookup == null ||
                !_runtimeLookup.TryGetRuntime(attackerRuntimeId, out var attacker) ||
                !_runtimeLookup.TryGetRuntime(targetRuntimeId, out var target))
                return false;

            if (attacker.RuleState.Faction == BFUnitFaction.Player)
            {
                return CanPlayerAct(attacker) &&
                       target.RuleState.IsAlive &&
                       target.RuleState.Faction == BFUnitFaction.Enemy &&
                       TryAttackInternal(attacker, target);
            }

            return TryAttack(attacker, target);
        }

        /// <summary>
        /// 通过 RuntimeId 提交等待请求，供输入、HUD、AI 或其他适配层消费者使用。
        /// </summary>
        public bool TryWait(string runtimeId)
        {
            if (_runtimeLookup == null || !_runtimeLookup.TryGetRuntime(runtimeId, out var unit))
                return false;

            if (unit.RuleState.Faction == BFUnitFaction.Player)
            {
                if (!CanPlayerAct(unit) || !TryWaitInternal(unit))
                    return false;

                if (_selectionController != null &&
                    string.Equals(_selectionController.SelectedRuntimeId, runtimeId, StringComparison.Ordinal))
                    _selectionController.ClearSelection();

                _turnManager?.RefreshPlayerLegalActions();
                return true;
            }

            return TryWait(unit);
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

            if (SelectedUnit != null &&
                SelectedUnit.RuleState.Attributes.RemainingActionPoints <= 0)
                _selectionController?.ClearSelection();

            SetActionLocked(_enemyActionController != null && _enemyActionController.IsExecuting);
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
                    result.Target.RuleState.Faction,
                    result.Attacker.RuntimeId,
                    _battleSession.Context.TurnNumber));
            }

            _outcomeCoordinator?.Evaluate();
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

            SetActionLocked(_enemyActionController != null && _enemyActionController.IsExecuting);
            _turnManager?.RefreshPlayerLegalActions();
        }

        private bool TryAttackInternal(UnitRuntime attacker, UnitRuntime target)
        {
            if (IsBoardSyncFaulted || !CanAct(attacker) || target == null ||
                !IsCurrentSessionUnit(target) || !target.RuleState.IsAlive ||
                attacker.RuleState.Faction == target.RuleState.Faction)
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
            // 攻击开始只驱动表现，不发布领域结算事实；结算事实由命中帧路径发布。
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
            return CanAct(unit) && unit.RuleState.Faction == BFUnitFaction.Player &&
                   _turnManager != null && _turnManager.CurrentPhase == BattlePhase.PlayerTurn;
        }

        private bool CanEnemyAct(UnitRuntime unit)
        {
            return CanAct(unit) && unit.RuleState.Faction == BFUnitFaction.Enemy &&
                   _turnManager != null && _turnManager.CurrentPhase == BattlePhase.EnemyTurn;
        }

        private bool CanAct(UnitRuntime unit)
        {
            return _battleSession != null && _runtimeLookup != null &&
                   _battleSession.State == DomainSessionState.Running &&
                   !IsBoardSyncFaulted &&
                   (!IsActionLocked || (_enemyActionController != null && _enemyActionController.IsExecuting)) &&
                   IsCurrentSessionUnit(unit) &&
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

        /// <summary>获取当前会话内仍存活且属于指定阵营的 Runtime。</summary>
        private List<UnitRuntime> GetAliveUnitsByFaction(UnitFaction faction)
        {
            return _unitQuery != null
                ? _unitQuery.GetAliveRuntimesByFaction(faction)
                : new List<UnitRuntime>();
        }

        /// <summary>验证 Runtime 是否属于当前 BattleSession 且仍绑定其规则状态实例。</summary>
        private bool IsCurrentSessionUnit(UnitRuntime unit)
        {
            return unit != null && unit.IsRuleBound && _runtimeLookup != null &&
                   _battleSession != null &&
                   string.Equals(unit.BattleId, _battleSession.Context.BattleId, StringComparison.Ordinal) &&
                   _battleSession.Context.TryGetUnit(unit.RuntimeId, out var state) &&
                   ReferenceEquals(state, unit.RuleState);
        }

        private UnitRuntime ResolveSelectedUnit()
        {
            var runtimeId = _selectionController?.SelectedRuntimeId;
            if (string.IsNullOrWhiteSpace(runtimeId) || _runtimeLookup == null ||
                !_runtimeLookup.TryGetRuntime(runtimeId, out var runtime) ||
                runtime == null || !runtime.gameObject.activeInHierarchy ||
                !IsCurrentSessionUnit(runtime) || !runtime.RuleState.IsAlive)
                return null;

            return runtime;
        }

        private static int GetManhattanDistance(BFGridPosition first, BFGridPosition second)
        {
            return Mathf.Abs(first.X - second.X) + Mathf.Abs(first.Y - second.Y);
        }

        private static List<BFGridPosition> ToRulePath(IReadOnlyList<Vector2Int> path)
        {
            var rulePath = new List<BFGridPosition>(path.Count);
            for (var index = 0; index < path.Count; index++)
                rulePath.Add(new BFGridPosition(path[index].x, path[index].y));

            return rulePath;
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
