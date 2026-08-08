using System.Collections.Generic;
using System.Reflection;
using BF.Game.Battle.Domain;
using BF.Game.Battle.Domain.Events;
using BF.Game.Battle.Domain.Units;
using BF.Game.Runtime.Battle.Data;
using BF.Game.Runtime.Battle.Factory;
using BF.Game.Runtime.Battle.Managers;
using BF.Game.Runtime.Battle.Units;
using NUnit.Framework;
using Pathfinding;
using UnityEditor;
using UnityEngine;
using RuntimeUnitRole = BF.Game.Runtime.Battle.Units.BFUnitRole;

namespace BF.Game.Tests.EditMode.Battle
{
    /// <summary>
    /// 验证 Encounter 驱动的完整单位创建、跨层绑定和失败回滚合同。
    /// </summary>
    public sealed class BFBattleUnitFactoryTests
    {
        private readonly List<Object> _createdObjects = new();
        private BFBattleUnitFactory _factory;
        private BFBattleSession _session;

        [TearDown]
        public void TearDown()
        {
            _factory?.Dispose();
            _factory = null;
            _session?.Dispose();
            _session = null;

            for (var index = 0; index < _createdObjects.Count; index++)
            {
                if (_createdObjects[index] != null)
                    Object.DestroyImmediate(_createdObjects[index]);
            }

            _createdObjects.Clear();
        }

        [Test]
        public void CreateEncounter_BindsRuleStateRuntimeRegistryAndBoardOccupancy()
        {
            var board = CreateScannedBoard(4, 4);
            var prefab = CreateUnitPrefab("DefaultUnitPrefab");
            var definition = CreateDefinition(
                CreateImportedConfig(
                    "unit_001",
                    "先锋",
                    UnitFaction.Player,
                    RuntimeUnitRole.Warrior,
                    new BFUnitStatBlock(30, 8, 1, 2, 6)),
                CreateUnityBinding());
            var encounter = CreateEncounter(definition, new Vector2Int(1, 2), UnitFaction.None, 3);
            var factoryConfig = CreateFactoryConfig(prefab);
            _session = new BFBattleSession(new BFBattleContext("battle_factory_test"));
            var registry = new BFUnitRegistry(_session.Context.BattleId);
            _factory = new BFBattleUnitFactory(
                _session,
                registry,
                factoryConfig,
                board,
                null);

            var result = _factory.CreateEncounter(encounter);

            Assert.That(result.Succeeded, Is.True, result.Error);
            Assert.That(result.Units, Has.Count.EqualTo(1));

            var created = result.Units[0];
            Assert.That(created.Handle.BattleId, Is.EqualTo("battle_factory_test"));
            Assert.That(created.Handle.RuntimeId, Is.EqualTo("battle_factory_test_unit_0001"));
            Assert.That(_session.Context.TryGetUnit(created.Handle.RuntimeId, out var state), Is.True);
            Assert.That(state.ProfileId, Is.EqualTo("unit_001"));
            Assert.That(state.UnitLevel, Is.EqualTo(3));
            Assert.That(state.Attributes.CurrentHP, Is.EqualTo(state.Attributes.EffectiveMaxHP));
            Assert.That(registry.Count, Is.EqualTo(1));
            Assert.That(registry.TryGetRuntime(created.Handle, out var runtime), Is.True);
            Assert.That(runtime, Is.SameAs(created.Runtime));
            Assert.That(runtime.IsRuleBound, Is.True);
            Assert.That(runtime.RuntimeId, Is.EqualTo(created.Handle.RuntimeId));
            Assert.That(runtime.Identity.DisplayName, Is.EqualTo("先锋"));
            Assert.That(runtime.gameObject.name, Is.EqualTo("Unit_Player_Warrior_01"));
            Assert.That(runtime.gameObject.name, Does.Not.Contain(created.Handle.BattleId));
            Assert.That(runtime.gameObject.name, Does.Not.Contain(created.Handle.RuntimeId));
            Assert.That(runtime.Grid.GridPosition, Is.EqualTo(new Vector2Int(1, 2)));
            Assert.That(board.IsCellOccupied(new Vector2Int(1, 2)), Is.True);
            Assert.That(board.GetOccupant(new Vector2Int(1, 2)), Is.EqualTo(created.Handle.RuntimeId));
            Assert.That(registry.TryRegister(created.Handle, created.Runtime), Is.False);
            var foreignHandle = new BFBattleUnitHandle("other_battle", created.Handle.RuntimeId);
            Assert.That(registry.TryGetRuntime(foreignHandle, out _), Is.False);
            Assert.That(registry.TryUnregister(foreignHandle), Is.False);

            var foreignRuntime = Object.Instantiate(created.Runtime.gameObject).GetComponent<UnitRuntime>();
            _createdObjects.Add(foreignRuntime.gameObject);
            foreignRuntime.BindRuleState(
                state,
                definition.UnityBinding,
                "foreign runtime",
                foreignHandle,
                definition);
            var currentBattleHandle = new BFBattleUnitHandle(
                _session.Context.BattleId,
                created.Handle.RuntimeId);
            Assert.That(registry.TryRegister(currentBattleHandle, foreignRuntime), Is.False);
        }

        [Test]
        public void CreateEncounter_WhenRuntimeCreationFails_RollsBackAllRuleAndBoardState()
        {
            var board = CreateScannedBoard(4, 4);
            var prefab = CreateUnitPrefab("DefaultUnitPrefab");
            var definition = CreateDefinition(
                CreateImportedConfig(
                    "unit_002",
                    "失败单位",
                    UnitFaction.Enemy,
                    RuntimeUnitRole.Warrior,
                    BFUnitStatBlock.Default),
                CreateUnityBinding());
            var encounter = CreateEncounter(definition, new Vector2Int(2, 1), UnitFaction.None, 1);
            var factoryConfig = CreateFactoryConfig(prefab);
            var provider = new FailingRuntimeProvider();
            _session = new BFBattleSession(new BFBattleContext("battle_rollback_test"));
            var registry = new BFUnitRegistry(_session.Context.BattleId);
            _factory = new BFBattleUnitFactory(
                _session,
                registry,
                factoryConfig,
                board,
                null,
                provider);

            var result = _factory.CreateEncounter(encounter);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain("runtime creation failed"));
            Assert.That(provider.CreateCount, Is.EqualTo(1));
            Assert.That(provider.ReleaseCount, Is.EqualTo(0));
            Assert.That(_session.Context.Units, Is.Empty);
            Assert.That(registry.Count, Is.EqualTo(0));
            Assert.That(board.IsCellOccupied(new Vector2Int(2, 1)), Is.False);
        }

        [Test]
        public void CreateEncounter_RejectsDuplicatePositionsBeforeCreatingAnyRuntime()
        {
            var board = CreateScannedBoard(4, 4);
            var prefab = CreateUnitPrefab("DefaultUnitPrefab");
            var definition = CreateDefinition(
                CreateImportedConfig(
                    "unit_003",
                    "重复位置单位",
                    UnitFaction.Player,
                    RuntimeUnitRole.Warrior,
                    BFUnitStatBlock.Default),
                CreateUnityBinding());
            var encounter = CreateEncounterWithDuplicatePositions(definition);
            var factoryConfig = CreateFactoryConfig(prefab);
            var provider = new RecordingRuntimeProvider();
            _session = new BFBattleSession(new BFBattleContext("battle_duplicate_test"));
            var registry = new BFUnitRegistry(_session.Context.BattleId);
            _factory = new BFBattleUnitFactory(
                _session,
                registry,
                factoryConfig,
                board,
                null,
                provider);

            var result = _factory.CreateEncounter(encounter);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain("duplicate grid position"));
            Assert.That(provider.CreateCount, Is.EqualTo(0));
            Assert.That(_session.Context.Units, Is.Empty);
            Assert.That(registry.Count, Is.EqualTo(0));
        }

        [Test]
        public void CreateEncounter_FailureRollsBackOnlyCurrentBatch()
        {
            var board = CreateScannedBoard(4, 4);
            var prefab = CreateUnitPrefab("DefaultUnitPrefab");
            var definition = CreateDefinition(
                CreateImportedConfig(
                    "unit_004",
                    "批量单位",
                    UnitFaction.Player,
                    RuntimeUnitRole.Warrior,
                    BFUnitStatBlock.Default),
                CreateUnityBinding());
            var factoryConfig = CreateFactoryConfig(prefab);
            var provider = new FailOnCreateNumberRuntimeProvider(3);
            _session = new BFBattleSession(new BFBattleContext("battle_batch_test"));
            var registry = new BFUnitRegistry(_session.Context.BattleId);
            _factory = new BFBattleUnitFactory(
                _session,
                registry,
                factoryConfig,
                board,
                null,
                provider);

            var firstResult = _factory.CreateEncounter(
                CreateEncounter(definition, new Vector2Int(0, 0), UnitFaction.None, 1));
            Assert.That(firstResult.Succeeded, Is.True, firstResult.Error);
            var existingHandle = firstResult.Units[0].Handle;

            var secondResult = _factory.CreateEncounter(
                CreateEncounterWithPositions(
                    definition,
                    new Vector2Int(1, 0),
                    new Vector2Int(1, 1)));

            Assert.That(secondResult.Succeeded, Is.False);
            Assert.That(secondResult.Error, Does.Contain("runtime creation failed"));
            Assert.That(_session.Context.TryGetUnit(existingHandle.RuntimeId, out _), Is.True);
            Assert.That(registry.Count, Is.EqualTo(1));
            Assert.That(board.GetOccupant(new Vector2Int(0, 0)), Is.EqualTo(existingHandle.RuntimeId));
            Assert.That(board.IsCellOccupied(new Vector2Int(1, 0)), Is.False);
            Assert.That(board.IsCellOccupied(new Vector2Int(1, 1)), Is.False);
            Assert.That(provider.ReleaseCount, Is.EqualTo(1));
        }

        [Test]
        public void CreateEncounter_RejectsMissingEntry()
        {
            var board = CreateScannedBoard(4, 4);
            var factoryConfig = CreateFactoryConfig(CreateUnitPrefab("DefaultUnitPrefab"));
            var encounter = CreateScriptableObject<BFBattleEncounterSO>();
            typeof(BFBattleEncounterSO)
                .GetField("_spawnEntries", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(encounter, new List<BFBattleEncounterSpawnEntry> { null });
            _session = new BFBattleSession(new BFBattleContext("battle_missing_entry_test"));
            var registry = new BFUnitRegistry(_session.Context.BattleId);
            _factory = new BFBattleUnitFactory(
                _session,
                registry,
                factoryConfig,
                board,
                null);

            var result = _factory.CreateEncounter(encounter);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain("entry at index 0 is missing"));
            Assert.That(_session.Context.Units, Is.Empty);
            Assert.That(registry.Count, Is.EqualTo(0));
        }

        [Test]
        public void CreateEncounter_RejectsMissingEncounterId()
        {
            var board = CreateScannedBoard(4, 4);
            var prefab = CreateUnitPrefab("DefaultUnitPrefab");
            var definition = CreateDefinition(
                CreateImportedConfig(
                    "unit_missing_encounter_id",
                    "无效战斗配置单位",
                    UnitFaction.Player,
                    RuntimeUnitRole.Warrior,
                    BFUnitStatBlock.Default),
                CreateUnityBinding());
            var encounter = CreateEncounter(definition, new Vector2Int(1, 1), UnitFaction.None, 1);
            var encounterSerializedObject = new SerializedObject(encounter);
            encounterSerializedObject.FindProperty("_encounterId").stringValue = string.Empty;
            encounterSerializedObject.ApplyModifiedPropertiesWithoutUndo();
            var factoryConfig = CreateFactoryConfig(prefab);
            _session = new BFBattleSession(new BFBattleContext("battle_missing_encounter_id_test"));
            var registry = new BFUnitRegistry(_session.Context.BattleId);
            _factory = new BFBattleUnitFactory(
                _session,
                registry,
                factoryConfig,
                board,
                null);

            var result = _factory.CreateEncounter(encounter);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain("EncounterId is missing"));
            Assert.That(_session.Context.Units, Is.Empty);
            Assert.That(registry.Count, Is.EqualTo(0));
        }

        [Test]
        public void CreateEncounter_RejectsEmptySpawnEntries()
        {
            var board = CreateScannedBoard(4, 4);
            var factoryConfig = CreateFactoryConfig(CreateUnitPrefab("DefaultUnitPrefab"));
            var encounter = CreateScriptableObject<BFBattleEncounterSO>();
            _session = new BFBattleSession(new BFBattleContext("battle_empty_entries_test"));
            var registry = new BFUnitRegistry(_session.Context.BattleId);
            _factory = new BFBattleUnitFactory(
                _session,
                registry,
                factoryConfig,
                board,
                null);

            var result = _factory.CreateEncounter(encounter);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain("no spawn entries"));
            Assert.That(_session.Context.Units, Is.Empty);
            Assert.That(registry.Count, Is.EqualTo(0));
        }

        [Test]
        public void CreateEncounter_RejectsNullSpawnEntriesCollection()
        {
            var board = CreateScannedBoard(4, 4);
            var factoryConfig = CreateFactoryConfig(CreateUnitPrefab("DefaultUnitPrefab"));
            var encounter = CreateScriptableObject<BFBattleEncounterSO>();
            typeof(BFBattleEncounterSO)
                .GetField("_spawnEntries", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(encounter, null);
            _session = new BFBattleSession(new BFBattleContext("battle_null_entries_test"));
            var registry = new BFUnitRegistry(_session.Context.BattleId);
            _factory = new BFBattleUnitFactory(
                _session,
                registry,
                factoryConfig,
                board,
                null);

            var result = _factory.CreateEncounter(encounter);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain("spawn entries are missing"));
            Assert.That(_session.Context.Units, Is.Empty);
            Assert.That(registry.Count, Is.EqualTo(0));
        }

        private T CreateScriptableObject<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _createdObjects.Add(asset);
            return asset;
        }

        private GameObject CreateGameObject(string name)
        {
            var gameObject = new GameObject(name);
            _createdObjects.Add(gameObject);
            return gameObject;
        }

        private GameObject CreateUnitPrefab(string name)
        {
            var prefab = CreateGameObject(name);
            prefab.AddComponent<UnitRuntime>();
            return prefab;
        }

        private BFUnitImportedConfigSO CreateImportedConfig(
            string profileId,
            string displayName,
            UnitFaction faction,
            RuntimeUnitRole role,
            BFUnitStatBlock stats)
        {
            var config = CreateScriptableObject<BFUnitImportedConfigSO>();
            var serializedObject = new SerializedObject(config);
            serializedObject.FindProperty("_profileId").stringValue = profileId;
            serializedObject.FindProperty("_displayName").stringValue = displayName;
            serializedObject.FindProperty("_defaultFaction").intValue = (int)faction;
            serializedObject.FindProperty("_role").intValue = (int)role;
            SetStats(serializedObject.FindProperty("_baseStats"), stats);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        private BFUnitUnityBindingSO CreateUnityBinding(GameObject overridePrefab = null)
        {
            var binding = CreateScriptableObject<BFUnitUnityBindingSO>();
            var serializedObject = new SerializedObject(binding);
            serializedObject.FindProperty("_overrideUnitPrefab").objectReferenceValue = overridePrefab;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return binding;
        }

        private BFUnitDefinitionSO CreateDefinition(BFUnitImportedConfigSO config, BFUnitUnityBindingSO binding)
        {
            var definition = CreateScriptableObject<BFUnitDefinitionSO>();
            var serializedObject = new SerializedObject(definition);
            serializedObject.FindProperty("_importedConfig").objectReferenceValue = config;
            serializedObject.FindProperty("_unityBinding").objectReferenceValue = binding;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private BFUnitFactoryConfigSO CreateFactoryConfig(GameObject defaultPrefab)
        {
            var factoryConfig = CreateScriptableObject<BFUnitFactoryConfigSO>();
            var serializedObject = new SerializedObject(factoryConfig);
            serializedObject.FindProperty("_defaultUnitPrefab").objectReferenceValue = defaultPrefab;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return factoryConfig;
        }

        private BFBattleEncounterSO CreateEncounter(
            BFUnitDefinitionSO definition,
            Vector2Int gridPosition,
            UnitFaction factionOverride,
            int unitLevel)
        {
            var encounter = CreateScriptableObject<BFBattleEncounterSO>();
            var serializedObject = new SerializedObject(encounter);
            var entries = serializedObject.FindProperty("_spawnEntries");
            entries.arraySize = 1;
            var entry = entries.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("_unitDefinition").objectReferenceValue = definition;
            entry.FindPropertyRelative("_gridPosition").vector2IntValue = gridPosition;
            entry.FindPropertyRelative("_unitLevel").intValue = unitLevel;
            entry.FindPropertyRelative("_factionOverride").intValue = (int)factionOverride;
            entry.FindPropertyRelative("_isEnabled").boolValue = true;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return encounter;
        }

        private BFBattleEncounterSO CreateEncounterWithDuplicatePositions(BFUnitDefinitionSO definition)
        {
            var encounter = CreateScriptableObject<BFBattleEncounterSO>();
            var serializedObject = new SerializedObject(encounter);
            var entries = serializedObject.FindProperty("_spawnEntries");
            entries.arraySize = 2;
            for (var index = 0; index < entries.arraySize; index++)
            {
                var entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("_unitDefinition").objectReferenceValue = definition;
                entry.FindPropertyRelative("_gridPosition").vector2IntValue = new Vector2Int(1, 1);
                entry.FindPropertyRelative("_unitLevel").intValue = 1;
                entry.FindPropertyRelative("_factionOverride").intValue = (int)UnitFaction.None;
                entry.FindPropertyRelative("_isEnabled").boolValue = true;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return encounter;
        }

        private BFBattleEncounterSO CreateEncounterWithPositions(
            BFUnitDefinitionSO definition,
            params Vector2Int[] positions)
        {
            var encounter = CreateScriptableObject<BFBattleEncounterSO>();
            var serializedObject = new SerializedObject(encounter);
            var entries = serializedObject.FindProperty("_spawnEntries");
            entries.arraySize = positions.Length;
            for (var index = 0; index < positions.Length; index++)
            {
                var entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("_unitDefinition").objectReferenceValue = definition;
                entry.FindPropertyRelative("_gridPosition").vector2IntValue = positions[index];
                entry.FindPropertyRelative("_unitLevel").intValue = 1;
                entry.FindPropertyRelative("_factionOverride").intValue = (int)UnitFaction.None;
                entry.FindPropertyRelative("_isEnabled").boolValue = true;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return encounter;
        }

        private static void SetStats(SerializedProperty property, BFUnitStatBlock stats)
        {
            property.FindPropertyRelative("_maxHP").intValue = stats.MaxHP;
            property.FindPropertyRelative("_attack").intValue = stats.Attack;
            property.FindPropertyRelative("_attackRange").intValue = stats.AttackRange;
            property.FindPropertyRelative("_attackCost").intValue = stats.AttackCost;
            property.FindPropertyRelative("_maxActionPoints").intValue = stats.MaxActionPoints;
        }

        private BFBattleBoardManager CreateScannedBoard(int width, int height)
        {
            var boardObject = new GameObject("Board");
            _createdObjects.Add(boardObject);
            var astar = boardObject.AddComponent<AstarPath>();
            var grid = astar.data.AddGraph(typeof(GridGraph)) as GridGraph;
            Assert.That(grid, Is.Not.Null);
            grid.SetDimensions(width, height, 1f);
            grid.center = new Vector3(width * 0.5f - 0.5f, height * 0.5f - 0.5f, 0f);
            grid.is2D = true;
            grid.collision.use2D = true;
            grid.collision.heightCheck = false;
            grid.neighbours = NumNeighbours.Four;
            astar.Scan();

            var manager = boardObject.AddComponent<BFBattleBoardManager>();
            var awake = typeof(BFBattleBoardManager).GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(awake, Is.Not.Null);
            awake.Invoke(manager, null);
            return manager;
        }

        private sealed class FailingRuntimeProvider : IBFUnitRuntimeProvider
        {
            public int CreateCount { get; private set; }
            public int ReleaseCount { get; private set; }

            public UnitRuntime Create(
                GameObject prefab,
                Vector3 worldPosition,
                Transform parent,
                out string error)
            {
                CreateCount++;
                error = "runtime creation failed";
                return null;
            }

            public void Release(UnitRuntime runtime)
            {
                ReleaseCount++;
            }
        }

        private sealed class RecordingRuntimeProvider : IBFUnitRuntimeProvider
        {
            public int CreateCount { get; private set; }

            public UnitRuntime Create(
                GameObject prefab,
                Vector3 worldPosition,
                Transform parent,
                out string error)
            {
                CreateCount++;
                error = string.Empty;
                return null;
            }

            public void Release(UnitRuntime runtime)
            {
            }
        }

        private sealed class FailOnCreateNumberRuntimeProvider : IBFUnitRuntimeProvider
        {
            private readonly int _failureNumber;

            public FailOnCreateNumberRuntimeProvider(int failureNumber)
            {
                _failureNumber = failureNumber;
            }

            public int CreateCount { get; private set; }
            public int ReleaseCount { get; private set; }

            public UnitRuntime Create(
                GameObject prefab,
                Vector3 worldPosition,
                Transform parent,
                out string error)
            {
                CreateCount++;
                if (CreateCount == _failureNumber)
                {
                    error = "runtime creation failed";
                    return null;
                }

                var instance = Object.Instantiate(prefab, worldPosition, Quaternion.identity, parent);
                if (!instance.TryGetComponent(out UnitRuntime runtime))
                {
                    Object.DestroyImmediate(instance);
                    error = "created prefab has no UnitRuntime";
                    return null;
                }

                error = string.Empty;
                return runtime;
            }

            public void Release(UnitRuntime runtime)
            {
                ReleaseCount++;
                if (runtime != null)
                    Object.DestroyImmediate(runtime.gameObject);
            }
        }
    }
}
