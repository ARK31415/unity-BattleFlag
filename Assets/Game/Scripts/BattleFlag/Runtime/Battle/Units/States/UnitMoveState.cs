using UnityEngine;

namespace BF.Game.Runtime.Battle.Units
{
    /// <summary>
    /// 单位移动状态。
    /// 单位正在沿 A* 路径逐格行走，移动逻辑由 IMovementHandler 驱动。
    /// </summary>
    public class UnitMoveState : BaseUnitState
    {
        /// <summary>移动目标格坐标。</summary>
        public Vector2Int TargetCell { get; private set; }

        /// <summary>
        /// 进入移动状态：不做特殊处理，移动由外部命令触发。
        /// </summary>
        public override void OnEnter()
        {
        }

        /// <summary>
        /// 逻辑更新：移动逻辑由 IMovementHandler 驱动，
        /// 在 GridManager 的协助下逐格移动。
        /// </summary>
        public override void LogicUpdate()
        {
        }

        /// <summary>
        /// 物理更新：移动状态无独立物理更新需求。
        /// </summary>
        public override void PhysicsUpdate()
        {
        }

        /// <summary>
        /// 退出移动状态：不做特殊处理。
        /// </summary>
        public override void OnExit()
        {
        }

        /// <summary>
        /// 设置移动目标格，由 UnitManager 在路径移动开始前调用。
        /// </summary>
        /// <param name="targetCell">目标格坐标。</param>
        public void SetTarget(Vector2Int targetCell)
        {
            TargetCell = targetCell;
        }
    }
}
