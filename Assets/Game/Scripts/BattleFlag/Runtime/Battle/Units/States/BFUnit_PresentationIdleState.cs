namespace BF.Game.Runtime.Battle.Units
{
    /// <summary>
    /// 单位表现待机状态。
    /// 规则层的 Idle 只描述行动状态；本类型只负责表现状态机中的待机阶段。
    /// </summary>
    public class BFUnit_PresentationIdleState : BFUnit_PresentationState
    {
        /// <inheritdoc />
        public override void OnEnter()
        {
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
