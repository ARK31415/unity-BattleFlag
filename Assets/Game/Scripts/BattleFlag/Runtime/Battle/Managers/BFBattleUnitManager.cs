using System;
using System.Collections;
using System.Collections.Generic;
using BF.Game.Runtime.Battle.Events;
using BF.Game.Runtime.Battle.Presentation;
using BF.Game.Runtime.Battle.Units;
using UnityEngine;

namespace BF.Game.Runtime.Battle.Managers
{
    /// <summary>
    /// 单位管理器。维护单位列表、玩家选择、移动/攻击命令、敌方 AI 行动与胜负判定。
    /// </summary>
    public class BFBattleUnitManager : MonoBehaviour
    {
        [Header("Event Channels")]
        [SerializeField] private BFUnitEventSO _unitEventChannel;

        [Header("Dependencies")]
        [SerializeField] private BFBattleBoardManager _boardManager;
        [SerializeField] private BFBattleTurnManager _turnManager;
        [SerializeField] private BFBattleResolutionManager _resolutionManager;

        [Header("Movement")]
        [SerializeField] private float _secondsPerMoveCell = 0.2f;

        private Coroutine _activeMoveCoroutine;
        private Coroutine _enemyTurnCoroutine;
        private UnitRuntime _activeMovingUnit;
        private bool _isActionLocked;

        /// <summary>战场上所有单位。</summary>
        public List<UnitRuntime> AllUnits { get; private set; } = new();

        /// <summary>当前选中的玩家单位。</summary>
        public UnitRuntime SelectedUnit { get; private set; }

        /// <summary>战斗结果。</summary>
        public BattleResult Result { get; private set; }

        /// <summary>是否有移动或行动表现正在执行。</summary>
        public bool IsActionLocked => _isActionLocked;

        public event Action<UnitRuntime> OnUnitSelected;
        public event Action<UnitRuntime> OnUnitDeselected;
        public event Action<UnitRuntime> OnUnitMoveCompleted;
        public event Action<BattleResult> OnBattleEnded;

        private void OnDisable()
        {
            if (_activeMoveCoroutine != null)
            {
                StopCoroutine(_activeMoveCoroutine);
                _activeMoveCoroutine = null;
            }

            if (_enemyTurnCoroutine != null)
            {
                StopCoroutine(_enemyTurnCoroutine);
                _enemyTurnCoroutine = null;
            }

            RestoreInterruptedMove();
            _isActionLocked = false;
        }

        public void RegisterUnit(UnitRuntime unit)
        {
            if (unit == null || AllUnits.Contains(unit)) return;

            AllUnits.Add(unit);
            unit.GetComponent<BFUnitAnimationPresenter>()?.ApplyInitialFacing();
            Debug.Log($"[BFBattleUnitManager] Registered: {unit.DisplayName} ({unit.Faction})");
        }

        public List<UnitRuntime> GetAliveUnitsByFaction(UnitFaction faction)
        {
            var result = new List<UnitRuntime>();
            foreach (var unit in AllUnits)
            {
                if (unit != null && unit.Faction == faction && unit.IsAlive)
                {
                    result.Add(unit);
                }
            }

            return result;
        }

        public void ResetAllUnitsForNewTurn()
        {
            foreach (var unit in AllUnits)
            {
                unit?.ResetTurnActions();
            }
        }

        public bool TrySelectUnit(UnitRuntime unit)
        {
            if (_isActionLocked) return false;
            if (unit == null || !unit.IsAlive) return false;
            if (_turnManager != null && _turnManager.CurrentPhase != BattlePhase.PlayerTurn) return false;
            if (unit.Faction != UnitFaction.Player) return false;

            DeselectUnit();
            SelectedUnit = unit;
            OnUnitSelected?.Invoke(unit);

            _unitEventChannel?.Raise(new BFUnitEventData
            {
                UnitId = unit.UnitId,
                EventType = "Selected"
            });

            Debug.Log($"[BFBattleUnitManager] Selected: {unit.DisplayName}, AP: {unit.RemainingActionPoints}");
            return true;
        }

        public void DeselectUnit()
        {
            if (_isActionLocked) return;
            if (SelectedUnit == null) return;

            var old = SelectedUnit;
            SelectedUnit = null;
            OnUnitDeselected?.Invoke(old);
            _unitEventChannel?.Raise(new BFUnitEventData
            {
                UnitId = old.UnitId,
                EventType = "Deselected"
            });
        }

        public bool TryMoveUnit(Vector2Int targetCell)
        {
            if (_isActionLocked) return false;
            if (SelectedUnit == null || SelectedUnit.HasActed) return false;
            if (_turnManager != null && _turnManager.CurrentPhase != BattlePhase.PlayerTurn) return false;
            if (!TryGetMovePath(SelectedUnit, targetCell, out var path)) return false;

            _activeMoveCoroutine = StartCoroutine(MoveUnitAlongPathCoroutine(
                SelectedUnit,
                path,
                refreshPlayerLegalActions: true,
                clearSelectionWhenActed: true,
                manageActionLock: true));

            return true;
        }

        public bool TryAttack(UnitRuntime target)
        {
            if (_isActionLocked) return false;
            if (SelectedUnit == null || SelectedUnit.HasActed) return false;
            if (target == null || !target.IsAlive || target.Faction == SelectedUnit.Faction) return false;
            if (_turnManager != null && _turnManager.CurrentPhase != BattlePhase.PlayerTurn) return false;

            if (!TryStartAttack(SelectedUnit, target, allowPartialCost: false, out var attackCost))
            {
                return false;
            }

            _isActionLocked = true;
            RaiseUnitActionEvent(SelectedUnit, "Attacked", target.UnitId, attackCost);
            Debug.Log($"[BFBattleUnitManager] {SelectedUnit.DisplayName} 发起攻击 -> {target.DisplayName}, AP 剩余: {SelectedUnit.RemainingActionPoints}");
            return true;
        }

        public List<Vector2Int> GetReachableCellsForSelected()
        {
            if (SelectedUnit == null || _boardManager == null) return new List<Vector2Int>();

            return _boardManager.GetReachableCells(
                SelectedUnit.GridPosition,
                SelectedUnit.RemainingActionPoints,
                SelectedUnit.UnitId);
        }

        public List<UnitRuntime> GetAttackableTargets()
        {
            var targets = new List<UnitRuntime>();
            if (SelectedUnit == null) return targets;

            foreach (var unit in AllUnits)
            {
                if (unit == null || !unit.IsAlive || unit == SelectedUnit || unit.Faction == SelectedUnit.Faction)
                    continue;

                int distance = GetManhattanDistance(unit.GridPosition, SelectedUnit.GridPosition);
                if (distance <= SelectedUnit.AttackRange)
                {
                    targets.Add(unit);
                }
            }

            return targets;
        }

        public bool PlayerHasLegalAction()
        {
            var players = GetAliveUnitsByFaction(UnitFaction.Player);
            if (players.Count == 0) return false;

            var enemies = GetAliveUnitsByFaction(UnitFaction.Enemy);
            if (enemies.Count == 0) return false;

            foreach (var unit in players)
            {
                if (unit.RemainingActionPoints <= 0) continue;

                var reachable = _boardManager.GetReachableCells(
                    unit.GridPosition,
                    unit.RemainingActionPoints,
                    unit.UnitId);
                if (reachable.Count > 0) return true;

                if (unit.RemainingActionPoints < unit.AttackCost) continue;
                foreach (var enemy in enemies)
                {
                    int distance = GetManhattanDistance(enemy.GridPosition, unit.GridPosition);
                    if (distance <= unit.AttackRange) return true;
                }
            }

            return false;
        }

        public void CheckBattleEndCondition()
        {
            if (Result != null && Result.HasResult) return;

            bool playerAlive = GetAliveUnitsByFaction(UnitFaction.Player).Count > 0;
            bool enemyAlive = GetAliveUnitsByFaction(UnitFaction.Enemy).Count > 0;

            if (!playerAlive)
            {
                Result = BattleResult.Defeat("BattleTest", _turnManager != null ? _turnManager.TurnNumber : 0);
                OnBattleEnded?.Invoke(Result);
                _turnManager?.TransitionToResolution();
            }
            else if (!enemyAlive)
            {
                Result = BattleResult.Victory("BattleTest", _turnManager != null ? _turnManager.TurnNumber : 0);
                OnBattleEnded?.Invoke(Result);
                _turnManager?.TransitionToResolution();
            }
        }

        public void ExecuteEnemyTurn()
        {
            if (_enemyTurnCoroutine != null || _isActionLocked) return;
            _enemyTurnCoroutine = StartCoroutine(ExecuteEnemyTurnCoroutine());
        }

        public void HandleAttackResolved(BF.Game.Runtime.Battle.Commands.BFAttackResolveResult result)
        {
            if (result.Attacker == null || result.Target == null) return;

            _unitEventChannel?.Raise(new BFUnitEventData
            {
                UnitId = result.Target.UnitId,
                EventType = "Damaged",
                TargetId = result.Attacker.UnitId,
                Value = result.FinalDamage
            });

            if (result.TargetWasKilled)
            {
                _unitEventChannel?.Raise(new BFUnitEventData
                {
                    UnitId = result.Target.UnitId,
                    EventType = "Killed"
                });
            }

            result.Attacker.NotifyAttackResolved();
            if (SelectedUnit != null && SelectedUnit.HasActed)
            {
                DeselectUnitIgnoringLock();
            }

            _isActionLocked = _enemyTurnCoroutine != null;
            _turnManager?.RefreshPlayerLegalActions();

            Debug.Log($"[BFBattleUnitManager] 攻击结算完成：{result.Attacker.DisplayName} -> {result.Target.DisplayName}, 伤害 {result.FinalDamage}, 目标剩余 HP {result.TargetRemainingHp}");
            CheckBattleEndCondition();
        }

        private IEnumerator ExecuteEnemyTurnCoroutine()
        {
            _isActionLocked = true;

            var enemies = GetAliveUnitsByFaction(UnitFaction.Enemy);
            var players = GetAliveUnitsByFaction(UnitFaction.Player);

            if (enemies.Count == 0 || players.Count == 0)
            {
                CheckBattleEndCondition();
                FinishEnemyTurn();
                yield break;
            }

            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.IsAlive) continue;

                var nearest = FindNearestPlayer(enemy, players);
                if (nearest == null) break;

                if (TryStartAttack(enemy, nearest, allowPartialCost: true, out _))
                {
                    yield return WaitForAttackToFinishCoroutine(enemy);
                    continue;
                }

                var reachable = _boardManager.GetReachableCells(
                    enemy.GridPosition,
                    enemy.RemainingActionPoints,
                    enemy.UnitId);
                if (reachable.Count > 0)
                {
                    var best = FindBestReachableCell(reachable, nearest.GridPosition);
                    var path = _boardManager.FindPath(enemy.GridPosition, best, enemy.UnitId);
                    if (path.Count > 0)
                    {
                        yield return MoveUnitAlongPathCoroutine(
                            enemy,
                            path,
                            refreshPlayerLegalActions: false,
                            clearSelectionWhenActed: false,
                            manageActionLock: false);
                    }
                }

                if (enemy.IsAlive && nearest.IsAlive && TryStartAttack(enemy, nearest, allowPartialCost: true, out _))
                {
                    yield return WaitForAttackToFinishCoroutine(enemy);
                }
            }

            CheckBattleEndCondition();
            FinishEnemyTurn();
        }

        private IEnumerator MoveUnitAlongPathCoroutine(
            UnitRuntime unit,
            List<Vector2Int> path,
            bool refreshPlayerLegalActions,
            bool clearSelectionWhenActed,
            bool manageActionLock)
        {
            if (unit == null || path == null || path.Count == 0) yield break;

            if (manageActionLock)
            {
                _isActionLocked = true;
            }

            var startCell = unit.GridPosition;
            var presenter = unit.GetComponent<BFUnitAnimationPresenter>();
            _activeMovingUnit = unit;
            unit.GetMoveState().SetTarget(path[^1]);
            unit.ChangeState(unit.GetMoveState());

            var previousCell = startCell;
            for (int i = 0; i < path.Count; i++)
            {
                if (unit == null || !unit.IsAlive || !unit.gameObject.activeInHierarchy) break;

                var nextCell = path[i];
                presenter?.FaceMovementStep(previousCell, nextCell);

                Vector3 fromWorld = unit.transform.position;
                Vector3 toWorld = (Vector3)_boardManager.CellToWorld(nextCell);
                float elapsed = 0f;

                while (elapsed < _secondsPerMoveCell)
                {
                    if (unit == null || !unit.IsAlive || !unit.gameObject.activeInHierarchy) break;

                    elapsed += Time.deltaTime;
                    float t = _secondsPerMoveCell <= 0f ? 1f : Mathf.Clamp01(elapsed / _secondsPerMoveCell);
                    unit.transform.position = Vector3.Lerp(fromWorld, toWorld, t);
                    yield return null;
                }

                if (unit == null || !unit.IsAlive || !unit.gameObject.activeInHierarchy) break;

                unit.transform.position = toWorld;
                previousCell = nextCell;
            }

            bool completed = unit != null && unit.IsAlive && unit.gameObject.activeInHierarchy && previousCell == path[^1];
            if (completed)
            {
                CompleteMove(unit, startCell, previousCell, path.Count, refreshPlayerLegalActions, clearSelectionWhenActed);
            }
            else if (unit != null && unit.IsAlive)
            {
                unit.ChangeState(unit.GetIdleState());
            }

            if (manageActionLock)
            {
                _isActionLocked = false;
                _activeMoveCoroutine = null;
            }

            if (_activeMovingUnit == unit)
            {
                _activeMovingUnit = null;
            }
        }

        private IEnumerator WaitForAttackToFinishCoroutine(UnitRuntime unit)
        {
            const float timeoutSeconds = 10f;
            float elapsed = 0f;

            while (unit != null
                   && unit.IsAlive
                   && (unit.CurrentState is UnitAttackState || unit.HasQueuedAttack || (_resolutionManager != null && _resolutionManager.HasPendingAttack(unit)))
                   && elapsed < timeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (elapsed >= timeoutSeconds)
            {
                Debug.LogWarning($"[BFBattleUnitManager] {unit.DisplayName} 攻击表现等待超时，请检查动画事件。");
            }
        }

        private bool TryGetMovePath(UnitRuntime unit, Vector2Int targetCell, out List<Vector2Int> path)
        {
            path = null;
            if (unit == null || _boardManager == null) return false;

            path = _boardManager.FindPath(unit.GridPosition, targetCell, unit.UnitId);
            if (path.Count == 0)
            {
                Debug.LogWarning($"[BFBattleUnitManager] 目标格子 {targetCell} 不可达。");
                return false;
            }

            if (path.Count > unit.RemainingActionPoints)
            {
                Debug.LogWarning($"[BFBattleUnitManager] 路径成本 {path.Count} 超过剩余 AP {unit.RemainingActionPoints}");
                return false;
            }

            return true;
        }

        private bool TryStartAttack(UnitRuntime attacker, UnitRuntime target, bool allowPartialCost, out int cost)
        {
            cost = 0;
            if (attacker == null || target == null || !attacker.IsAlive || !target.IsAlive) return false;

            int distance = GetManhattanDistance(target.GridPosition, attacker.GridPosition);
            if (distance > attacker.AttackRange) return false;

            cost = allowPartialCost
                ? Mathf.Min(attacker.AttackCost, attacker.RemainingActionPoints)
                : attacker.AttackCost;
            if (cost <= 0 || attacker.RemainingActionPoints < cost) return false;

            if (_resolutionManager == null)
            {
                Debug.LogError("[BFBattleUnitManager] ResolutionManager 未绑定。");
                return false;
            }

            if (!_resolutionManager.TryQueueAttack(attacker, target))
            {
                Debug.LogWarning("[BFBattleUnitManager] 攻击登记失败。");
                return false;
            }

            if (!attacker.BeginQueuedAttack(target))
            {
                _resolutionManager.ClearPendingAttack(attacker);
                return false;
            }

            attacker.ConsumeActionPoints(cost);
            return true;
        }

        private void CompleteMove(
            UnitRuntime unit,
            Vector2Int startCell,
            Vector2Int targetCell,
            int moveCost,
            bool refreshPlayerLegalActions,
            bool clearSelectionWhenActed)
        {
            _boardManager.ReleaseCell(startCell, unit.UnitId);
            _boardManager.OccupyCell(targetCell, unit.UnitId);
            unit.GridPosition = targetCell;
            unit.transform.position = (Vector3)_boardManager.CellToWorld(targetCell);
            unit.ConsumeActionPoints(moveCost);
            unit.ChangeState(unit.GetIdleState());

            RaiseUnitActionEvent(unit, "Moved", $"{targetCell.x},{targetCell.y}", moveCost);
            Debug.Log($"[BFBattleUnitManager] {unit.DisplayName} moved {moveCost} cells to {targetCell}, AP left: {unit.RemainingActionPoints}");

            if (clearSelectionWhenActed && SelectedUnit == unit && unit.HasActed)
            {
                DeselectUnitIgnoringLock();
            }

            if (refreshPlayerLegalActions)
            {
                _turnManager?.RefreshPlayerLegalActions();
            }

            OnUnitMoveCompleted?.Invoke(unit);
        }

        private void RestoreInterruptedMove()
        {
            if (_activeMovingUnit == null || _boardManager == null) return;
            if (!_activeMovingUnit.IsAlive) return;

            _activeMovingUnit.transform.position = (Vector3)_boardManager.CellToWorld(_activeMovingUnit.GridPosition);
            _activeMovingUnit.ChangeState(_activeMovingUnit.GetIdleState());
            _activeMovingUnit = null;
        }

        private void FinishEnemyTurn()
        {
            _enemyTurnCoroutine = null;
            _isActionLocked = false;

            if (Result == null || !Result.HasResult)
            {
                _turnManager?.EndTurn();
            }
        }

        private UnitRuntime FindNearestPlayer(UnitRuntime enemy, List<UnitRuntime> players)
        {
            UnitRuntime nearest = null;
            float minDistance = float.MaxValue;

            foreach (var player in players)
            {
                if (player == null || !player.IsAlive) continue;

                float distance = Vector2Int.Distance(enemy.GridPosition, player.GridPosition);
                if (distance >= minDistance) continue;

                minDistance = distance;
                nearest = player;
            }

            return nearest;
        }

        private Vector2Int FindBestReachableCell(List<Vector2Int> reachable, Vector2Int target)
        {
            Vector2Int best = reachable[0];
            float bestDistance = float.MaxValue;

            foreach (var cell in reachable)
            {
                float distance = Vector2Int.Distance(cell, target);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = cell;
            }

            return best;
        }

        private void RaiseUnitActionEvent(UnitRuntime unit, string eventType, string targetId, int value)
        {
            _unitEventChannel?.Raise(new BFUnitEventData
            {
                UnitId = unit.UnitId,
                EventType = eventType,
                TargetId = targetId,
                Value = value
            });
        }

        private void DeselectUnitIgnoringLock()
        {
            if (SelectedUnit == null) return;

            var old = SelectedUnit;
            SelectedUnit = null;
            OnUnitDeselected?.Invoke(old);
            _unitEventChannel?.Raise(new BFUnitEventData
            {
                UnitId = old.UnitId,
                EventType = "Deselected"
            });
        }

        private static int GetManhattanDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }
    }
}
