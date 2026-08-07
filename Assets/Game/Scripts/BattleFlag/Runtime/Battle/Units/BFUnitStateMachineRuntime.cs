using UnityEngine;

namespace BF.Game.Runtime.Battle.Units
{
    /// <summary>
    /// 旧表现状态机组件兼容类型。
    /// 新代码和新 Prefab 合同必须使用 <see cref="BFUnit_PresentationStateMachineRuntime"/>。
    /// </summary>
    [DisallowMultipleComponent]
    [System.Obsolete("Use BFUnit_PresentationStateMachineRuntime instead.")]
    public class BFUnitStateMachineRuntime : BFUnit_PresentationStateMachineRuntime
    {
    }
}
