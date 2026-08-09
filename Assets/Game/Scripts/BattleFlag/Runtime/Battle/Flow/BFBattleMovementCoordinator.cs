using System.Collections;
using System.Collections.Generic;
using BF.Game.Battle.Domain;
using BF.Game.Battle.Domain.Events;
using BF.Game.Battle.Domain.Units;
using BF.Game.Battle.Rules.Battle;
using BF.Game.Battle.Rules.Units;
using BF.Game.Runtime.Battle.Factory;
using BF.Game.Runtime.Battle.Input;
using BF.Game.Runtime.Battle.Managers;
using BF.Game.Runtime.Battle.Presentation;
using BF.Game.Runtime.Battle.Units;
using UnityEngine;
using DomainBattleSession = BF.Game.Battle.Domain.BFBattleSession;

namespace BF.Game.Runtime.Battle.Flow
{
    /// <summary>
    /// 移动表现协调器。
    ///
    /// 负责移动协程、Transform 插值、朝向和表现中断清理；
    /// 规则位置、AP 和移动合法性由本协调器转交 Rules 处理；
    /// 单位查询和行动锁由适配层合同注入，不依赖迁移期单位门面。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BFBattleMovementCoordinator : MonoBehaviour
    {
        [SerializeField] private BFBattleBoardManager _boardManager;
        [SerializeField] private BFBattleActionCoordinator _actionCoordinator;
        [SerializeField] private BFBattleSelectionController _selectionController;
        [SerializeField] private BFBattleTurnManager _turnManager;
        [SerializeField] private float _secondsPerMoveCell = 0.2f;

        private Coroutine _activeMoveCoroutine;
        private UnitRuntime _activeMovingUnit;
        private DomainBattleSession _battleSession;
        private IBFBattleRuntimeLookup _runtimeLookup;
        private BFUnitStateRules _unitStateRules;
        private BFBattleBoardRules _boardRules;

        /// <summary>指示当前是否有移动表现正在执行。</summary>
        public bool IsMoving => _activeMoveCoroutine != null;

        /// <summary>移动表现完成时通知输入适配层；参数为 Runtime 投影。</summary>
        public event System.Action<UnitRuntime> MoveCompleted;

        /// <summary>绑定移动表现所需的适配依赖。</summary>
        public void SetDependencies(
            IBFBattleRuntimeLookup runtimeLookup,
            BFBattleBoardManager boardManager,
            BFBattleActionCoordinator actionCoordinator,
            BFBattleSelectionController selectionController,
            BFBattleTurnManager turnManager)
        {
            _runtimeLookup = runtimeLookup;
            _boardManager = boardManager;
            _actionCoordinator = actionCoordinator;
            _selectionController = selectionController;
            _turnManager = turnManager;
        }

        /// <summary>绑定当前战斗会话，使移动规则提交使用本场战斗的唯一规则状态。</summary>
        public void SetBattleSession(DomainBattleSession session)
        {
            _battleSession = session;
            _unitStateRules = session == null ? null : new BFUnitStateRules(session.Context);
        }

        /// <summary>绑定当前战斗会话唯一的棋盘规则服务。</summary>
        public void SetBoardRules(BFBattleBoardRules boardRules)
        {
            _boardRules = boardRules;
        }

        /// <summary>查询并校验当前规则 AP 内可提交的移动路径。</summary>
        public bool TryGetMovePath(UnitRuntime unit, Vector2Int targetCell, out List<Vector2Int> path)
        {
            path = null;
            if (_boardManager == null || _battleSession == null ||
                _boardRules == null ||
                _battleSession.State != BFBattleSessionState.Running ||
                !IsCurrentSessionUnit(unit) || !unit.RuleState.IsAlive)
                return false;

            path = _boardManager.FindPath(
                new Vector2Int(unit.RuleState.GridPosition.X, unit.RuleState.GridPosition.Y),
                targetCell,
                unit.RuntimeId);
            if (path.Count == 0) return false;

            var rulePath = ToRulePath(path);
            var validation = _boardRules.ValidateCandidatePath(
                unit.RuntimeId,
                new BFGridPosition(targetCell.x, targetCell.y),
                rulePath);
            return validation.Succeeded &&
                   validation.ActionPointCost <= unit.RuleState.Attributes.RemainingActionPoints;
        }

        /// <summary>
        /// 为指定单位启动移动表现。
        /// </summary>
        public bool TryMove(
            UnitRuntime unit,
            Vector2Int targetCell,
            bool refreshPlayerLegalActions,
            bool clearSelectionWhenActed)
        {
            if (_runtimeLookup == null || _actionCoordinator == null || _boardManager == null || IsMoving ||
                unit == null || !unit.IsRuleBound || !unit.RuleState.IsAlive ||
                !unit.gameObject.activeInHierarchy)
                return false;
            if (!TryGetMovePath(unit, targetCell, out var path))
                return false;

            _actionCoordinator.SetActionLocked(true);
            _activeMovingUnit = unit;
            _activeMoveCoroutine = StartCoroutine(MoveUnitAlongPathCoroutine(
                unit,
                path,
                refreshPlayerLegalActions,
                clearSelectionWhenActed));
            return true;
        }

        /// <summary>等待当前移动表现结束。</summary>
        public IEnumerator WaitForCompletion()
        {
            while (IsMoving)
                yield return null;
        }

        /// <summary>
        /// 清理移动中断状态。
        /// </summary>
        public void CleanupInterruptedMove()
        {
            if (_activeMoveCoroutine != null)
            {
                StopCoroutine(_activeMoveCoroutine);
                _activeMoveCoroutine = null;
            }

            var unit = _activeMovingUnit;
            _activeMovingUnit = null;
            if (unit != null)
                RestoreMovePresentation(unit, new Vector2Int(
                    unit.RuleState.GridPosition.X,
                    unit.RuleState.GridPosition.Y));

            _actionCoordinator?.SetActionLocked(false);
        }

        /// <summary>
        /// 仅在指定单位就是当前移动单位时清理移动表现。
        /// 其他单位的 Unity 生命周期变化不能取消正在进行的移动。
        /// </summary>
        public void CleanupInterruptedMove(UnitRuntime unit)
        {
            if (unit != null && _activeMovingUnit != unit)
                return;

            CleanupInterruptedMove();
        }

        /// <summary>
        /// Unity 对象禁用时停止移动表现并恢复未提交移动的投影状态。
        /// 规则位置与 AP 只有在提交成功后才存在，不在此处回滚已提交事实。
        /// </summary>
        private void OnDisable()
        {
            CleanupInterruptedMove();
        }

        private IEnumerator MoveUnitAlongPathCoroutine(
            UnitRuntime unit,
            IReadOnlyList<Vector2Int> path,
            bool refreshPlayerLegalActions,
            bool clearSelectionWhenActed)
        {
            var startCell = new Vector2Int(
                unit.RuleState.GridPosition.X,
                unit.RuleState.GridPosition.Y);
            if (unit == null || !unit.gameObject.activeInHierarchy || !unit.RuleState.IsAlive ||
                !TryBeginMovePresentation(unit))
            {
                FinishMove(unit);
                yield break;
            }

            var presenter = unit.GetComponent<BFUnitAnimationPresenter>();
            unit.StateMachine.MoveState.SetTarget(path[path.Count - 1]);
            unit.StateMachine.ChangeState(unit.StateMachine.MoveState);

            var previousCell = startCell;
            for (var index = 0; index < path.Count; index++)
            {
                if (unit == null || !unit.RuleState.IsAlive || !unit.gameObject.activeInHierarchy)
                    break;

                var nextCell = path[index];
                presenter?.FaceMovementStep(previousCell, nextCell);

                var fromWorld = unit.transform.position;
                var toWorld = (Vector3)_boardManager.CellToWorld(nextCell);
                var elapsed = 0f;
                while (elapsed < _secondsPerMoveCell)
                {
                    if (unit == null || !unit.RuleState.IsAlive || !unit.gameObject.activeInHierarchy)
                        break;

                    elapsed += Time.deltaTime;
                    var duration = _secondsPerMoveCell <= 0f
                        ? 1f
                        : Mathf.Clamp01(elapsed / _secondsPerMoveCell);
                    unit.transform.position = Vector3.Lerp(fromWorld, toWorld, duration);
                    yield return null;
                }

                if (unit == null || !unit.RuleState.IsAlive || !unit.gameObject.activeInHierarchy)
                    break;

                unit.transform.position = toWorld;
                previousCell = nextCell;
            }

            var completed = unit != null
                            && unit.RuleState.IsAlive
                            && unit.gameObject.activeInHierarchy
                            && previousCell == path[path.Count - 1];
            if (completed)
            {
                var committed = CompleteMove(
                    unit,
                    startCell,
                    previousCell,
                    path.Count,
                    path,
                    refreshPlayerLegalActions,
                    clearSelectionWhenActed,
                    out var boardSyncFailed);
                if (!committed)
                    RestoreMovePresentation(unit, startCell);
                else if (boardSyncFailed)
                    FinishMove(unit);
            }
            else if (unit != null && unit.RuleState.IsAlive)
            {
                RestoreMovePresentation(unit, startCell);
            }

            FinishMove(unit);
        }

        private void FinishMove(UnitRuntime unit)
        {
            if (_activeMovingUnit == unit)
                _activeMovingUnit = null;

            _activeMoveCoroutine = null;
            _actionCoordinator?.SetActionLocked(false);
        }

        /// <summary>
        /// 提交一次移动规则命令，并在规则提交成功后同步棋盘与表现。
        /// 规则事实一旦提交不会因适配层同步失败而回滚。
        /// </summary>
        internal bool CompleteMove(
            UnitRuntime unit,
            Vector2Int startCell,
            Vector2Int targetCell,
            int moveCost,
            bool refreshPlayerLegalActions,
            bool clearSelectionWhenActed,
            out bool boardSyncFailed)
        {
            if (_boardRules == null)
            {
                boardSyncFailed = false;
                return false;
            }

            return CompleteMove(
                unit,
                startCell,
                targetCell,
                moveCost,
                null,
                refreshPlayerLegalActions,
                clearSelectionWhenActed,
                out boardSyncFailed);
        }

        /// <summary>
        /// 使用 A* 候选路径提交移动；候选路径在规则提交前再次验证，防止表现期间
        /// 棋盘状态变化后仍然写入非法位置。
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
            if (unit == null || _unitStateRules == null || _battleSession == null ||
                _battleSession.State != BFBattleSessionState.Running)
                return false;
            if (_boardRules == null)
                return false;

            var expectedStart = unit.RuleState.GridPosition;
            if (expectedStart.X != startCell.x || expectedStart.Y != startCell.y)
            {
                Debug.LogWarning(
                    $"[BFBattleMovementCoordinator] 移动起点与规则位置不一致：{unit.RuntimeId}。");
                return false;
            }

            if (candidatePath == null)
                return false;

            var validation = _boardRules.ValidateCandidatePath(
                unit.RuntimeId,
                new BFGridPosition(targetCell.x, targetCell.y),
                ToRulePath(candidatePath));
            if (!validation.Succeeded || validation.ActionPointCost != moveCost)
            {
                Debug.LogWarning(
                    $"[BFBattleMovementCoordinator] 候选路径被棋盘规则拒绝：{validation.FailureReason}");
                return false;
            }

            var result = _unitStateRules.TryMove(new MoveRequest(
                unit.RuntimeId,
                new BFGridPosition(targetCell.x, targetCell.y),
                moveCost));
            if (!result.Succeeded)
            {
                Debug.LogWarning($"[BFBattleMovementCoordinator] 规则移动被拒绝：{unit.Identity.DisplayName}，{result.FailureReason}");
                return false;
            }

            unit.RefreshRuleStateProjection();

            if (!_boardManager.TryMoveOccupancy(startCell, targetCell, unit.RuntimeId))
            {
                Debug.LogError($"[BFBattleMovementCoordinator] 棋盘占用同步失败：{unit.Identity.DisplayName} 目标格 {targetCell} 不可占用。规则位置与 AP 已提交，停止本次表现流程。");
                unit.transform.position = (Vector3)_boardManager.CellToWorld(targetCell);
                unit.StateMachine.ChangeState(unit.StateMachine.IdleState);
                PublishMovedEvent(result, unit.RuntimeId, moveCost);
                _boardManager.MarkSyncFault();
                boardSyncFailed = true;
                return true;
            }

            unit.transform.position = (Vector3)_boardManager.CellToWorld(targetCell);
            unit.StateMachine.ChangeState(unit.StateMachine.IdleState);
            PublishMovedEvent(result, unit.RuntimeId, moveCost);

            if (clearSelectionWhenActed && unit.RuleState.Attributes.RemainingActionPoints <= 0)
                _selectionController?.ClearSelection();
            if (refreshPlayerLegalActions)
                _turnManager?.RefreshPlayerLegalActions();

            MoveCompleted?.Invoke(unit);
            return true;
        }

        /// <summary>恢复尚未提交移动的 Transform 和表现状态，不修改棋盘占用。</summary>
        internal void RestoreMovePresentationForCoordinator(UnitRuntime unit, Vector2Int startCell)
        {
            RestoreMovePresentation(unit, startCell);
        }

        private void RestoreMovePresentation(UnitRuntime unit, Vector2Int startCell)
        {
            if (unit == null) return;

            if (unit.IsRuleBound && _unitStateRules != null &&
                _unitStateRules.TryChangeActionState(unit.RuntimeId, BFUnit_ActionState.Idle))
                unit.RefreshRuleStateProjection();

            if (!unit.RuleState.IsAlive) return;
            if (_boardManager != null && unit.gameObject.activeInHierarchy)
                unit.transform.position = (Vector3)_boardManager.CellToWorld(startCell);
            unit.StateMachine.ChangeState(unit.StateMachine.IdleState);
        }

        private bool TryBeginMovePresentation(UnitRuntime unit)
        {
            return unit != null && unit.IsRuleBound && _unitStateRules != null &&
                   _unitStateRules.TryChangeActionState(unit.RuntimeId, BFUnit_ActionState.Move);
        }

        private void PublishMovedEvent(MoveResult result, string runtimeId, int moveCost)
        {
            _battleSession.Publish(new BFUnitMovedEvent(
                _battleSession.Context.BattleId,
                runtimeId,
                result.FromGridPosition.Value,
                result.ToGridPosition.Value,
                moveCost,
                _battleSession.Context.TurnNumber));
        }

        private static List<BFGridPosition> ToRulePath(IReadOnlyList<Vector2Int> path)
        {
            var rulePath = new List<BFGridPosition>(path.Count);
            for (var index = 0; index < path.Count; index++)
                rulePath.Add(new BFGridPosition(path[index].x, path[index].y));

            return rulePath;
        }

        private bool IsCurrentSessionUnit(UnitRuntime unit)
        {
            return unit != null && unit.IsRuleBound && _runtimeLookup != null &&
                   _battleSession != null &&
                   string.Equals(unit.BattleId, _battleSession.Context.BattleId, System.StringComparison.Ordinal) &&
                   _battleSession.Context.TryGetUnit(unit.RuntimeId, out var state) &&
                   ReferenceEquals(state, unit.RuleState);
        }
    }
}
