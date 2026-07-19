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

        // 组件禁用时停止正在进行的协程，并把移动中的单位恢复到当前格子世界坐标。
        // 这样场景切换或对象禁用不会留下半锁定的输入状态。
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

        /// <summary>
        /// 将场景中的单位根注册到战斗单位列表。
        /// </summary>
        /// <param name="unit">已经完成 UnitRuntime 初始化的单位根。</param>
        public void RegisterUnit(UnitRuntime unit)
        {
            if (unit == null || AllUnits.Contains(unit)) return;

            // UnitRuntime 只作为根对象入表；身份和阵营等业务信息从 Identity 子组件读取。
            AllUnits.Add(unit);
            unit.GetComponent<BFUnitAnimationPresenter>()?.ApplyInitialFacing();
            Debug.Log($"[BFBattleUnitManager] Registered: {unit.Identity.DisplayName} ({unit.Identity.Faction})");
        }

        /// <summary>
        /// 获取指定阵营中仍可参与战斗的单位根列表。
        /// </summary>
        /// <param name="faction">要筛选的阵营。</param>
        /// <returns>阵营匹配且 Stats.IsAlive 为 true 的单位列表。</returns>
        public List<UnitRuntime> GetAliveUnitsByFaction(UnitFaction faction)
        {
            var result = new List<UnitRuntime>();
            foreach (var unit in AllUnits)
            {
                if (unit != null && unit.Identity.Faction == faction && unit.Stats.IsAlive)
                {
                    result.Add(unit);
                }
            }

            return result;
        }

        /// <summary>
        /// 在新回合开始时重置所有单位的回合资源。
        /// </summary>
        public void ResetAllUnitsForNewTurn()
        {
            foreach (var unit in AllUnits)
            {
                // 回合时机由 UnitManager/TurnManager 管理，具体 AP 重置由单位生命周期入口下发给 Stats。
                unit?.BeginTurn();
            }
        }

        /// <summary>
        /// 尝试选中一个玩家单位。
        /// </summary>
        /// <param name="unit">玩家点击或输入命中的单位根。</param>
        /// <returns>true 表示单位已成为当前选中单位。</returns>
        public bool TrySelectUnit(UnitRuntime unit)
        {
            if (_isActionLocked) return false;
            // 选择规则只读取子组件数据，避免重新把阵营和存活状态塞回 UnitRuntime 根 API。
            if (unit == null || !unit.Stats.IsAlive) return false;
            if (_turnManager != null && _turnManager.CurrentPhase != BattlePhase.PlayerTurn) return false;
            if (unit.Identity.Faction != UnitFaction.Player) return false;

            DeselectUnit();
            SelectedUnit = unit;
            OnUnitSelected?.Invoke(unit);

            _unitEventChannel?.Raise(new BFUnitEventData
            {
                UnitId = unit.UnitId,
                EventType = "Selected"
            });

            Debug.Log($"[BFBattleUnitManager] Selected: {unit.Identity.DisplayName}, AP: {unit.Stats.RemainingActionPoints}");
            return true;
        }

        /// <summary>
        /// 取消当前选中单位。
        ///
        /// 动作锁定期间不会取消选择，避免移动或攻击表现中途丢失上下文。
        /// </summary>
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

        /// <summary>
        /// 尝试让当前选中单位移动到目标格。
        /// </summary>
        /// <param name="targetCell">目标棋盘格坐标。</param>
        /// <returns>true 表示移动协程已启动。</returns>
        public bool TryMoveUnit(Vector2Int targetCell)
        {
            if (_isActionLocked) return false;
            if (SelectedUnit == null || SelectedUnit.Stats.HasActed) return false;
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

        /// <summary>
        /// 尝试让当前选中单位攻击目标。
        /// </summary>
        /// <param name="target">攻击目标单位根。</param>
        /// <returns>true 表示攻击表现和待结算上下文已启动。</returns>
        public bool TryAttack(UnitRuntime target)
        {
            if (_isActionLocked) return false;
            if (SelectedUnit == null || SelectedUnit.Stats.HasActed) return false;
            if (target == null || !target.Stats.IsAlive || target.Identity.Faction == SelectedUnit.Identity.Faction) return false;
            if (_turnManager != null && _turnManager.CurrentPhase != BattlePhase.PlayerTurn) return false;

            if (!TryStartAttack(SelectedUnit, target, allowPartialCost: false, out var attackCost))
            {
                return false;
            }

            _isActionLocked = true;
            RaiseUnitActionEvent(SelectedUnit, "Attacked", target.UnitId, attackCost);
            Debug.Log($"[BFBattleUnitManager] {SelectedUnit.Identity.DisplayName} 发起攻击 -> {target.Identity.DisplayName}, AP 剩余: {SelectedUnit.Stats.RemainingActionPoints}");
            return true;
        }

        /// <summary>
        /// 获取当前选中单位在剩余 AP 内可到达的格子。
        /// </summary>
        /// <returns>可移动目标格列表；无选中单位或棋盘缺失时返回空列表。</returns>
        public List<Vector2Int> GetReachableCellsForSelected()
        {
            if (SelectedUnit == null || _boardManager == null) return new List<Vector2Int>();

            return _boardManager.GetReachableCells(
                SelectedUnit.Grid.GridPosition,
                SelectedUnit.Stats.RemainingActionPoints,
                SelectedUnit.UnitId);
        }

        /// <summary>
        /// 获取当前选中单位可以攻击的敌方目标。
        /// </summary>
        /// <returns>处于攻击范围内且仍存活的敌方单位根列表。</returns>
        public List<UnitRuntime> GetAttackableTargets()
        {
            var targets = new List<UnitRuntime>();
            if (SelectedUnit == null) return targets;

            foreach (var unit in AllUnits)
            {
                if (unit == null || !unit.Stats.IsAlive || unit == SelectedUnit || unit.Identity.Faction == SelectedUnit.Identity.Faction)
                    continue;

                int distance = GetManhattanDistance(unit.Grid.GridPosition, SelectedUnit.Grid.GridPosition);
                if (distance <= SelectedUnit.Stats.AttackRange)
                {
                    targets.Add(unit);
                }
            }

            return targets;
        }

        /// <summary>
        /// 判断玩家阵营是否仍有移动或攻击可执行。
        /// </summary>
        /// <returns>true 表示至少一个玩家单位还有合法行动。</returns>
        public bool PlayerHasLegalAction()
        {
            var players = GetAliveUnitsByFaction(UnitFaction.Player);
            if (players.Count == 0) return false;

            var enemies = GetAliveUnitsByFaction(UnitFaction.Enemy);
            if (enemies.Count == 0) return false;

            foreach (var unit in players)
            {
                if (unit.Stats.RemainingActionPoints <= 0) continue;

                var reachable = _boardManager.GetReachableCells(
                    unit.Grid.GridPosition,
                    unit.Stats.RemainingActionPoints,
                    unit.UnitId);
                if (reachable.Count > 0) return true;

                if (unit.Stats.RemainingActionPoints < unit.Stats.AttackCost) continue;
                foreach (var enemy in enemies)
                {
                    int distance = GetManhattanDistance(enemy.Grid.GridPosition, unit.Grid.GridPosition);
                    if (distance <= unit.Stats.AttackRange) return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 检查双方存活单位并在一方全灭时产生战斗结果。
        /// </summary>
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

        /// <summary>
        /// 启动敌方回合 AI 表现协程。
        ///
        /// 若已有敌方回合或动作锁正在进行，本次调用会被忽略。
        /// </summary>
        public void ExecuteEnemyTurn()
        {
            if (_enemyTurnCoroutine != null || _isActionLocked) return;
            _enemyTurnCoroutine = StartCoroutine(ExecuteEnemyTurnCoroutine());
        }

        /// <summary>
        /// 处理结算层返回的攻击结果，广播单位事件并收尾攻击生命周期。
        /// </summary>
        /// <param name="result">结算层生成的攻击结果。</param>
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

            // 结算完成后由 UnitManager 协调攻击生命周期收尾：
            // Combat 清理上下文，StateMachine 只在攻击者仍存活时回到 Idle。
            result.Attacker.Combat.ClearQueuedAttack();
            if (result.Attacker.Stats.IsAlive)
            {
                result.Attacker.StateMachine.ChangeState(result.Attacker.StateMachine.IdleState);
            }

            if (SelectedUnit != null && SelectedUnit.Stats.HasActed)
            {
                DeselectUnitIgnoringLock();
            }

            _isActionLocked = _enemyTurnCoroutine != null;
            _turnManager?.RefreshPlayerLegalActions();

            Debug.Log($"[BFBattleUnitManager] 攻击结算完成：{result.Attacker.Identity.DisplayName} -> {result.Target.Identity.DisplayName}, 伤害 {result.FinalDamage}, 目标剩余 HP {result.TargetRemainingHp}");
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
                if (enemy == null || !enemy.Stats.IsAlive) continue;

                var nearest = FindNearestPlayer(enemy, players);
                if (nearest == null) break;

                if (TryStartAttack(enemy, nearest, allowPartialCost: true, out _))
                {
                    yield return WaitForAttackToFinishCoroutine(enemy);
                    continue;
                }

                var reachable = _boardManager.GetReachableCells(
                    enemy.Grid.GridPosition,
                    enemy.Stats.RemainingActionPoints,
                    enemy.UnitId);
                if (reachable.Count > 0)
                {
                    var best = FindBestReachableCell(reachable, nearest.Grid.GridPosition);
                    var path = _boardManager.FindPath(enemy.Grid.GridPosition, best, enemy.UnitId);
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

                if (enemy.Stats.IsAlive && nearest.Stats.IsAlive && TryStartAttack(enemy, nearest, allowPartialCost: true, out _))
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

            // 路径表现由 UnitManager 逐格驱动，格子真源写回 Grid，棋盘占用仍由 BoardManager 维护。
            var startCell = unit.Grid.GridPosition;
            var presenter = unit.GetComponent<BFUnitAnimationPresenter>();
            _activeMovingUnit = unit;
            unit.StateMachine.MoveState.SetTarget(path[^1]);
            unit.StateMachine.ChangeState(unit.StateMachine.MoveState);

            var previousCell = startCell;
            for (int i = 0; i < path.Count; i++)
            {
                if (unit == null || !unit.Stats.IsAlive || !unit.gameObject.activeInHierarchy) break;

                var nextCell = path[i];
                presenter?.FaceMovementStep(previousCell, nextCell);

                Vector3 fromWorld = unit.transform.position;
                Vector3 toWorld = (Vector3)_boardManager.CellToWorld(nextCell);
                float elapsed = 0f;

                while (elapsed < _secondsPerMoveCell)
                {
                    if (unit == null || !unit.Stats.IsAlive || !unit.gameObject.activeInHierarchy) break;

                    elapsed += Time.deltaTime;
                    float t = _secondsPerMoveCell <= 0f ? 1f : Mathf.Clamp01(elapsed / _secondsPerMoveCell);
                    unit.transform.position = Vector3.Lerp(fromWorld, toWorld, t);
                    yield return null;
                }

                if (unit == null || !unit.Stats.IsAlive || !unit.gameObject.activeInHierarchy) break;

                unit.transform.position = toWorld;
                previousCell = nextCell;
            }

            bool completed = unit != null && unit.Stats.IsAlive && unit.gameObject.activeInHierarchy && previousCell == path[^1];
            if (completed)
            {
                CompleteMove(unit, startCell, previousCell, path.Count, refreshPlayerLegalActions, clearSelectionWhenActed);
            }
            else if (unit != null && unit.Stats.IsAlive)
            {
                unit.StateMachine.ChangeState(unit.StateMachine.IdleState);
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
                   && unit.Stats.IsAlive
                   && (unit.StateMachine.CurrentState is UnitAttackState || unit.Combat.HasQueuedAttack || (_resolutionManager != null && _resolutionManager.HasPendingAttack(unit)))
                   && elapsed < timeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (elapsed >= timeoutSeconds)
            {
                Debug.LogWarning($"[BFBattleUnitManager] {unit.Identity.DisplayName} 攻击表现等待超时，请检查动画事件。");
            }
        }

        private bool TryGetMovePath(UnitRuntime unit, Vector2Int targetCell, out List<Vector2Int> path)
        {
            path = null;
            if (unit == null || _boardManager == null) return false;

            path = _boardManager.FindPath(unit.Grid.GridPosition, targetCell, unit.UnitId);
            if (path.Count == 0)
            {
                Debug.LogWarning($"[BFBattleUnitManager] 目标格子 {targetCell} 不可达。");
                return false;
            }

            if (path.Count > unit.Stats.RemainingActionPoints)
            {
                Debug.LogWarning($"[BFBattleUnitManager] 路径成本 {path.Count} 超过剩余 AP {unit.Stats.RemainingActionPoints}");
                return false;
            }

            return true;
        }

        private bool TryStartAttack(UnitRuntime attacker, UnitRuntime target, bool allowPartialCost, out int cost)
        {
            cost = 0;
            if (attacker == null || target == null || !attacker.Stats.IsAlive || !target.Stats.IsAlive) return false;

            // 攻击合法性在这里统一判定；Combat 只保存上下文，不重复计算阵营、距离或 AP。
            int distance = GetManhattanDistance(target.Grid.GridPosition, attacker.Grid.GridPosition);
            if (distance > attacker.Stats.AttackRange) return false;

            cost = allowPartialCost
                ? Mathf.Min(attacker.Stats.AttackCost, attacker.Stats.RemainingActionPoints)
                : attacker.Stats.AttackCost;
            if (cost <= 0 || attacker.Stats.RemainingActionPoints < cost) return false;

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

            if (!attacker.Combat.BeginQueuedAttack(target))
            {
                _resolutionManager.ClearPendingAttack(attacker);
                return false;
            }

            // 只有结算层和 Combat 都登记成功后，才进入 Attack 状态并扣 AP，避免失败路径消耗行动。
            attacker.StateMachine.AttackState.SetTarget(target);
            attacker.StateMachine.ChangeState(attacker.StateMachine.AttackState);
            attacker.Stats.ConsumeActionPoints(cost);
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
            // 完成移动时先同步棋盘占用，再写回单位 Grid，保证后续寻路读取到一致状态。
            _boardManager.ReleaseCell(startCell, unit.UnitId);
            _boardManager.OccupyCell(targetCell, unit.UnitId);
            unit.Grid.GridPosition = targetCell;
            unit.transform.position = (Vector3)_boardManager.CellToWorld(targetCell);
            unit.Stats.ConsumeActionPoints(moveCost);
            unit.StateMachine.ChangeState(unit.StateMachine.IdleState);

            RaiseUnitActionEvent(unit, "Moved", $"{targetCell.x},{targetCell.y}", moveCost);
            Debug.Log($"[BFBattleUnitManager] {unit.Identity.DisplayName} moved {moveCost} cells to {targetCell}, AP left: {unit.Stats.RemainingActionPoints}");

            if (clearSelectionWhenActed && SelectedUnit == unit && unit.Stats.HasActed)
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
            if (!_activeMovingUnit.Stats.IsAlive) return;

            _activeMovingUnit.transform.position = (Vector3)_boardManager.CellToWorld(_activeMovingUnit.Grid.GridPosition);
            _activeMovingUnit.StateMachine.ChangeState(_activeMovingUnit.StateMachine.IdleState);
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
                if (player == null || !player.Stats.IsAlive) continue;

                float distance = Vector2Int.Distance(enemy.Grid.GridPosition, player.Grid.GridPosition);
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
