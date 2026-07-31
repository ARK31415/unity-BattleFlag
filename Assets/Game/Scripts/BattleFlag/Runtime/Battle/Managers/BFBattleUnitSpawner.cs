using System.Collections.Generic;
using BF.Game.Runtime.Battle.Data;
using BF.Game.Runtime.Battle.Units;
using UnityEngine;

namespace BF.Game.Runtime.Battle.Managers
{
    /// <summary>
    /// 数据驱动单位生成入口（MonoBehaviour）。
    /// 从 Encounter 读取 Spawn Entry，实例化 Prefab，调用 UnitRuntime.InitializeFromDefinition。
    /// </summary>
    public class BFBattleUnitSpawner : MonoBehaviour
    {
        /// <summary>当前测试用关卡布阵 SO。</summary>
        [SerializeField] private BFBattleEncounterSO _encounter;
        /// <summary>单位生成配置 SO。</summary>
        [SerializeField] private BFUnitFactoryConfigSO _factoryConfig;
        /// <summary>生成单位的父节点，可为空。</summary>
        [SerializeField] private Transform _unitParent;

        private readonly List<UnitRuntime> _spawnedUnits = new();
        private bool _hasSpawned;

        /// <summary>已生成单位只读列表。</summary>
        public IReadOnlyList<UnitRuntime> SpawnedUnits => _spawnedUnits;
        /// <summary>是否有完整的生成配置。</summary>
        public bool HasSpawnConfig => _encounter != null && _factoryConfig != null;

        /// <summary>
        /// 使用 Inspector 绑定的配置生成 Encounter 中所有启用的单位。
        /// </summary>
        /// <param name="boardManager">棋盘管理器，用于格子到世界坐标的转换。</param>
        /// <param name="spawnedUnits">生成的单位列表。</param>
        /// <returns>true 表示所有启用的单位生成成功。</returns>
        public bool TrySpawnConfiguredEncounter(BFBattleBoardManager boardManager, out List<UnitRuntime> spawnedUnits)
        {
            return SpawnEncounter(_encounter, _factoryConfig, boardManager, out spawnedUnits);
        }

        /// <summary>
        /// 使用指定配置生成 Encounter 中所有启用的单位。
        /// 已生成过的场景不会重复生成（_hasSpawned 保护）。
        /// </summary>
        /// <param name="encounter">关卡布阵数据。</param>
        /// <param name="factoryConfig">单位生成配置。</param>
        /// <param name="boardManager">棋盘管理器。</param>
        /// <param name="spawnedUnits">生成的单位列表。</param>
        /// <returns>true 表示所有启用的单位生成成功。</returns>
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

        /// <summary>
        /// 根据单个 Spawn Entry 实例化 Prefab 并初始化 UnitRuntime。
        /// </summary>
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

        /// <summary>
        /// 将格子坐标转换为世界坐标。优先使用棋盘管理器的 CellToWorld，
        /// 否则使用原始格子坐标。
        /// </summary>
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
