using System.Collections.Generic;
using BF.Game.Runtime.Battle.Events;
using BF.Game.Runtime.Battle.Flow;
using BF.Game.Runtime.Battle.Grid;
using BF.Game.Runtime.Battle.Units;
using UnityEngine;

namespace BF.Game.Runtime.Battle.Commands
{
    /// <summary>
    /// 战斗命令服务。行动点制：移动一格消耗 1 点，攻击消耗 1 点。
    /// </summary>
    public class BattleCommandService : MonoBehaviour
    {
        [SerializeField] private BattleStateController _stateController;
        [SerializeField] private BFGridManager _gridManager;
        [SerializeField] private BFUnitEventSO _unitEventChannel;

        public UnitRuntime SelectedUnit { get; private set; }
        public event System.Action<UnitRuntime> OnUnitSelected;
        public event System.Action<UnitRuntime> OnUnitDeselected;

        public bool TrySelectUnit(UnitRuntime unit)
        {
            if (unit == null || !unit.IsAlive) return false;
            if (_stateController != null && _stateController.CurrentPhase != BattlePhase.PlayerTurn) return false;
            if (unit.Faction != UnitFaction.Player) return false;

            DeselectUnit();
            SelectedUnit = unit;
            OnUnitSelected?.Invoke(unit);

            _unitEventChannel?.Raise(new BFUnitEventData { UnitId = unit.UnitId, EventType = "Selected" });
            Debug.Log($"[BattleCommand] 选中: {unit.DisplayName}, 行动点: {unit.RemainingActionPoints}");
            return true;
        }

        public void DeselectUnit()
        {
            if (SelectedUnit == null) return;
            var old = SelectedUnit;
            SelectedUnit = null;
            OnUnitDeselected?.Invoke(old);
            _unitEventChannel?.Raise(new BFUnitEventData { UnitId = old.UnitId, EventType = "Deselected" });
        }

        /// <summary>移动：每格消耗 1 行动点。</summary>
        public bool TryMoveUnit(Vector2Int targetCell)
        {
            if (SelectedUnit == null || SelectedUnit.HasActed) return false;
            if (_stateController == null || _stateController.CurrentPhase != BattlePhase.PlayerTurn) return false;
            if (_gridManager == null) { Debug.LogError("[BattleCommand] 未绑定 BFGridManager。"); return false; }

            int dist = Mathf.Abs(targetCell.x - SelectedUnit.GridPosition.x)
                     + Mathf.Abs(targetCell.y - SelectedUnit.GridPosition.y);

            if (dist > SelectedUnit.RemainingActionPoints)
            {
                Debug.LogWarning($"[BattleCommand] 距离 {dist} 超过剩余行动点 {SelectedUnit.RemainingActionPoints}");
                return false;
            }

            if (!_gridManager.IsCellReachable(SelectedUnit.GridPosition, targetCell, SelectedUnit.RemainingActionPoints, SelectedUnit.UnitId))
            {
                Debug.LogWarning($"[BattleCommand] 目标格子 {targetCell} 不可达。");
                return false;
            }

            _gridManager.ReleaseCell(SelectedUnit.GridPosition, SelectedUnit.UnitId);
            _gridManager.OccupyCell(targetCell, SelectedUnit.UnitId);
            SelectedUnit.GridPosition = targetCell;
            SelectedUnit.transform.position = (Vector3)_gridManager.CellToWorld(targetCell);
            SelectedUnit.ConsumeActionPoints(dist);

            _unitEventChannel?.Raise(new BFUnitEventData { UnitId = SelectedUnit.UnitId, EventType = "Moved", TargetId = $"{targetCell.x},{targetCell.y}", Value = dist });
            Debug.Log($"[BattleCommand] {SelectedUnit.DisplayName} 移动 {dist} 格到 {targetCell}, 剩余行动点: {SelectedUnit.RemainingActionPoints}");
            return true;
        }

        /// <summary>攻击：消耗 1 行动点。</summary>
        public bool TryAttack(UnitRuntime target)
        {
            if (SelectedUnit == null || SelectedUnit.HasActed) return false;
            if (target == null || !target.IsAlive || target.Faction == SelectedUnit.Faction) return false;
            if (_stateController == null || _stateController.CurrentPhase != BattlePhase.PlayerTurn) return false;

            int dist = Mathf.Abs(target.GridPosition.x - SelectedUnit.GridPosition.x)
                     + Mathf.Abs(target.GridPosition.y - SelectedUnit.GridPosition.y);
            if (dist > SelectedUnit.AttackRange)
            {
                Debug.LogWarning($"[BattleCommand] 目标不在攻击范围内（距离: {dist}）。");
                return false;
            }

            int damage = SelectedUnit.Attack;
            target.TakeDamage(damage);
            SelectedUnit.ConsumeActionPoints(1);

            _unitEventChannel?.Raise(new BFUnitEventData { UnitId = SelectedUnit.UnitId, EventType = "Attacked", TargetId = target.UnitId, Value = damage });
            _unitEventChannel?.Raise(new BFUnitEventData { UnitId = target.UnitId, EventType = "Damaged", TargetId = SelectedUnit.UnitId, Value = damage });
            if (!target.IsAlive)
                _unitEventChannel?.Raise(new BFUnitEventData { UnitId = target.UnitId, EventType = "Killed" });

            Debug.Log($"[BattleCommand] {SelectedUnit.DisplayName} 攻击 {target.DisplayName}, 伤害 {damage}, 剩余行动点: {SelectedUnit.RemainingActionPoints}");

            if (SelectedUnit.HasActed) DeselectUnit();
            _stateController?.CheckBattleEndCondition();
            return true;
        }

        public void EndTurn()
        {
            if (_stateController == null || _stateController.CurrentPhase != BattlePhase.PlayerTurn) return;
            DeselectUnit();
            _stateController.EndTurn();
            Debug.Log("[BattleCommand] 玩家结束回合。");
        }

        public List<Vector2Int> GetReachableCellsForSelected()
        {
            if (SelectedUnit == null || _gridManager == null) return new List<Vector2Int>();
            return _gridManager.GetReachableCells(SelectedUnit.GridPosition, SelectedUnit.RemainingActionPoints, SelectedUnit.UnitId);
        }

        public List<UnitRuntime> GetAttackableTargets()
        {
            var targets = new List<UnitRuntime>();
            if (SelectedUnit == null || _stateController?.Context == null) return targets;
            foreach (var u in _stateController.Context.Units)
            {
                if (u == null || !u.IsAlive || u == SelectedUnit || u.Faction == SelectedUnit.Faction) continue;
                int d = Mathf.Abs(u.GridPosition.x - SelectedUnit.GridPosition.x) + Mathf.Abs(u.GridPosition.y - SelectedUnit.GridPosition.y);
                if (d <= SelectedUnit.AttackRange) targets.Add(u);
            }
            return targets;
        }
    }
}
