using System;
using BF.Game.Battle.Domain.Events;
using BF.Game.Battle.Domain.Units;

namespace BF.Game.Battle.Rules.Units
{
    /// <summary>
    /// 创建单位规则状态所需的、已经完成配置解析的纯规则数据。
    /// </summary>
    public readonly struct BFUnitStateCreationData
    {
        /// <summary>
        /// 创建规则状态数据。
        /// </summary>
        public BFUnitStateCreationData(
            string profileId,
            BFUnitFaction faction,
            BFUnitRole role,
            BFUnitTier tier,
            int unitLevel,
            BFUnitAttributes attributes,
            BFGridPosition gridPosition)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                throw new ArgumentException("ProfileId 不能为空。", nameof(profileId));
            if (unitLevel < 1)
                throw new ArgumentOutOfRangeException(nameof(unitLevel), unitLevel, "单位等级必须大于等于 1。");
            if (faction != BFUnitFaction.Player && faction != BFUnitFaction.Enemy)
                throw new ArgumentException("单位阵营必须为 Player 或 Enemy。", nameof(faction));

            ProfileId = profileId;
            Faction = faction;
            Role = role;
            Tier = tier;
            UnitLevel = unitLevel;
            Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
            GridPosition = gridPosition;
        }

        /// <summary>单位配置身份。</summary>
        public string ProfileId { get; }

        /// <summary>单位最终阵营。</summary>
        public BFUnitFaction Faction { get; }

        /// <summary>单位战斗角色。</summary>
        public BFUnitRole Role { get; }

        /// <summary>单位层级或品质。</summary>
        public BFUnitTier Tier { get; }

        /// <summary>单位规则等级。</summary>
        public int UnitLevel { get; }

        /// <summary>单位规则属性和当前资源。</summary>
        public BFUnitAttributes Attributes { get; }

        /// <summary>单位规则网格位置。</summary>
        public BFGridPosition GridPosition { get; }
    }
}
