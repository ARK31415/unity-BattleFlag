using System.Collections.Generic;
using BF.Game.Runtime.Battle.Units;
using UnityEngine;

namespace BF.Game.Runtime.Battle.Data
{
    /// <summary>
    /// 单位策划基础配置层。
    /// </summary>
    [CreateAssetMenu(fileName = "BFUnitImportedConfig", menuName = "BF/Battle/Units/Imported Config")]
    public class BFUnitImportedConfigSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _unitId = "unit_001";
        [SerializeField] private string _displayName = "Unit";
        [SerializeField] private UnitFaction _defaultFaction = UnitFaction.Player;
        [SerializeField] private BFUnitRole _role = BFUnitRole.Warrior;

        [Header("Base Stats")]
        [SerializeField] private BFUnitStatBlock _baseStats = BFUnitStatBlock.Default;

        [Header("Skill References")]
        [SerializeField] private List<string> _skillIds = new();

        public string UnitId => string.IsNullOrWhiteSpace(_unitId) ? name : _unitId;
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? UnitId : _displayName;
        public UnitFaction DefaultFaction => _defaultFaction;
        public BFUnitRole Role => _role;
        public BFUnitStatBlock BaseStats => _baseStats;
        public IReadOnlyList<string> SkillIds => _skillIds;
    }
}
