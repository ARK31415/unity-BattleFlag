using UnityEngine;

namespace BF.Game.Runtime.Battle.Units
{
    /// <summary>
    /// 单位待机状态。
    /// 单位未行动时的默认状态，等待玩家选择行为。
    /// 四段式生命周期中，此状态不执行任何主动逻辑。
    /// </summary>
    public class UnitIdleState : BaseUnitState
    {
        /// <summary>
        /// 进入待机：不做特殊处理，单位保持 Idle 动画。
        /// </summary>
        public override void OnEnter()
        {
        }

        /// <summary>
        /// 逻辑更新：等待外部命令触发状态切换。
        /// </summary>
        public override void LogicUpdate()
        {
        }

        /// <summary>
        /// 物理更新：Idle 状态无物理更新需求。
        /// </summary>
        public override void PhysicsUpdate()
        {
        }

        /// <summary>
        /// 退出待机：不做特殊处理，由下个状态的 OnEnter 接管。
        /// </summary>
        public override void OnExit()
        {
        }
    }
}
