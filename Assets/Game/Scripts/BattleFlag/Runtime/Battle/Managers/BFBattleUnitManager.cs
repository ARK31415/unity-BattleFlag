using System;
using System.Collections;
using System.Collections.Generic;
using BF.Game.Battle.Domain;
using BF.Game.Battle.Domain.Events;
using BF.Game.Battle.Domain.Units;
using BF.Game.Battle.Rules.Battle;
using BF.Game.Runtime.Battle.AI;
using BF.Game.Runtime.Battle.Events;
using BF.Game.Runtime.Battle.Flow;
using BF.Game.Runtime.Battle.Input;
using BF.Game.Runtime.Battle.Presentation;
using BF.Game.Runtime.Battle.Units;
using UnityEngine;
using DomainBattleSession = BF.Game.Battle.Domain.BFBattleSession;
using DomainSessionState = BF.Game.Battle.Domain.BFBattleSessionState;

namespace BF.Game.Runtime.Battle.Managers
{
    /// <summary>
    /// 战斗单位迁移期门面。
    ///
    /// 3.3 期间只维护当前会话的 Runtime 注册表、查询入口和旧场景稳定入口；
    /// 选择、行动、移动表现、敌方 AI、胜负和攻击生命周期由独立协调器执行。
    /// 3.5 完成消费者迁移后，该门面及其场景引用将被移除。
    /// </summary>
    [RequireComponent(typeof(BFBattleSelectionController))]
    [RequireComponent(typeof(BFBattleActionCoordinator))]
    [RequireComponent(typeof(BFBattleMovementCoordinator))]
    [RequireComponent(typeof(BFBattleEnemyActionController))]
    [RequireComponent(typeof(BFBattleOutcomeCoordinator))]
    public class BFBattleUnitManager : MonoBehaviour
    {
        [Header("Event Channels")]
        [SerializeField] private BFUnitEventSO _unitEventChannel;

        [Header("Dependencies")]
        [SerializeField] private BFBattleBoardManager _boardManager;
        [SerializeField] private BFBattleTurnManager _turnManager;
        [SerializeField] private BFBattleResolutionManager _resolutionManager;

        [Header("Flow Coordination")]
        [SerializeField] private BFBattleSelectionController _selectionController;
        [SerializeField] private BFBattleActionCoordinator _actionCoordinator;
        [SerializeField] private BFBattleMovementCoordinator _movementCoordinator;
        [SerializeField] private BFBattleEnemyActionController _enemyActionController;
        [SerializeField] private BFBattleOutcomeCoordinator _outcomeCoordinator;

        private bool _isActionLocked;
        private DomainBattleSession _battleSession;
        private BFBattleBoardRules _boardRules;
        private bool _boardSyncFaulted;

        /// <summary>战场上所有通过当前 BattleSession 注册的单位 Runtime。</summary>
        public List<UnitRuntime> AllUnits { get; private set; } = new();

        /// <summary>
        /// 当前选中的玩家单位。
        /// 选择事实由 BFBattleSelectionController 保存 RuntimeId；这里仅解析当前 Runtime 投影。
        /// </summary>
        public UnitRuntime SelectedUnit => ResolveSelectedUnit();

        /// <summary>当前选中的单位 RuntimeId。</summary>
        public string SelectedRuntimeId => _selectionController?.SelectedRuntimeId;

        /// <summary>由胜负协调器产生的表现层战斗结果。</summary>
        public BattleResult Result => _outcomeCoordinator?.Result;

        /// <summary>是否有移动或行动表现正在执行。</summary>
        public bool IsActionLocked => _isActionLocked;

        /// <summary>棋盘占用与规则位置失去一致性时进入故障状态。</summary>
        public bool IsBoardSyncFaulted => _boardSyncFaulted;

        /// <summary>指示单位管理器是否已经绑定战斗会话。</summary>
        public bool HasBattleSession => _battleSession != null;

        /// <summary>指示当前会话仍允许提交规则行动。</summary>
        internal bool IsBattleRunning => _battleSession != null &&
                                          _battleSession.State == DomainSessionState.Running;

        /// <summary>统一的 Move、Attack、Wait 行动协调器。</summary>
        public BFBattleActionCoordinator ActionCoordinator => _actionCoordinator;

        /// <summary>只保存 RuntimeId 的选择控制器。</summary>
        public BFBattleSelectionController SelectionController => _selectionController;

        /// <summary>指示敌方行动控制器是否正在执行当前敌方回合。</summary>
        internal bool EnemyActionControllerIsExecuting =>
            _enemyActionController != null && _enemyActionController.IsExecuting;

        /// <summary>当前单位被选中时触发的兼容性回调。</summary>
        public event Action<UnitRuntime> OnUnitSelected;

        /// <summary>当前单位被取消选中时触发的兼容性回调。</summary>
        public event Action<UnitRuntime> OnUnitDeselected;

        /// <summary>单位完成移动表现时触发的兼容性回调。</summary>
        public event Action<UnitRuntime> OnUnitMoveCompleted;

        /// <summary>战斗结果确定时触发的旧 C# 回调；新代码应优先订阅领域事件。</summary>
        public event Action<BattleResult> OnBattleEnded;

        private void Awake()
        {
            EnsureFlowComponents();
        }

        /// <summary>
        /// 读取场景中已经装配的流程组件并注入依赖。
        /// 组件缺失时不运行期补齐，避免把资源配置错误伪装成兼容路径。
        /// </summary>
        private void EnsureFlowComponents()
        {
            _selectionController ??= GetComponent<BFBattleSelectionController>();
            _actionCoordinator ??= GetComponent<BFBattleActionCoordinator>();
            _movementCoordinator ??= GetComponent<BFBattleMovementCoordinator>();
            _enemyActionController ??= GetComponent<BFBattleEnemyActionController>();
            _outcomeCoordinator ??= GetComponent<BFBattleOutcomeCoordinator>();

            if (_actionCoordinator == null || _movementCoordinator == null ||
                _enemyActionController == null || _outcomeCoordinator == null ||
                _selectionController == null)
            {
                Debug.LogError("[BFBattleUnitManager] 流程协调组件缺失，无法启动战斗流程。", this);
                return;
            }

            _actionCoordinator.SetDependencies(
                this,
                _movementCoordinator,
                _resolutionManager,
                _turnManager,
                _boardManager);
            _actionCoordinator.SetBattleSession(_battleSession);
            _actionCoordinator.SetBoardRules(_boardRules);
            _movementCoordinator.SetDependencies(this, _boardManager);
            _movementCoordinator.SetBattleSession(_battleSession);
            _movementCoordinator.SetBoardRules(_boardRules);
            _enemyActionController.SetDependencies(
                this,
                _actionCoordinator,
                _movementCoordinator,
                _turnManager);
            _outcomeCoordinator.SetDependencies(this, _turnManager, _battleSession);
            _outcomeCoordinator.BattleEnded -= HandleOutcomeCompleted;
            _outcomeCoordinator.BattleEnded += HandleOutcomeCompleted;
        }

        /// <summary>注入统一行动协调器，供场景装配和测试使用。</summary>
        public void SetActionCoordinator(BFBattleActionCoordinator actionCoordinator)
        {
            _actionCoordinator = actionCoordinator;
            EnsureFlowComponents();
        }

        /// <summary>注入选择控制器，供场景装配和测试使用。</summary>
        public void SetSelectionController(BFBattleSelectionController selectionController)
        {
            _selectionController = selectionController;
            EnsureFlowComponents();
        }

        /// <summary>
        /// 将单位管理器绑定到一个战斗会话。
        /// 同一个管理器可以重复绑定同一会话，但不能改绑到其他会话。
        /// </summary>
        /// <param name="session">要绑定的战斗会话。</param>
        public void SetBattleSession(DomainBattleSession session)
        {
            if (_battleSession != null && _battleSession != session)
                throw new InvalidOperationException("BFBattleUnitManager is already attached to another battle session.");

            _battleSession = session;
            EnsureFlowComponents();

            if (session == null)
            {
                _boardSyncFaulted = false;
                _selectionController?.ClearSelection();

                foreach (var unit in AllUnits)
                {
                    if (unit != null)
                        unit.Disabled -= HandleUnitDisabled;
                }

                AllUnits.Clear();
            }
        }

        /// <summary>注入当前战斗会话唯一的棋盘规则服务。</summary>
        public void SetBoardRules(BFBattleBoardRules boardRules)
        {
            if (boardRules != null && _battleSession != null &&
                !boardRules.IsBoundTo(_battleSession.Context))
            {
                throw new InvalidOperationException(
                    "BFBattleBoardRules 必须绑定到同一个 BattleSession Context。");
            }

            _boardRules = boardRules;
            _actionCoordinator?.SetBoardRules(boardRules);
            _movementCoordinator?.SetBoardRules(boardRules);
        }

        /// <summary>测试辅助：注入棋盘管理器引用。</summary>
        internal void SetBoardForTest(BFBattleBoardManager boardManager)
        {
            _boardManager = boardManager;
            EnsureFlowComponents();
        }

        /// <summary>
        /// 在外部完成棋盘修复后重新验证规则位置与棋盘占用。
        /// 验证通过才解除故障状态；该方法不会修改规则状态，也不会自动重建棋盘。
        /// </summary>
        public bool TryRecoverBoardSync()
        {
            if (!_boardSyncFaulted) return true;
            if (_boardManager == null || _battleSession == null) return false;

            var expectedOccupants = new Dictionary<Vector2Int, string>();
            foreach (var unit in AllUnits)
            {
                if (unit == null || !unit.IsRuleBound ||
                    !string.Equals(unit.BattleId, _battleSession.Context.BattleId, StringComparison.Ordinal))
                    return false;
                if (!unit.RuleState.IsAlive) continue;

                var cell = new Vector2Int(unit.RuleState.GridPosition.X, unit.RuleState.GridPosition.Y);
                if (string.IsNullOrWhiteSpace(unit.RuntimeId) ||
                    !expectedOccupants.TryAdd(cell, unit.RuntimeId))
                    return false;
            }

            if (!_boardManager.HasExactUnitOccupancy(expectedOccupants))
                return false;

            _boardSyncFaulted = false;
            return true;
        }

        /// <summary>
        /// 组件禁用时清理协调器持有的表现协程和未提交攻击。
        /// 已由 Rules 提交的结果不会回滚。
        /// </summary>
        private void OnDisable()
        {
            CleanupInterruptedActions();
        }

        private void OnDestroy()
        {
            if (_outcomeCoordinator != null)
                _outcomeCoordinator.BattleEnded -= HandleOutcomeCompleted;

            foreach (var unit in AllUnits)
            {
                if (unit != null)
                    unit.Disabled -= HandleUnitDisabled;
            }
        }

        /// <summary>
        /// 清理被中断的移动与攻击表现，恢复未提交行动的规则状态。
        /// 该方法是适配层生命周期入口，不创建或恢复第二套规则状态。
        /// </summary>
        internal void CleanupInterruptedActions()
        {
            _actionCoordinator?.CleanupInterruptedAttack();
            _movementCoordinator?.CleanupInterruptedMove();
            _enemyActionController?.CancelTurn();

            _actionCoordinator?.CleanupInterruptedActions();

            _isActionLocked = false;
        }

        /// <summary>将已完成规则绑定的单位注册到当前战斗会话。</summary>
        public void RegisterUnit(UnitRuntime unit)
        {
            if (unit == null || AllUnits.Contains(unit)) return;
            if (_battleSession == null)
            {
                Debug.LogWarning($"[BFBattleUnitManager] 拒绝注册未绑定 BattleSession 的单位：{unit.name}");
                return;
            }
            if (!unit.IsRuleBound)
            {
                Debug.LogWarning($"[BFBattleUnitManager] 拒绝注册未绑定规则状态的单位：{unit.name}");
                return;
            }
            if (!string.Equals(unit.BattleId, _battleSession.Context.BattleId, StringComparison.Ordinal) ||
                !_battleSession.Context.TryGetUnit(unit.RuntimeId, out var ruleState) ||
                !ReferenceEquals(ruleState, unit.RuleState))
            {
                Debug.LogWarning($"[BFBattleUnitManager] 拒绝注册不属于当前 BattleSession 的单位：{unit.name}");
                return;
            }

            AllUnits.Add(unit);
            unit.Disabled += HandleUnitDisabled;
            unit.GetComponent<BFUnitAnimationPresenter>()?.ApplyInitialFacing();
        }

        /// <summary>获取指定阵营中仍可参与战斗的 Runtime 投影。</summary>
        public List<UnitRuntime> GetAliveUnitsByFaction(UnitFaction faction)
        {
            var result = new List<UnitRuntime>();
            foreach (var unit in AllUnits)
            {
                if (unit == null || !IsCurrentSessionUnit(unit) || !unit.gameObject.activeInHierarchy ||
                    _battleSession == null || !_battleSession.Context.TryGetUnit(unit.RuntimeId, out var state) ||
                    !state.IsAlive || ToRuntimeFaction(state.Faction) != faction)
                    continue;

                result.Add(unit);
            }

            return result;
        }

        /// <summary>在新回合开始时通过 Rules 重置所有单位回合资源。</summary>
        public void ResetAllUnitsForNewTurn()
        {
            _actionCoordinator?.ResetAllUnitsForNewTurn();
        }

        /// <summary>尝试选中一个当前会话内的玩家单位。</summary>
        public bool TrySelectUnit(UnitRuntime unit)
        {
            EnsureFlowComponents();
            if (_isActionLocked || _boardSyncFaulted || _selectionController == null)
                return false;
                if (unit == null || !IsCurrentSessionUnit(unit) || !unit.gameObject.activeInHierarchy ||
                !unit.RuleState.IsAlive || _battleSession == null || !AllUnits.Contains(unit) ||
                !string.Equals(unit.BattleId, _battleSession.Context.BattleId, StringComparison.Ordinal) ||
                !_battleSession.Context.TryGetUnit(unit.RuntimeId, out var ruleState) ||
                !ReferenceEquals(ruleState, unit.RuleState) ||
                unit.Identity.Faction != UnitFaction.Player)
                return false;
            if (_turnManager == null || _turnManager.CurrentPhase != BattlePhase.PlayerTurn)
                return false;

            DeselectUnit();
            _selectionController.TrySelect(unit.RuntimeId);
            OnUnitSelected?.Invoke(unit);
            _unitEventChannel?.Raise(new BFUnitEventData
            {
                UnitId = unit.RuntimeId,
                EventType = "Selected"
            });
            return true;
        }

        /// <summary>取消当前选中单位；行动锁定期间保持选择不变。</summary>
        public void DeselectUnit()
        {
            EnsureFlowComponents();
            if (_isActionLocked || SelectedUnit == null) return;
            DeselectUnitIgnoringLockForCoordinator();
        }

        /// <summary>尝试让当前选中单位移动到目标格。</summary>
        public bool TryMoveUnit(Vector2Int targetCell)
        {
            EnsureFlowComponents();
            return _actionCoordinator != null && _actionCoordinator.TryMoveSelected(targetCell);
        }

        /// <summary>尝试让当前选中单位攻击目标。</summary>
        public bool TryAttack(UnitRuntime target)
        {
            EnsureFlowComponents();
            return _actionCoordinator != null && _actionCoordinator.TryAttackSelected(target);
        }

        /// <summary>让当前选中单位执行单位级等待。</summary>
        public bool TryWaitSelectedUnit()
        {
            EnsureFlowComponents();
            return _actionCoordinator != null && _actionCoordinator.TryWaitSelected();
        }

        /// <summary>由统一行动协调器提交当前选择的移动。</summary>
        internal bool TryMoveUnitCore(Vector2Int targetCell) =>
            _actionCoordinator != null && _actionCoordinator.TryMoveSelected(targetCell);

        /// <summary>由统一行动协调器提交指定敌方单位的移动。</summary>
        internal bool TryMoveUnitCore(UnitRuntime unit, Vector2Int targetCell) =>
            _actionCoordinator != null && _actionCoordinator.TryMove(unit, targetCell);

        /// <summary>由统一行动协调器提交指定敌方单位的攻击。</summary>
        internal bool TryAttackUnitCore(UnitRuntime attacker, UnitRuntime target) =>
            _actionCoordinator != null && _actionCoordinator.TryAttack(attacker, target);

        /// <summary>由统一行动协调器提交指定敌方单位的等待。</summary>
        internal bool TryWaitUnitCore(UnitRuntime unit) =>
            _actionCoordinator != null && _actionCoordinator.TryWait(unit);

        /// <summary>为移动表现协调器提供指定单位的 A* 路径查询。</summary>
        internal bool TryGetMovePathForCoordinator(UnitRuntime unit, Vector2Int targetCell, out List<Vector2Int> path)
        {
            path = null;
            EnsureFlowComponents();
            return _movementCoordinator != null &&
                   _movementCoordinator.TryGetMovePath(unit, targetCell, out path);
        }

        /// <summary>由移动协调器设置表现行动锁。</summary>
        internal void SetActionLockedForCoordinator(bool value)
        {
            _isActionLocked = value;
        }

        /// <summary>由移动协调器提交移动规则结果。</summary>
        internal bool CompleteMove(
            UnitRuntime unit,
            Vector2Int startCell,
            Vector2Int targetCell,
            int moveCost,
            bool refreshPlayerLegalActions,
            bool clearSelectionWhenActed,
            out bool boardSyncFailed)
        {
            boardSyncFailed = false;
            return _movementCoordinator != null && _movementCoordinator.CompleteMove(
                unit,
                startCell,
                targetCell,
                moveCost,
                refreshPlayerLegalActions,
                clearSelectionWhenActed,
                out boardSyncFailed);
        }

        /// <summary>
        /// 由移动协调器提交带有 A* 候选路径的移动规则结果。
        ///
        /// 候选路径只作为适配层输入，最终仍由棋盘规则服务重新验证；该重载保留在
        /// 内部门面中，便于流程测试验证非法候选路径不会绕过规则提交。
        /// </summary>
        internal bool CompleteMove(
            UnitRuntime unit,
            Vector2Int startCell,
            Vector2Int targetCell,
            int moveCost,
            IReadOnlyList<Vector2Int> candidatePath,
            bool refreshPlayerLegalActions,
            bool clearSelectionWhenActed,
            out bool boardSyncFailed)
        {
            boardSyncFailed = false;
            return _movementCoordinator != null && _movementCoordinator.CompleteMove(
                unit,
                startCell,
                targetCell,
                moveCost,
                candidatePath,
                refreshPlayerLegalActions,
                clearSelectionWhenActed,
                out boardSyncFailed);
        }

        /// <summary>验证 Runtime 是否属于当前 BattleSession 且仍使用其规则状态实例。</summary>
        internal bool IsCurrentSessionUnit(UnitRuntime unit)
        {
            return unit != null && unit.IsRuleBound && _battleSession != null &&
                   string.Equals(unit.BattleId, _battleSession.Context.BattleId, StringComparison.Ordinal) &&
                   _battleSession.Context.TryGetUnit(unit.RuntimeId, out var state) &&
                   ReferenceEquals(state, unit.RuleState);
        }

        /// <summary>等待攻击表现生命周期结束，供敌方行动控制器使用。</summary>
        internal IEnumerator WaitForAttackCompletion(UnitRuntime attacker)
        {
            return _actionCoordinator != null
                ? _actionCoordinator.WaitForAttack(attacker)
                : EmptyCoroutine();
        }

        /// <summary>查询指定单位的可达格，供 AI 和输入查询使用。</summary>
        public List<Vector2Int> GetReachableCellsForUnit(UnitRuntime unit)
        {
            EnsureFlowComponents();
            return _actionCoordinator?.GetReachableCellsForUnit(unit) ?? new List<Vector2Int>();
        }

        /// <summary>获取当前选中单位在剩余 AP 内可到达的格子。</summary>
        public List<Vector2Int> GetReachableCellsForSelected()
        {
            return SelectedUnit == null
                ? new List<Vector2Int>()
                : GetReachableCellsForUnit(SelectedUnit);
        }

        /// <summary>获取当前选中单位可以攻击的敌方目标。</summary>
        public List<UnitRuntime> GetAttackableTargets()
        {
            EnsureFlowComponents();
            return _actionCoordinator?.GetAttackableTargets() ?? new List<UnitRuntime>();
        }

        /// <summary>判断玩家阵营是否仍有移动或攻击可执行。</summary>
        public bool PlayerHasLegalAction()
        {
            EnsureFlowComponents();
            return _actionCoordinator != null && _actionCoordinator.PlayerHasLegalAction();
        }

        /// <summary>请求胜负协调器根据规则状态评估战斗结果。</summary>
        public void CheckBattleEndCondition()
        {
            EnsureFlowComponents();
            _outcomeCoordinator?.Evaluate();
        }

        /// <summary>启动敌方回合行动控制器。</summary>
        public void ExecuteEnemyTurn()
        {
            EnsureFlowComponents();
            _enemyActionController?.BeginTurn();
        }

        /// <summary>将结算层攻击结果转交给统一行动协调器。</summary>
        public void HandleAttackResolved(BF.Game.Runtime.Battle.Commands.BFAttackResolveResult result)
        {
            _actionCoordinator?.HandleAttackResolved(result);
        }

        /// <summary>将未完成攻击清理转交给统一行动协调器。</summary>
        internal void HandleAttackResolutionFailed(UnitRuntime attacker)
        {
            _actionCoordinator?.HandleAttackResolutionFailed(attacker);
        }

        /// <summary>供移动协调器报告规则提交后的棋盘同步故障。</summary>
        internal void MarkBoardSyncFaultForCoordinator()
        {
            _boardSyncFaulted = true;
        }

        /// <summary>供移动协调器在规则提交完成后刷新玩家行动提示。</summary>
        internal void RefreshPlayerLegalActionsForCoordinator()
        {
            _turnManager?.RefreshPlayerLegalActions();
        }

        /// <summary>供移动协调器在规则提交完成后发送迁移期表现回调。</summary>
        internal void NotifyMoveCompletedForCoordinator(UnitRuntime unit)
        {
            OnUnitMoveCompleted?.Invoke(unit);
        }

        /// <summary>供行动协调器转发单位级表现通知。</summary>
        internal void RaiseUnitActionEventForCoordinator(UnitRuntime unit, string eventType, string targetId, int value)
        {
            _unitEventChannel?.Raise(new BFUnitEventData
            {
                UnitId = unit.RuntimeId,
                EventType = eventType,
                TargetId = targetId,
                Value = value
            });
        }

        /// <summary>供协调器在规则行动完成后清除选择。</summary>
        internal void DeselectUnitIgnoringLockForCoordinator()
        {
            var old = SelectedUnit;
            if (old == null) return;

            _selectionController?.ClearSelection();
            OnUnitDeselected?.Invoke(old);
            _unitEventChannel?.Raise(new BFUnitEventData
            {
                UnitId = old.RuntimeId,
                EventType = "Deselected"
            });
        }

        private void HandleOutcomeCompleted(BattleResult result)
        {
            OnBattleEnded?.Invoke(result);
        }

        private void HandleUnitDisabled(UnitRuntime unit)
        {
            if (unit == null) return;

            if (_selectionController?.IsSelected(unit.RuntimeId) == true)
            {
                _selectionController.ClearSelection();
                OnUnitDeselected?.Invoke(unit);
                _unitEventChannel?.Raise(new BFUnitEventData
                {
                    UnitId = unit.RuntimeId,
                    EventType = "Deselected"
                });
            }

            var wasAttacking = unit.IsRuleBound &&
                               unit.RuleState.ActionState == BFUnit_ActionState.Attack;

            _movementCoordinator?.CleanupInterruptedMove(unit);
            _resolutionManager?.ClearPendingAttacksInvolving(unit);

            // 测试或外部生命周期可能只建立了规则攻击状态而尚未登记 pending attack；
            // 这种情况下仍需恢复规则状态，但不能对普通被禁用目标释放全局行动锁。
            if (wasAttacking && unit.RuleState.ActionState == BFUnit_ActionState.Attack)
                _actionCoordinator?.HandleAttackResolutionFailed(unit);
        }

        private UnitRuntime ResolveSelectedUnit()
        {
            var runtimeId = _selectionController?.SelectedRuntimeId;
            if (string.IsNullOrWhiteSpace(runtimeId)) return null;

            foreach (var unit in AllUnits)
            {
                if (unit != null && unit.gameObject.activeInHierarchy && IsCurrentSessionUnit(unit) &&
                    unit.RuleState.IsAlive && string.Equals(unit.RuntimeId, runtimeId, StringComparison.Ordinal))
                    return unit;
            }

            return null;
        }

        private static UnitFaction ToRuntimeFaction(BFUnitFaction faction)
        {
            return faction switch
            {
                BFUnitFaction.Player => UnitFaction.Player,
                BFUnitFaction.Enemy => UnitFaction.Enemy,
                _ => UnitFaction.None
            };
        }

        private static IEnumerator EmptyCoroutine()
        {
            yield break;
        }
    }
}
