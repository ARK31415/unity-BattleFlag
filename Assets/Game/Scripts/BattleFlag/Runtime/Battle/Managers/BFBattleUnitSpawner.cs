using System.Collections.Generic;
using BF.Game.Runtime.Battle.Data;
using BF.Game.Runtime.Battle.Units;
using UnityEngine;

namespace BF.Game.Runtime.Battle.Managers
{
    /// <summary>
    /// 根据 Encounter 和 Factory 配置生成单位根。
    /// </summary>
    public class BFBattleUnitSpawner : MonoBehaviour
    {
        [SerializeField] private BFBattleEncounterSO _encounter;
        [SerializeField] private BFUnitFactoryConfigSO _factoryConfig;
        [SerializeField] private Transform _unitParent;

        private readonly List<UnitRuntime> _spawnedUnits = new();
        private bool _hasSpawned;

        public IReadOnlyList<UnitRuntime> SpawnedUnits => _spawnedUnits;
        public bool HasSpawnConfig => _encounter != null && _factoryConfig != null;

        public bool TrySpawnConfiguredEncounter(BFBattleBoardManager boardManager, out List<UnitRuntime> spawnedUnits)
        {
            return SpawnEncounter(_encounter, _factoryConfig, boardManager, out spawnedUnits);
        }

        public bool SpawnEncounter(
            BFBattleEncounterSO encounter,
            BFUnitFactoryConfigSO factoryConfig,
            BFBattleBoardManager boardManager,
            out List<UnitRuntime> spawnedUnits)
        {
            spawnedUnits = new List<UnitRuntime>();
            if (_hasSpawned)
            {
                spawnedUnits.AddRange(_spawnedUnits);
                return true;
            }

            if (encounter == null || factoryConfig == null)
            {
                Debug.LogError("[BFBattleUnitSpawner] Encounter or factory config is missing.", this);
                return false;
            }

            for (int i = 0; i < encounter.SpawnEntries.Count; i++)
            {
                var entry = encounter.SpawnEntries[i];
                if (entry == null || !entry.IsEnabled) continue;

                if (!TrySpawnEntry(entry, factoryConfig, boardManager, out var unit))
                {
                    return false;
                }

                _spawnedUnits.Add(unit);
                spawnedUnits.Add(unit);
            }

            _hasSpawned = true;
            return true;
        }

        private bool TrySpawnEntry(
            BFBattleEncounterSpawnEntry entry,
            BFUnitFactoryConfigSO factoryConfig,
            BFBattleBoardManager boardManager,
            out UnitRuntime unit)
        {
            unit = null;
            var definition = entry.UnitDefinition;
            if (!factoryConfig.TryGetPrefab(definition, out var prefab, out string error))
            {
                Debug.LogError($"[BFBattleUnitSpawner] {error}", this);
                return false;
            }

            Vector3 worldPosition = ResolveSpawnWorldPosition(entry.GridPosition, boardManager);
            Transform parent = _unitParent != null ? _unitParent : transform;
            var instance = Instantiate(prefab, worldPosition, Quaternion.identity, parent);
            instance.name = string.IsNullOrWhiteSpace(definition.UnitId) ? prefab.name : definition.UnitId;

            if (!instance.TryGetComponent(out unit))
            {
                Debug.LogError($"[BFBattleUnitSpawner] Spawned prefab {instance.name} has no UnitRuntime.", instance);
                Destroy(instance);
                return false;
            }

            var config = definition.ImportedConfig;
            var faction = entry.ResolveFaction(config.DefaultFaction);
            unit.InitializeFromDefinition(definition, new BFUnitSpawnContext(entry.GridPosition, faction));
            return true;
        }

        private static Vector3 ResolveSpawnWorldPosition(Vector2Int gridPosition, BFBattleBoardManager boardManager)
        {
            if (boardManager != null && boardManager.Width > 0 && boardManager.Height > 0)
            {
                return (Vector3)boardManager.CellToWorld(gridPosition);
            }

            return new Vector3(gridPosition.x, gridPosition.y, 0f);
        }
    }
}
