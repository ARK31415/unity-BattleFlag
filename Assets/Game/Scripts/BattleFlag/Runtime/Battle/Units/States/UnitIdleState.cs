using UnityEngine;

namespace BF.Game.Runtime.Battle.Units
{
    /// <summary>
    /// 单位待机状态。
    /// 单位未行动时的默认状态，等待玩家选择行为。
    /// </summary>
    public class UnitIdleState : BaseUnitState
    {
        public override void OnEnter()
        {
            // 待机时不做特殊处理
        }

        public override void LogicUpdate()
        {
            // 等待外部命令触发状态切换
        }

        public override void PhysicsUpdate()
        {
        }

        public override void OnExit()
        {
        }
    }
}
