using BF.Game.Runtime.Battle.Units;
using UnityEngine;

namespace BF.Game.Runtime.Battle.Factory
{
    /// <summary>
    /// 使用 Unity Instantiate/Destroy 管理单位 Runtime 的默认 Provider。
    /// </summary>
    public sealed class BFUnityUnitRuntimeProvider : IBFUnitRuntimeProvider
    {
        /// <inheritdoc />
        public UnitRuntime Create(GameObject prefab, Vector3 worldPosition, Transform parent, out string error)
        {
            if (prefab == null)
            {
                error = "Unit prefab is missing.";
                return null;
            }

            var instance = Object.Instantiate(prefab, worldPosition, Quaternion.identity, parent);
            if (!instance.TryGetComponent(out UnitRuntime runtime))
            {
                Release(instance.GetComponent<UnitRuntime>());
                Object.Destroy(instance);
                error = $"{instance.name} has no UnitRuntime component.";
                return null;
            }

            error = string.Empty;
            return runtime;
        }

        /// <inheritdoc />
        public void Release(UnitRuntime runtime)
        {
            if (runtime == null) return;

            if (Application.isPlaying)
                Object.Destroy(runtime.gameObject);
            else
                Object.DestroyImmediate(runtime.gameObject);
        }
    }
}
