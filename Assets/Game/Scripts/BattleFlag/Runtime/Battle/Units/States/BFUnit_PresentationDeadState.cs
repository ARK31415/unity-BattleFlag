using UnityEngine;

namespace BF.Game.Runtime.Battle.Units
{
    /// <summary>
    /// 单位表现阵亡状态。
    /// 规则层先进入 Dead；表现对象继续播放死亡动画，完成后再执行视觉清理。
    /// </summary>
    public class BFUnit_PresentationDeadState : BFUnit_PresentationState
    {
        /// <inheritdoc />
        public override void OnEnter()
        {
            Debug.Log($"[BFUnit_PresentationDeadState] {Owner.Identity.DisplayName} 逻辑死亡，等待死亡动画完成。");
        }

        /// <inheritdoc />
        public override void LogicUpdate()
        {
        }

        /// <inheritdoc />
        public override void PhysicsUpdate()
        {
        }

        /// <inheritdoc />
        public override void OnExit()
        {
        }
    }
}
