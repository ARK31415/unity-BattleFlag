using System;
using System.Collections.Generic;
using BF.Game.Runtime.Battle.Units;
using UnityEngine;

namespace BF.Game.Runtime.Battle.Data
{
    /// <summary>
    /// 战斗关卡强相关布阵数据（ScriptableObject）。
    /// 保存 Spawn Entry 列表，每个 Entry 绑定单位定义、出生格、阵营覆盖和启用状态。
    /// </summary>
    [CreateAssetMenu(fileName = "BFBattleEncounter", menuName = "BF/Battle/Encounters/Battle Encounter")]
    public class BFBattleEncounterSO : ScriptableObject
    {
        [Header("Identity")]
        /// <summary>战斗关卡配置身份；为空时视为无效配置。</summary>
        [SerializeField] private string _encounterId = "encounter_001";

        /// <summary>出场条目列表。</summary>
        [SerializeField] private List<BFBattleEncounterSpawnEntry> _spawnEntries = new();

        /// <summary>战斗关卡配置身份。</summary>
        public string EncounterId => _encounterId;

        /// <summary>出场条目只读列表。</summary>
        public IReadOnlyList<BFBattleEncounterSpawnEntry> SpawnEntries => _spawnEntries;
    }

    /// <summary>
    /// 单个出场条目（可序列化类）。
    /// 绑定 Definition + GridPosition + 阵营覆盖 + 启用标志。
    /// </summary>
    [Serializable]
    public class BFBattleEncounterSpawnEntry
    {
        /// <summary>引用的单位定义。</summary>
        [SerializeField] private BFUnitDefinitionSO _unitDefinition;
        /// <summary>出生格坐标。</summary>
        [SerializeField] private Vector2Int _gridPosition;
        /// <summary>单位规则等级，未配置时使用 1。</summary>
        [SerializeField] private int _unitLevel = 1;
        /// <summary>阵营覆盖（None 表示使用 Config 默认阵营）。</summary>
        [SerializeField] private UnitFaction _factionOverride = UnitFaction.None;
        /// <summary>是否启用此出场条目。</summary>
        [SerializeField] private bool _isEnabled = true;

        /// <summary>引用的单位定义。</summary>
        public BFUnitDefinitionSO UnitDefinition => _unitDefinition;
        /// <summary>出生格坐标。</summary>
        public Vector2Int GridPosition => _gridPosition;
        /// <summary>单位规则等级。</summary>
        public int UnitLevel => _unitLevel;
        /// <summary>阵营覆盖值。</summary>
        public UnitFaction FactionOverride => _factionOverride;
        /// <summary>是否启用。</summary>
        public bool IsEnabled => _isEnabled;
        /// <summary>是否有阵营覆盖（FactionOverride != None）。</summary>
        public bool HasFactionOverride => _factionOverride != UnitFaction.None;

        /// <summary>
        /// 解析最终阵营：优先使用覆盖值，否则回退到默认阵营。
        /// </summary>
        /// <param name="defaultFaction">Config 中的默认阵营。</param>
        /// <returns>最终生效的阵营。</returns>
        public UnitFaction ResolveFaction(UnitFaction defaultFaction)
        {
            return HasFactionOverride ? _factionOverride : defaultFaction;
        }
    }
}
