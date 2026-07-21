using System.Collections.Generic;
using BF.Game.Runtime.Battle.Data;
using BF.Game.Runtime.Battle.Managers;
using BF.Game.Runtime.Battle.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BF.Game.Tests.EditMode.Battle
{
    /// <summary>
    /// 验证单位 SO 数据驱动配置、Prefab 选择和运行时初始化合同。
    /// </summary>
    public class BFUnitDataDrivenConfigTests
    {
        private readonly List<Object> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _createdObjects.Count; i++)
            {
                if (_createdObjects[i] != null)
                {
                    Object.DestroyImmediate(_createdObjects[i]);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public void Definition_ExposesIdentityFromImportedConfig()
        {
            var config = CreateImportedConfig("warrior_001", "先锋", UnitFaction.Player, BFUnitRole.Warrior, new BFUnitStatBlock(30, 8, 1, 2, 5));
            var binding = CreateUnityBinding();
            var definition = CreateDefinition(config, binding);

            Assert.That(definition.UnitId, Is.EqualTo("warrior_001"));
            Assert.That(definition.DisplayName, Is.EqualTo("先锋"));
            Assert.That(definition.ImportedConfig, Is.SameAs(config));
            Assert.That(definition.UnityBinding, Is.SameAs(binding));
        }

        [Test]
        public void InitializeFromDefinition_AppliesConfigToRuntimeComponents()
        {
            var stats = new BFUnitStatBlock(34, 9, 2, 3, 6);
            var config = CreateImportedConfig("mage_001", "法师", UnitFaction.Player, BFUnitRole.Mage, stats);
            var definition = CreateDefinition(config, CreateUnityBinding());
            var unit = CreateUnit("Runtime Unit");

            unit.InitializeFromDefinition(definition, new BFUnitSpawnContext(new Vector2Int(2, 3), UnitFaction.Enemy));

            Assert.That(unit.Definition, Is.SameAs(definition));
            Assert.That(unit.Identity.UnitId, Is.EqualTo("mage_001"));
            Assert.That(unit.Identity.DisplayName, Is.EqualTo("法师"));
            Assert.That(unit.Identity.Faction, Is.EqualTo(UnitFaction.Enemy));
            Assert.That(unit.Identity.Role, Is.EqualTo(BFUnitRole.Mage));
            Assert.That(unit.Stats.MaxHP, Is.EqualTo(34));
            Assert.That(unit.Stats.CurrentHP, Is.EqualTo(34));
            Assert.That(unit.Stats.Attack, Is.EqualTo(9));
            Assert.That(unit.Stats.AttackRange, Is.EqualTo(2));
            Assert.That(unit.Stats.AttackCost, Is.EqualTo(3));
            Assert.That(unit.Stats.MaxActionPoints, Is.EqualTo(6));
            Assert.That(unit.Stats.RemainingActionPoints, Is.EqualTo(6));
            Assert.That(unit.Grid.GridPosition, Is.EqualTo(new Vector2Int(2, 3)));
            Assert.That(unit.Grid.SpawnGridPosition, Is.EqualTo(new Vector2Int(2, 3)));
        }

        [Test]
        public void FactoryConfig_UsesDefaultPrefabWhenBindingHasNoOverride()
        {
            var defaultPrefab = CreateUnitPrefab("DefaultUnitPrefab");
            var factoryConfig = CreateFactoryConfig(defaultPrefab);
            var definition = CreateDefinition(
                CreateImportedConfig("unit_default", "Default Unit", UnitFaction.Player, BFUnitRole.Warrior, BFUnitStatBlock.Default),
                CreateUnityBinding());

            bool found = factoryConfig.TryGetPrefab(definition, out GameObject prefab, out string error);

            Assert.That(found, Is.True, error);
            Assert.That(prefab, Is.SameAs(defaultPrefab));
        }

        [Test]
        public void FactoryConfig_UsesOverridePrefabWhenConfigured()
        {
            var defaultPrefab = CreateUnitPrefab("DefaultUnitPrefab");
            var overridePrefab = CreateUnitPrefab("OverrideUnitPrefab");
            var factoryConfig = CreateFactoryConfig(defaultPrefab);
            var definition = CreateDefinition(
                CreateImportedConfig("unit_override", "Override Unit", UnitFaction.Player, BFUnitRole.Warrior, BFUnitStatBlock.Default),
                CreateUnityBinding(overridePrefab));

            bool found = factoryConfig.TryGetPrefab(definition, out GameObject prefab, out string error);

            Assert.That(found, Is.True, error);
            Assert.That(prefab, Is.SameAs(overridePrefab));
        }

        [Test]
        public void FactoryConfig_RejectsPrefabMissingRuntimeContract()
        {
            var invalidPrefab = CreateGameObject("InvalidPrefab");
            var factoryConfig = CreateFactoryConfig(invalidPrefab);
            var definition = CreateDefinition(
                CreateImportedConfig("unit_invalid", "Invalid Unit", UnitFaction.Player, BFUnitRole.Warrior, BFUnitStatBlock.Default),
                CreateUnityBinding());

            bool found = factoryConfig.TryGetPrefab(definition, out _, out string error);

            Assert.That(found, Is.False);
            Assert.That(error, Does.Contain("Unit Runtime Contract"));
        }

        [Test]
        public void Spawner_SpawnsEncounterAndInitializesUnit()
        {
            var defaultPrefab = CreateUnitPrefab("DefaultUnitPrefab");
            var factoryConfig = CreateFactoryConfig(defaultPrefab);
            var config = CreateImportedConfig("encounter_unit", "关卡单位", UnitFaction.Player, BFUnitRole.Mage, new BFUnitStatBlock(18, 7, 2, 3, 4));
            var definition = CreateDefinition(config, CreateUnityBinding());
            var encounter = CreateEncounter(definition, new Vector2Int(4, 1), UnitFaction.Enemy);
            var spawner = CreateGameObject("Spawner").AddComponent<BFBattleUnitSpawner>();

            bool spawned = spawner.SpawnEncounter(encounter, factoryConfig, null, out var units);

            Assert.That(spawned, Is.True);
            Assert.That(units, Has.Count.EqualTo(1));
            Assert.That(units[0].Identity.UnitId, Is.EqualTo("encounter_unit"));
            Assert.That(units[0].Identity.DisplayName, Is.EqualTo("关卡单位"));
            Assert.That(units[0].Identity.Faction, Is.EqualTo(UnitFaction.Enemy));
            Assert.That(units[0].Grid.GridPosition, Is.EqualTo(new Vector2Int(4, 1)));
            Assert.That(units[0].Stats.Attack, Is.EqualTo(7));
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

        private UnitRuntime CreateUnit(string name)
        {
            return CreateGameObject(name).AddComponent<UnitRuntime>();
        }

        private GameObject CreateUnitPrefab(string name)
        {
            var prefab = CreateGameObject(name);
            prefab.AddComponent<UnitRuntime>();
            return prefab;
        }

        private BFUnitImportedConfigSO CreateImportedConfig(string unitId, string displayName, UnitFaction faction, BFUnitRole role, BFUnitStatBlock stats)
        {
            var config = CreateScriptableObject<BFUnitImportedConfigSO>();
            var serializedObject = new SerializedObject(config);
            serializedObject.FindProperty("_unitId").stringValue = unitId;
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

        private BFBattleEncounterSO CreateEncounter(BFUnitDefinitionSO definition, Vector2Int gridPosition, UnitFaction factionOverride)
        {
            var encounter = CreateScriptableObject<BFBattleEncounterSO>();
            var serializedObject = new SerializedObject(encounter);
            var entries = serializedObject.FindProperty("_spawnEntries");
            entries.arraySize = 1;

            var entry = entries.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("_unitDefinition").objectReferenceValue = definition;
            entry.FindPropertyRelative("_gridPosition").vector2IntValue = gridPosition;
            entry.FindPropertyRelative("_factionOverride").intValue = (int)factionOverride;
            entry.FindPropertyRelative("_isEnabled").boolValue = true;
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
    }
}
