using System;
using BF.Game.Battle.Domain.Units;
using BF.Game.Runtime.Battle.Data;
using BF.Game.Runtime.Battle.Units;

namespace BF.Game.Runtime.Battle.Factory
{
    /// <summary>
    /// 将规则状态投影到 UnitRuntime，不反向写回规则状态。
    /// </summary>
    public sealed class BFUnitRuntimeBinder
    {
        /// <summary>
        /// 绑定已完成初始化的规则状态与 Runtime。
        /// </summary>
        public bool TryBind(
            BFBattleUnitHandle handle,
            BFUnitState state,
            UnitRuntime runtime,
            BFUnitStatBlock combatStats,
            BFUnitUnityBindingSO unityBinding,
            string displayName,
            BFUnitDefinitionSO definition,
            out string error)
        {
            if (handle == null)
            {
                error = "Unit handle is missing.";
                return false;
            }

            if (state == null || runtime == null)
            {
                error = "Rule state or UnitRuntime is missing.";
                return false;
            }

            if (!string.Equals(state.RuntimeId, handle.RuntimeId, StringComparison.Ordinal))
            {
                error = "Rule state and handle RuntimeId do not match.";
                return false;
            }

            if (unityBinding == null)
            {
                error = "Unity binding is missing.";
                return false;
            }

            runtime.BindRuleState(state, combatStats, unityBinding, displayName, handle, definition);
            error = string.Empty;
            return true;
        }

        /// <summary>解除 Runtime 与规则状态的适配关系。</summary>
        public void Unbind(UnitRuntime runtime)
        {
            runtime?.UnbindRuleState();
        }
    }
}
