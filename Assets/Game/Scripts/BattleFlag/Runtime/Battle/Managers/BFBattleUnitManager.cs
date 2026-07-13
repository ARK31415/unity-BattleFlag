using System;
using System.Collections.Generic;
using BF.Game.Runtime.Battle.Events;
using BF.Game.Runtime.Battle.Units;
using UnityEngine;

namespace BF.Game.Runtime.Battle.Managers
{
    /// <summary>
    /// 单位管理器。维护单位列表、管理选中状态、执行玩家命令、
    /// 驱动敌方 AI、判定胜负、判断玩家是否仍有合法操作（Spec 第 3-6 节）。
    /// 不负责棋盘绘制、阶段切换、玩家输入处理。
    /// </summary>
    public class BFBattleUnitManager : MonoBehaviour
    {
        [Header("Event Channels")]
        [SerializeField] private BFUnitEventSO _unitEventChannel;

        [Header("Dependencies")]
        [SerializeField] private BFBattleBoardManager _boardManager;
        [SerializeField] private BFBattleTurnManager _turnManager;
        [SerializeField] private BFBattleResolutionManager _resolutionManager;

        /// <summary>战场上所有单位。</summary>
        public List<UnitRuntime> AllUnits { get; private set; } = new();

        /// <summary>当前选中的玩家单位。</summary>
        public UnitRuntime SelectedUnit { get; private set; }

        /// <summary>战斗结果（全灭后设置）。</summary>
        public BattleResult Result { get; private set; }

        /// <summary>选中事件。</summary>
        public event Action<UnitRuntime> OnUnitSelected;
        /// <summary>取消选中事件。</summary>
        public event Action<UnitRuntime> OnUnitDeselected;
        /// <summary>战斗结束事件。</summary>
        public event Action<BattleResult> OnBattleEnded;

        // ============================================================
        // 单位注册
        // ============================================================

        /// <summary>注册单位到管理器。</summary>
        public void RegisterUnit(UnitRuntime unit)
        {
            if (unit == null || AllUnits.Contains(unit)) return;
            AllUnits.Add(unit);
            Debug.Log($"[BFBattleUnitManager] Registered: {unit.DisplayName} ({unit.Faction})");
        }

        // ============================================================
        // 阵营查询
        // ============================================================

        /// <summary>获取指定阵营的存活单位列表。</summary>
        public List<UnitRuntime> GetAliveUnitsByFaction(UnitFaction faction)
        {
            var result = new List<UnitRuntime>();
            foreach (var u in AllUnits)
            {
                if (u != null && u.Faction == faction && u.IsAlive)
                    result.Add(u);
            }
            return result;
        }

        /// <summary>重置所有单位回合行动点。</summary>
        public void ResetAllUnitsForNewTurn()
        {
            foreach (var unit in AllUnits)
            {
                unit?.ResetTurnActions();
            }
        }

        // ============================================================
        // 选中管理
        // ============================================================

        /// <summary>尝试选中单位。</summary>
        public bool TrySelectUnit(UnitRuntime unit)
        {
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

        /// <summary>取消选中。</summary>
        public void DeselectUnit()
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

        // ============================================================
        // 玩家命令：移动
        // ============================================================

        /// <summary>
        /// 尝试将选中单位移动到目标格子。
        /// 每格消耗 1 行动点。
        /// </summary>
        public bool TryMoveUnit(Vector2Int targetCell)
        {
            if (SelectedUnit == null || SelectedUnit.HasActed) return false;
            if (_turnManager != null && _turnManager.CurrentPhase != BattlePhase.PlayerTurn) return false;
            if (_boardManager == null)
            {
                Debug.LogError("[BFBattleUnitManager] BoardManager 未绑定。");
                return false;
            }

            int dist = Mathf.Abs(targetCell.x - SelectedUnit.GridPosition.x)
                     + Mathf.Abs(targetCell.y - SelectedUnit.GridPosition.y);

            if (dist > SelectedUnit.RemainingActionPoints)
            {
                Debug.LogWarning($"[BFBattleUnitManager] 距离 {dist} 超过剩余 AP {SelectedUnit.RemainingActionPoints}");
                return false;
            }

            if (!_boardManager.IsCellReachable(SelectedUnit.GridPosition, targetCell,
                    SelectedUnit.RemainingActionPoints, SelectedUnit.UnitId))
            {
                Debug.LogWarning($"[BFBattleUnitManager] 目标格子 {targetCell} 不可达。");
                return false;
            }

            _boardManager.ReleaseCell(SelectedUnit.GridPosition, SelectedUnit.UnitId);
            _boardManager.OccupyCell(targetCell, SelectedUnit.UnitId);
            SelectedUnit.GridPosition = targetCell;
            SelectedUnit.transform.position = (Vector3)_boardManager.CellToWorld(targetCell);
            SelectedUnit.ConsumeActionPoints(dist);

            _unitEventChannel?.Raise(new BFUnitEventData
            {
                UnitId = SelectedUnit.UnitId,
                EventType = "Moved",
                TargetId = $"{targetCell.x},{targetCell.y}",
                Value = dist
            });
            Debug.Log($"[BFBattleUnitManager] {SelectedUnit.DisplayName} moved {dist} to {targetCell}, AP left: {SelectedUnit.RemainingActionPoints}");

            if (SelectedUnit.HasActed) DeselectUnit();
            _turnManager?.RefreshPlayerLegalActions();
            return true;
        }

        // ============================================================
        // 玩家命令：攻击
        // ============================================================

        /// <summary>
        /// 尝试用选中单位攻击目标。
        /// 攻击消耗按职业：战士 2 点，法师 3 点（Spec 第 5 节）。
        /// </summary>
        public bool TryAttack(UnitRuntime target)
        {
            if (SelectedUnit == null || SelectedUnit.HasActed) return false;
            if (target == null || !target.IsAlive || target.Faction == SelectedUnit.Faction) return false;
            if (_turnManager != null && _turnManager.CurrentPhase != BattlePhase.PlayerTurn) return false;

            int dist = Mathf.Abs(target.GridPosition.x - SelectedUnit.GridPosition.x)
                     + Mathf.Abs(target.GridPosition.y - SelectedUnit.GridPosition.y);
            if (dist > SelectedUnit.AttackRange)
            {
                Debug.LogWarning($"[BFBattleUnitManager] 目标不在攻击范围内（距离: {dist}）。");
                return false;
            }

            int attackCost = SelectedUnit.AttackCost;
            if (SelectedUnit.RemainingActionPoints < attackCost)
            {
                Debug.LogWarning($"[BFBattleUnitManager] AP 不足：需 {attackCost}，剩余 {SelectedUnit.RemainingActionPoints}");
                return false;
            }

            if (_resolutionManager == null)
            {
                Debug.LogError("[BFBattleUnitManager] ResolutionManager 未绑定。");
                return false;
            }

            if (!_resolutionManager.TryQueueAttack(SelectedUnit, target))
            {
                Debug.LogWarning("[BFBattleUnitManager] 攻击登记失败。");
                return false;
            }

            SelectedUnit.ConsumeActionPoints(attackCost);

            _unitEventChannel?.Raise(new BFUnitEventData
            {
                UnitId = SelectedUnit.UnitId,
                EventType = "Attacked",
                TargetId = target.UnitId,
                Value = attackCost
            });

            Debug.Log($"[BFBattleUnitManager] {SelectedUnit.DisplayName} 发起攻击 -> {target.DisplayName}，AP 剩余: {SelectedUnit.RemainingActionPoints}");

            SelectedUnit.BeginQueuedAttack(target);

            if (SelectedUnit.HasActed) DeselectUnit();
            _turnManager?.RefreshPlayerLegalActions();
            return true;
        }

        // ============================================================
        // 可达格 / 可攻击目标查询
        // ============================================================

        /// <summary>获取选中单位的可达格列表。</summary>
        public List<Vector2Int> GetReachableCellsForSelected()
        {
            if (SelectedUnit == null || _boardManager == null) return new List<Vector2Int>();
            return _boardManager.GetReachableCells(
                SelectedUnit.GridPosition,
                SelectedUnit.RemainingActionPoints,
                SelectedUnit.UnitId);
        }

        /// <summary>获取选中单位的可攻击目标列表。</summary>
        public List<UnitRuntime> GetAttackableTargets()
        {
            var targets = new List<UnitRuntime>();
            if (SelectedUnit == null) return targets;

            foreach (var u in AllUnits)
            {
                if (u == null || !u.IsAlive || u == SelectedUnit || u.Faction == SelectedUnit.Faction)
                    continue;
                int d = Mathf.Abs(u.GridPosition.x - SelectedUnit.GridPosition.x)
                      + Mathf.Abs(u.GridPosition.y - SelectedUnit.GridPosition.y);
                if (d <= SelectedUnit.AttackRange)
                    targets.Add(u);
            }
            return targets;
        }

        // ============================================================
        // 合法操作检测（Spec 第 6 节）
        // ============================================================

        /// <summary>
        /// 判断玩家方是否仍有合法操作（任何存活单位还有可达格 或 可攻击目标）。
        /// </summary>
        public bool PlayerHasLegalAction()
        {
            var players = GetAliveUnitsByFaction(UnitFaction.Player);
            if (players.Count == 0) return false;

            var enemies = GetAliveUnitsByFaction(UnitFaction.Enemy);
            if (enemies.Count == 0) return false;

            foreach (var unit in players)
            {
                if (unit.RemainingActionPoints <= 0) continue;

                // 检查是否有可达格
                var reachable = _boardManager.GetReachableCells(
                    unit.GridPosition, unit.RemainingActionPoints, unit.UnitId);
                if (reachable.Count > 0) return true;

                // 检查是否有攻击目标在范围内 且 AP 足够发动攻击
                if (unit.RemainingActionPoints >= unit.AttackCost)
                {
                    foreach (var enemy in enemies)
                    {
                        int d = Mathf.Abs(enemy.GridPosition.x - unit.GridPosition.x)
                              + Mathf.Abs(enemy.GridPosition.y - unit.GridPosition.y);
                        if (d <= unit.AttackRange) return true;
                    }
                }
            }

            return false;
        }

        // ============================================================
        // 胜负判定
        // ============================================================

        /// <summary>检查战斗结束条件。</summary>
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

        // ============================================================
        // 敌方 AI（Spec 第 3 节）
        // ============================================================

        /// <summary>
        /// 执行敌方回合。每个敌方单位按列表顺序行动：
        /// 1. 优先攻击范围内的玩家目标
        /// 2. 否则向最近玩家目标移动
        /// 任一方全灭 → 判定胜负。
        /// </summary>
        public void ExecuteEnemyTurn()
        {
            var enemies = GetAliveUnitsByFaction(UnitFaction.Enemy);
            var players = GetAliveUnitsByFaction(UnitFaction.Player);

            if (enemies.Count == 0 || players.Count == 0)
            {
                CheckBattleEndCondition();
                return;
            }

            foreach (var enemy in enemies)
            {
                if (!enemy.IsAlive) continue;

                var nearest = FindNearestPlayer(enemy, players);
                if (nearest == null) break;

                // 优先攻击
                if (TryEnemyAttack(enemy, nearest))
                    continue;

                // 否则移动靠近
                if (enemy.MovementHandler == null) continue;

                var reachable = enemy.MovementHandler.GetReachableCells(
                    enemy.GridPosition, enemy.MaxActionPoints, enemy.UnitId);
                if (reachable.Count == 0) continue;

                Vector2Int start = enemy.GridPosition;
                Vector2Int best = FindBestReachableCell(reachable, nearest.GridPosition);

                enemy.MovementHandler.ReleaseCell(enemy.GridPosition, enemy.UnitId);
                enemy.MovementHandler.OccupyCell(best, enemy.UnitId);
                enemy.GridPosition = best;
                enemy.transform.position = (Vector3)_boardManager.CellToWorld(best);

                int moveDistance = Mathf.Abs(best.x - start.x) + Mathf.Abs(best.y - start.y);
                enemy.ConsumeActionPoints(moveDistance);
                Debug.Log($"[BattleAI] {enemy.DisplayName} moved {moveDistance} cells to {best}");

                // 移动后再尝试攻击
                TryEnemyAttack(enemy, nearest);
            }

            CheckBattleEndCondition();
            if (Result == null || !Result.HasResult)
                _turnManager?.EndTurn();
        }

        private UnitRuntime FindNearestPlayer(UnitRuntime enemy, List<UnitRuntime> players)
        {
            UnitRuntime nearest = null;
            float minDistance = float.MaxValue;

            foreach (var player in players)
            {
                if (!player.IsAlive) continue;
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

        private bool TryEnemyAttack(UnitRuntime enemy, UnitRuntime target)
        {
            if (enemy == null || target == null || !enemy.IsAlive || !target.IsAlive)
                return false;

            int distance = Mathf.Abs(target.GridPosition.x - enemy.GridPosition.x)
                         + Mathf.Abs(target.GridPosition.y - enemy.GridPosition.y);
            if (distance > enemy.AttackRange) return false;

            int cost = Mathf.Min(enemy.AttackCost, enemy.RemainingActionPoints);
            if (cost <= 0) return false;

            if (_resolutionManager == null)
            {
                Debug.LogError("[BFBattleUnitManager] ResolutionManager 未绑定。");
                return false;
            }

            if (!_resolutionManager.TryQueueAttack(enemy, target))
            {
                Debug.LogWarning("[BFBattleUnitManager] 敌方攻击登记失败。");
                return false;
            }

            enemy.ConsumeActionPoints(cost);
            enemy.BeginQueuedAttack(target);

            Debug.Log($"[BattleAI] {enemy.DisplayName} 发起攻击 -> {target.DisplayName}");

            return true;
        }

        /// <summary>
        /// 处理攻击结算完成后的事件（由 BFBattleResolutionManager 调用）。
        /// </summary>
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

            Debug.Log($"[BFBattleUnitManager] 攻击结算完成：{result.Attacker.DisplayName} -> {result.Target.DisplayName}，伤害 {result.FinalDamage}，目标剩余 HP {result.TargetRemainingHp}");

            CheckBattleEndCondition();
        }
    }
}
