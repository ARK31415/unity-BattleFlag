using System;
using System.Collections.Generic;
using BF.Game.Battle.Domain;
using BF.Game.Battle.Domain.Events;
using BF.Game.Battle.Domain.Units;
using BF.Game.Runtime.Battle.Factory;
using BF.Game.Runtime.Battle.Flow;
using BF.Game.Runtime.Battle.Managers;
using BF.Game.Runtime.Battle.Units;
using UnityEngine;
using DomainBattleSession = BF.Game.Battle.Domain.BFBattleSession;

namespace BF.Game.Runtime.Battle.Input
{
    /// <summary>
    /// 选择适配协调器。
    ///
    /// SelectionController 只持有 RuntimeId；本组件负责验证当前会话、阶段、阵营、
    /// 存活状态和行动锁，避免输入层或 HUD 直接把 Unity Runtime 当作选择事实来源。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BFBattleSelectionCoordinator : MonoBehaviour
    {
        private BFBattleSelectionController _selectionController;
        private IBFBattleRuntimeLookup _runtimeLookup;
        private BFBattleActionCoordinator _actionCoordinator;
        private BFBattleTurnManager _turnManager;
        private BFBattleBoardManager _boardManager;
        private DomainBattleSession _battleSession;
        private readonly List<UnitRuntime> _subscribedUnits = new();

        public string SelectedRuntimeId => _selectionController?.SelectedRuntimeId;

        public void SetDependencies(
            BFBattleSelectionController selectionController,
            IBFBattleRuntimeLookup runtimeLookup,
            BFBattleActionCoordinator actionCoordinator,
            BFBattleTurnManager turnManager,
            BFBattleBoardManager boardManager,
            DomainBattleSession battleSession)
        {
            _selectionController = selectionController;
            _runtimeLookup = runtimeLookup;
            _actionCoordinator = actionCoordinator;
            _turnManager = turnManager;
            _boardManager = boardManager;
            _battleSession = battleSession;
        }

        /// <summary>
        /// 绑定本场 Runtime 的禁用生命周期，用于清理已经失效的选择身份。
        /// </summary>
        public void SetRuntimeUnits(IEnumerable<UnitRuntime> units)
        {
            UnsubscribeRuntimeUnits();
            if (units == null) return;

            foreach (var unit in units)
            {
                if (unit == null) continue;
                unit.Disabled += HandleUnitDisabled;
                _subscribedUnits.Add(unit);
            }
        }

        /// <summary>尝试选中一个玩家单位。</summary>
        public bool TrySelect(UnitRuntime runtime)
        {
            return runtime != null && TrySelect(runtime.RuntimeId);
        }

        /// <summary>按 RuntimeId 验证并记录选择。</summary>
        public bool TrySelect(string runtimeId)
        {
            if (_selectionController == null || _runtimeLookup == null || _battleSession == null ||
                _actionCoordinator == null || _actionCoordinator.IsActionLocked ||
                _boardManager == null || _boardManager.IsSyncFaulted ||
                _battleSession.State != BFBattleSessionState.Running ||
                _turnManager == null || _turnManager.CurrentPhase != BattlePhase.PlayerTurn ||
                string.IsNullOrWhiteSpace(runtimeId) ||
                !_runtimeLookup.TryGetRuntime(runtimeId, out var runtime) ||
                runtime == null || !runtime.gameObject.activeInHierarchy ||
                !runtime.IsRuleBound || !runtime.RuleState.IsAlive ||
                runtime.RuleState.Faction != BFUnitFaction.Player ||
                !_battleSession.Context.TryGetUnit(runtimeId, out var state) ||
                !ReferenceEquals(state, runtime.RuleState))
                return false;

            return _selectionController.TrySelect(runtimeId);
        }

        /// <summary>在没有行动锁时清除当前选择。</summary>
        public bool ClearSelection()
        {
            if (_actionCoordinator != null && _actionCoordinator.IsActionLocked)
                return false;

            return ClearSelectionIgnoringLock();
        }

        /// <summary>由行动完成或会话清理路径使用的强制清除入口。</summary>
        public bool ClearSelectionIgnoringLock()
        {
            return _selectionController != null && _selectionController.ClearSelection();
        }

        /// <summary>验证 RuntimeId 是否为当前选择。</summary>
        public bool IsSelected(string runtimeId)
        {
            return _selectionController != null && _selectionController.IsSelected(runtimeId);
        }

        private void OnDestroy()
        {
            UnsubscribeRuntimeUnits();
        }

        private void HandleUnitDisabled(UnitRuntime unit)
        {
            if (unit != null && IsSelected(unit.RuntimeId))
                ClearSelectionIgnoringLock();
        }

        private void UnsubscribeRuntimeUnits()
        {
            foreach (var unit in _subscribedUnits)
            {
                if (unit != null)
                    unit.Disabled -= HandleUnitDisabled;
            }

            _subscribedUnits.Clear();
        }
    }
}
