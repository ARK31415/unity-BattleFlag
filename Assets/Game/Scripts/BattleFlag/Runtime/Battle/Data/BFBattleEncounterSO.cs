using System;
using System.Collections.Generic;
using BF.Game.Runtime.Battle.Units;
using UnityEngine;

namespace BF.Game.Runtime.Battle.Data
{
    /// <summary>
    /// 战斗关卡强相关布阵数据。
    /// </summary>
    [CreateAssetMenu(fileName = "BFBattleEncounter", menuName = "BF/Battle/Encounters/Battle Encounter")]
    public class BFBattleEncounterSO : ScriptableObject
    {
        [SerializeField] private List<BFBattleEncounterSpawnEntry> _spawnEntries = new();

        public IReadOnlyList<BFBattleEncounterSpawnEntry> SpawnEntries => _spawnEntries;
    }

    [Serializable]
    public class BFBattleEncounterSpawnEntry
    {
        [SerializeField] private BFUnitDefinitionSO _unitDefinition;
        [SerializeField] private Vector2Int _gridPosition;
        [SerializeField] private UnitFaction _factionOverride = UnitFaction.None;
        [SerializeField] private bool _isEnabled = true;

        public BFUnitDefinitionSO UnitDefinition => _unitDefinition;
        public Vector2Int GridPosition => _gridPosition;
        public UnitFaction FactionOverride => _factionOverride;
        public bool IsEnabled => _isEnabled;
        public bool HasFactionOverride => _factionOverride != UnitFaction.None;

        public UnitFaction ResolveFaction(UnitFaction defaultFaction)
        {
            return HasFactionOverride ? _factionOverride : defaultFaction;
        }
    }
}
