using System.Collections.Generic;
using BF.Game.Runtime.Battle.Units;
using UnityEngine;
using UnityEngine.Serialization;
using DomainUnitTier = BF.Game.Battle.Domain.Units.BFUnitTier;

namespace BF.Game.Runtime.Battle.Data
{
    /// <summary>
    /// 单位策划基础配置层（ScriptableObject）。
    /// 拥有 ProfileId、DisplayName、默认阵营、职业、基础属性和技能 ID。
    /// 第一版手动编辑 SO，后续作为 Excel / JSON 导入目标。
    /// </summary>
    [CreateAssetMenu(fileName = "BFUnitImportedConfig", menuName = "BF/Battle/Units/Imported Config")]
    public class BFUnitImportedConfigSO : ScriptableObject
    {
        [Header("Identity")]
        /// <summary>稳定配置身份；为空时视为无效配置。</summary>
        [FormerlySerializedAs("_unitId")]
        [SerializeField] private string _profileId = "unit_001";
        /// <summary>第一版展示名，空值时回退到 ProfileId。</summary>
        [SerializeField] private string _displayName = "Unit";
        /// <summary>默认阵营，关卡 Spawn Entry 可覆盖。</summary>
        [SerializeField] private UnitFaction _defaultFaction = UnitFaction.Player;
        /// <summary>单位职业（Warrior / Mage）。</summary>
        [SerializeField] private BFUnitRole _role = BFUnitRole.Warrior;
        /// <summary>单位层级或品质（Normal / Elite / Boss）。</summary>
        [SerializeField] private DomainUnitTier _tier = DomainUnitTier.Normal;

        [Header("Base Stats")]
        /// <summary>基础战斗白值包。</summary>
        [SerializeField] private BFUnitStatBlock _baseStats = BFUnitStatBlock.Default;

        [Header("Skill References")]
        /// <summary>技能 ID 列表（第一版只记录不实现）。</summary>
        [SerializeField] private List<string> _skillIds = new();

        /// <summary>稳定配置身份。</summary>
        public string ProfileId => _profileId;
        /// <summary>展示名，空值时回退到 ProfileId。</summary>
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? ProfileId : _displayName;
        /// <summary>默认阵营，关卡可覆盖。</summary>
        public UnitFaction DefaultFaction => _defaultFaction;
        /// <summary>单位职业。</summary>
        public BFUnitRole Role => _role;
        /// <summary>单位层级或品质。</summary>
        public DomainUnitTier Tier => _tier;
        /// <summary>基础战斗白值。</summary>
        public BFUnitStatBlock BaseStats => _baseStats;
        /// <summary>技能 ID 只读列表。</summary>
        public IReadOnlyList<string> SkillIds => _skillIds;
    }
}
