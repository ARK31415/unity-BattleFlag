using UnityEngine;

namespace BF.Game.Runtime.Battle.Units
{
    /// <summary>
    /// 单位移动状态。
    /// 单位正在执行移动行为。
    /// </summary>
    public class UnitMoveState : BaseUnitState
    {
        public Vector2Int TargetCell { get; private set; }

        public override void OnEnter()
        {
        }

        public override void LogicUpdate()
        {
            // 移动逻辑由 IMovementHandler 驱动
            // 此处在 GridManager 的协助下逐格移动
        }

        public override void PhysicsUpdate()
        {
        }

        public override void OnExit()
        {
        }

        /// <summary>
        /// 设置移动目标并开始移动。
        /// </summary>
        public void SetTarget(Vector2Int targetCell)
        {
            TargetCell = targetCell;
        }
    }
}
