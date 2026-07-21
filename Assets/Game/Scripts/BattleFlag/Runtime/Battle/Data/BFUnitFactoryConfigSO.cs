using BF.Game.Runtime.Battle.Units;
using UnityEngine;

namespace BF.Game.Runtime.Battle.Data
{
    /// <summary>
    /// 单位生成系统配置。
    /// </summary>
    [CreateAssetMenu(fileName = "BFUnitFactoryConfig", menuName = "BF/Battle/Factory/Unit Factory Config")]
    public class BFUnitFactoryConfigSO : ScriptableObject
    {
        [SerializeField] private GameObject _defaultUnitPrefab;

        public GameObject DefaultUnitPrefab => _defaultUnitPrefab;

        public bool TryGetPrefab(BFUnitDefinitionSO definition, out GameObject prefab, out string error)
        {
            prefab = null;

            if (definition == null)
            {
                error = "Unit definition is missing.";
                return false;
            }

            if (!definition.ValidateConfiguration(out error))
            {
                return false;
            }

            prefab = definition.UnityBinding.OverrideUnitPrefab != null
                ? definition.UnityBinding.OverrideUnitPrefab
                : _defaultUnitPrefab;

            if (prefab == null)
            {
                error = $"No unit prefab configured for {definition.UnitId}.";
                return false;
            }

            if (!PrefabSatisfiesUnitContract(prefab))
            {
                error = $"{prefab.name} does not satisfy the Unit Runtime Contract.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static bool PrefabSatisfiesUnitContract(GameObject prefab)
        {
            return prefab != null
                   && prefab.TryGetComponent(out UnitRuntime _)
                   && prefab.TryGetComponent(out BFUnitIdentityRuntime _)
                   && prefab.TryGetComponent(out BFUnitStatsRuntime _)
                   && prefab.TryGetComponent(out BFUnitGridRuntime _)
                   && prefab.TryGetComponent(out BFUnitCombatRuntime _)
                   && prefab.TryGetComponent(out BFUnitStateMachineRuntime _);
        }
    }
}
