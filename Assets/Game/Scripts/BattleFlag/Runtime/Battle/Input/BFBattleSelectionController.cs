using System;
using UnityEngine;

namespace BF.Game.Runtime.Battle.Input
{
    /// <summary>
    /// 战斗单位选择控制器。
    ///
    /// 选择层只保存 RuntimeId，不持有 UnitRuntime 或 BFUnitState，
    /// 需要使用单位时由当前战斗门面根据 RuntimeId 解析运行时投影。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BFBattleSelectionController : MonoBehaviour
    {
        /// <summary>当前选中单位的 RuntimeId；没有选择时为空。</summary>
        public string SelectedRuntimeId { get; private set; }

        /// <summary>指示当前是否存在有效选择。</summary>
        public bool HasSelection => !string.IsNullOrWhiteSpace(SelectedRuntimeId);

        /// <summary>选择发生变化时触发，参数为空表示取消选择。</summary>
        public event Action<string> SelectionChanged;

        /// <summary>
        /// 记录一个有效 RuntimeId 的选择。
        /// </summary>
        /// <param name="runtimeId">当前战斗会话中的 RuntimeId。</param>
        /// <returns>RuntimeId 有效且选择已记录时返回 true。</returns>
        public bool TrySelect(string runtimeId)
        {
            if (string.IsNullOrWhiteSpace(runtimeId))
                return false;

            if (string.Equals(SelectedRuntimeId, runtimeId, StringComparison.Ordinal))
                return true;

            SelectedRuntimeId = runtimeId;
            SelectionChanged?.Invoke(SelectedRuntimeId);
            return true;
        }

        /// <summary>
        /// 清除当前选择。
        /// </summary>
        /// <returns>之前存在选择并已清除时返回 true。</returns>
        public bool ClearSelection()
        {
            if (!HasSelection)
                return false;

            SelectedRuntimeId = null;
            SelectionChanged?.Invoke(null);
            return true;
        }

        /// <summary>判断指定 RuntimeId 是否为当前选择。</summary>
        public bool IsSelected(string runtimeId)
        {
            return HasSelection && string.Equals(
                SelectedRuntimeId,
                runtimeId,
                StringComparison.Ordinal);
        }
    }
}
