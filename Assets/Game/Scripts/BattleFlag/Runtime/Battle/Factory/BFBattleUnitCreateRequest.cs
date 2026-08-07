using System;
using BF.Game.Battle.Domain.Events;
using BF.Game.Battle.Domain.Units;
using BF.Game.Runtime.Battle.Data;

namespace BF.Game.Runtime.Battle.Factory
{
    /// <summary>
    /// 已完成 Encounter/Profile 解析的通用单位创建请求。
    /// </summary>
    public sealed class BFBattleUnitCreateRequest
    {
        /// <summary>
        /// 创建一个规则数据与表现配置一致的通用请求。
        /// </summary>
        public BFBattleUnitCreateRequest(
            string profileId,
            BFUnitFaction faction,
            BFUnitRole role,
            BFUnitTier tier,
            int unitLevel,
            BFUnitAttributes attributes,
            BFGridPosition gridPosition,
            BFUnitDefinitionSO definition,
            BFUnitStatBlock combatStats,
            BFUnitUnityBindingSO unityBinding,
            string displayName)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                throw new ArgumentException("ProfileId 不能为空。", nameof(profileId));
            if (faction != BFUnitFaction.Player && faction != BFUnitFaction.Enemy)
                throw new ArgumentException("单位阵营必须为 Player 或 Enemy。", nameof(faction));
            if (unitLevel < 1)
                throw new ArgumentOutOfRangeException(nameof(unitLevel), unitLevel, "单位等级必须大于等于 1。");
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (unityBinding == null) throw new ArgumentNullException(nameof(unityBinding));

            ProfileId = profileId;
            Faction = faction;
            Role = role;
            Tier = tier;
            UnitLevel = unitLevel;
            Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
            GridPosition = gridPosition;
            Definition = definition;
            CombatStats = combatStats;
            UnityBinding = unityBinding;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? profileId : displayName;
        }

        /// <summary>单位配置身份。</summary>
        public string ProfileId { get; }

        /// <summary>单位最终阵营。</summary>
        public BFUnitFaction Faction { get; }

        /// <summary>单位规则角色。</summary>
        public BFUnitRole Role { get; }

        /// <summary>单位品质层级。</summary>
        public BFUnitTier Tier { get; }

        /// <summary>单位规则等级。</summary>
        public int UnitLevel { get; }

        /// <summary>单位规则属性。</summary>
        public BFUnitAttributes Attributes { get; }

        /// <summary>单位规则网格位置。</summary>
        public BFGridPosition GridPosition { get; }

        /// <summary>Unity 单位定义配置。</summary>
        public BFUnitDefinitionSO Definition { get; }

        /// <summary>表现层仍需读取的攻击范围、攻击消耗等兼容数据。</summary>
        public BFUnitStatBlock CombatStats { get; }

        /// <summary>表现资源绑定配置。</summary>
        public BFUnitUnityBindingSO UnityBinding { get; }

        /// <summary>单位展示名。</summary>
        public string DisplayName { get; }
    }
}
