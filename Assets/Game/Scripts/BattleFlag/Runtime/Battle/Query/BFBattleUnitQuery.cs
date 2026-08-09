using System;
using System.Collections.Generic;
using BF.Game.Battle.Domain;
using BF.Game.Battle.Domain.Events;
using BF.Game.Battle.Domain.Units;
using BF.Game.Runtime.Battle.Factory;
using BF.Game.Runtime.Battle.Units;
using UnityEngine;
using DomainBattleSession = BF.Game.Battle.Domain.BFBattleSession;

namespace BF.Game.Runtime.Battle.Query
{
    /// <summary>
    /// 将当前 BattleSession 的规则状态映射为表现查询结果。
    ///
    /// 这是适配层查询服务：可以读取 Runtime 的配置展示名，但 HP、AP、位置、
    /// 行动状态和存活状态必须从 Context 中的 BFUnitState 生成。
    /// </summary>
    public sealed class BFBattleUnitQuery : IBFBattleUnitQuery
    {
        private readonly DomainBattleSession _session;
        private readonly IBFBattleRuntimeLookup _runtimeLookup;

        public BFBattleUnitQuery(
            DomainBattleSession session,
            IBFBattleRuntimeLookup runtimeLookup)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _runtimeLookup = runtimeLookup ?? throw new ArgumentNullException(nameof(runtimeLookup));

            if (!string.Equals(_session.Context.BattleId, _runtimeLookup.BattleId, StringComparison.Ordinal))
                throw new ArgumentException("查询服务与 Runtime 注册表必须属于同一个 BattleId。", nameof(runtimeLookup));
        }

        public string BattleId => _session.Context.BattleId;

        public bool TryGetSnapshot(string runtimeId, out BFUnitViewSnapshot snapshot)
        {
            snapshot = default;
            if (string.IsNullOrWhiteSpace(runtimeId) ||
                !_runtimeLookup.TryGetRuntime(runtimeId, out var runtime) ||
                runtime == null ||
                !_session.Context.TryGetUnit(runtimeId, out var state) ||
                !ReferenceEquals(state, runtime.RuleState))
                return false;

            snapshot = CreateSnapshot(BattleId, state, runtime.Identity.DisplayName);
            return true;
        }

        public IReadOnlyList<BFUnitViewSnapshot> GetSnapshots()
        {
            var snapshots = new List<BFUnitViewSnapshot>(_runtimeLookup.Runtimes.Count);
            foreach (var runtime in _runtimeLookup.Runtimes)
            {
                if (runtime == null || !TryGetSnapshot(runtime.RuntimeId, out var snapshot))
                    continue;

                snapshots.Add(snapshot);
            }

            return snapshots;
        }

        /// <summary>获取当前会话内仍存活且属于指定阵营的 Runtime。</summary>
        public List<UnitRuntime> GetAliveRuntimesByFaction(UnitFaction faction)
        {
            var result = new List<UnitRuntime>();
            foreach (var runtime in _runtimeLookup.Runtimes)
            {
                if (runtime == null || !runtime.gameObject.activeInHierarchy ||
                    !TryGetSnapshot(runtime.RuntimeId, out var snapshot) ||
                    !snapshot.IsAlive ||
                    ToRuntimeFaction(snapshot.Faction) != faction)
                    continue;

                result.Add(runtime);
            }

            return result;
        }

        private static BFUnitViewSnapshot CreateSnapshot(
            string battleId,
            BFUnitState state,
            string displayName)
        {
            var attributes = state.Attributes;
            return new BFUnitViewSnapshot(
                battleId: battleId,
                profileId: state.ProfileId,
                runtimeId: state.RuntimeId,
                displayName: displayName,
                faction: state.Faction,
                role: state.Role,
                tier: state.Tier,
                unitLevel: state.UnitLevel,
                currentHP: attributes.CurrentHP,
                maxHP: attributes.EffectiveMaxHP,
                attack: attributes.EffectiveAttackPower,
                attackRange: attributes.EffectiveAttackRange,
                attackCost: attributes.EffectiveAttackCost,
                remainingActionPoints: attributes.RemainingActionPoints,
                maxActionPoints: attributes.EffectiveMaxActionPoints,
                gridPosition: state.GridPosition,
                actionState: state.ActionState,
                isAlive: state.IsAlive);
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
    }
}
