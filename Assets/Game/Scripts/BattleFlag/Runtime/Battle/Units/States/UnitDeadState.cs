using UnityEngine;

namespace BF.Game.Runtime.Battle.Units
{
    /// <summary>
    /// 单位阵亡状态。
    /// 单位 HP 归零后进入此状态：逻辑死亡立即生效（停止移动/攻击/被选中），
    /// 但视觉对象继续存在以播放死亡动画，动画完成后由 FinalizeDeathVisualCleanup 清理。
    /// </summary>
    public class UnitDeadState : BaseUnitState
    {
        /// <summary>
        /// 进入阵亡状态：逻辑死亡立即生效，视觉清理等待死亡动画完成事件。
        /// </summary>
        public override void OnEnter()
        {
            Debug.Log($"[UnitDeadState] {Owner.Identity.DisplayName} 逻辑死亡，等待死亡动画完成。");
        }

        /// <summary>
        /// 逻辑更新：阵亡单位不执行任何逻辑（不可移动、不可攻击、不可被选中）。
        /// </summary>
        public override void LogicUpdate()
        {
        }

        /// <summary>
        /// 物理更新：阵亡单位无物理更新需求。
        /// </summary>
        public override void PhysicsUpdate()
        {
        }

        /// <summary>
        /// 退出阵亡：阵亡状态不应被退出（除非实现复活机制）。
        /// </summary>
        public override void OnExit()
        {
        }
    }
}
