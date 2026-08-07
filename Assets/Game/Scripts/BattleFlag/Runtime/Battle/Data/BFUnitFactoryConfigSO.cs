using BF.Game.Runtime.Battle.Units;
using UnityEngine;

namespace BF.Game.Runtime.Battle.Data
{
    /// <summary>
    /// 单位生成配置（ScriptableObject）。
    /// 保存默认单位 Prefab，统一执行 Prefab 选择与 Unit Runtime Contract 校验。
    /// </summary>
    [CreateAssetMenu(fileName = "BFUnitFactoryConfig", menuName = "BF/Battle/Factory/Unit Factory Config")]
    public class BFUnitFactoryConfigSO : ScriptableObject
    {
        /// <summary>默认单位 Prefab，所有使用默认外观的单位共享此模板。</summary>
        [SerializeField] private GameObject _defaultUnitPrefab;

        /// <summary>默认单位 Prefab。</summary>
        public GameObject DefaultUnitPrefab => _defaultUnitPrefab;

        /// <summary>
        /// 根据单位定义和生成配置选择 Prefab。
        /// 优先返回 Override Prefab，否则返回默认 Prefab。
        /// </summary>
        /// <param name="definition">单位定义聚合入口。</param>
        /// <param name="prefab">选定的 Prefab。</param>
        /// <param name="error">失败时输出错误信息。</param>
        /// <returns>true 表示成功选定并校验通过。</returns>
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

        /// <summary>
        /// 校验 Prefab 是否满足单位运行时组件合同。
        /// 必需组件：UnitRuntime、Identity、Stats、Grid、Combat、PresentationStateMachine。
        /// </summary>
        /// <param name="prefab">待校验的 Prefab。</param>
        /// <returns>true 表示满足合同。</returns>
        public static bool PrefabSatisfiesUnitContract(GameObject prefab)
        {
            return prefab != null
                   && prefab.TryGetComponent(out UnitRuntime _)
                   && prefab.TryGetComponent(out BFUnitIdentityRuntime _)
                   && prefab.TryGetComponent(out BFUnitStatsRuntime _)
                   && prefab.TryGetComponent(out BFUnitGridRuntime _)
                   && prefab.TryGetComponent(out BFUnitCombatRuntime _)
                   && prefab.TryGetComponent(out BFUnit_PresentationStateMachineRuntime _);
        }
    }
}
