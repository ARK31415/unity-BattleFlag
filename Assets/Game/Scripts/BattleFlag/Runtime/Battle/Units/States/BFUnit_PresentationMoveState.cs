using UnityEngine;

namespace BF.Game.Runtime.Battle.Units
{
    /// <summary>
    /// 单位表现移动状态。
    /// 移动路径和规则位置由适配层维护，本类型只承载表现状态机阶段。
    /// </summary>
    public class BFUnit_PresentationMoveState : BFUnit_PresentationState
    {
        /// <summary>移动目标格坐标。</summary>
        public Vector2Int TargetCell { get; private set; }

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

        /// <summary>设置表现移动目标格。</summary>
        public void SetTarget(Vector2Int targetCell)
        {
            TargetCell = targetCell;
        }
    }
}
