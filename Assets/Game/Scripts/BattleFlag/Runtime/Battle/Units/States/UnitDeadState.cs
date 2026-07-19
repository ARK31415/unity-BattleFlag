using UnityEngine;

namespace BF.Game.Runtime.Battle.Units
{
    /// <summary>
    /// 单位阵亡状态。
    /// 单位 HP 归零后进入此状态，逻辑上立即死亡，但视觉对象继续存在以播放死亡动画。
    /// </summary>
    public class UnitDeadState : BaseUnitState
    {
        public override void OnEnter()
        {
            // 逻辑上立即死亡，但不隐藏对象
            // 视觉清理由 BFUnitAnimationPresenter 在死亡动画完成后调用 FinalizeDeathVisualCleanup
            Debug.Log($"[UnitDeadState] {Owner.Identity.DisplayName} 逻辑死亡，等待死亡动画完成。");
        }

        public override void LogicUpdate()
        {
            // 阵亡单位不执行任何逻辑
        }

        public override void PhysicsUpdate()
        {
        }

        public override void OnExit()
        {
            // 阵亡状态不应被退出（除非复活）
        }
    }
}
