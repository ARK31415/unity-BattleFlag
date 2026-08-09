using BF.Game.Runtime.Battle.Units;
using UnityEngine;

namespace BF.Game.Runtime.Battle.Factory
{
    /// <summary>
    /// Unity Runtime 创建与释放的适配接口。
    /// </summary>
    public interface IBFUnitRuntimeProvider
    {
        /// <summary>从 Prefab 创建一个单位 Runtime。</summary>
        UnitRuntime Create(GameObject prefab, Vector3 worldPosition, Transform parent, out string error);

        /// <summary>释放一个由 Provider 创建的单位 Runtime。</summary>
        void Release(UnitRuntime runtime);
    }
}
