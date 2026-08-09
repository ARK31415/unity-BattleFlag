using BF.Game.Battle.Domain.Events;
using BF.Game.Battle.Domain.Units;
using BF.Game.Runtime.Battle.Data;
using BF.Game.Runtime.Battle.Units;
using BF.Game.Battle.Rules.Units;
using DomainUnitRole = BF.Game.Battle.Domain.Units.BFUnitRole;

namespace BF.Game.Runtime.Battle.Factory
{
    /// <summary>
    /// 将 Unity 配置资产解析为通用单位创建请求。
    /// </summary>
    public sealed class BFUnitDefinitionResolver
    {
        private readonly BFUnitAttributesFactory _attributesFactory = new();

        /// <summary>
        /// 解析一个 Encounter 出场条目，不创建 Runtime 或修改战斗 Context。
        /// </summary>
        public bool TryResolve(
            BFBattleEncounterSpawnEntry entry,
            out BFBattleUnitCreateRequest request,
            out string error)
        {
            request = null;
            if (entry == null)
            {
                error = "Encounter entry is missing.";
                return false;
            }

            var definition = entry.UnitDefinition;
            if (definition == null)
            {
                error = "Encounter entry unit definition is missing.";
                return false;
            }

            if (!definition.ValidateConfiguration(out error))
                return false;

            var config = definition.ImportedConfig;
            if (config == null || string.IsNullOrWhiteSpace(config.ProfileId))
            {
                error = "Unit ProfileId is missing.";
                return false;
            }

            if (entry.UnitLevel < 1)
            {
                error = $"Unit {config.ProfileId} has invalid UnitLevel {entry.UnitLevel}.";
                return false;
            }

            var stats = definition.GetBaseStats();
            if (definition.TryGetProgressionStats(entry.UnitLevel, out var progressionStats))
                stats = progressionStats;

            var faction = ToDomainFaction(entry.ResolveFaction(config.DefaultFaction));
            if (faction == BFUnitFaction.None)
            {
                error = $"Unit {config.ProfileId} has no valid faction.";
                return false;
            }

            var attributes = _attributesFactory.Create(
                stats.MaxHP,
                stats.MaxActionPoints,
                stats.Attack,
                stats.AttackRange,
                stats.AttackCost);

            request = new BFBattleUnitCreateRequest(
                config.ProfileId,
                faction,
                ToDomainRole(config.Role),
                config.Tier,
                entry.UnitLevel,
                attributes,
                new BFGridPosition(entry.GridPosition.x, entry.GridPosition.y),
                definition,
                definition.UnityBinding,
                config.DisplayName);
            error = string.Empty;
            return true;
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

        private static DomainUnitRole ToDomainRole(BF.Game.Runtime.Battle.Units.BFUnitRole role)
        {
            return role switch
            {
                BF.Game.Runtime.Battle.Units.BFUnitRole.Mage => DomainUnitRole.Mage,
                _ => DomainUnitRole.Warrior
            };
        }
    }
}
