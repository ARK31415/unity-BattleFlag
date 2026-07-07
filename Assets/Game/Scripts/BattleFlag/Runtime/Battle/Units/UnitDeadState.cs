using UnityEngine;

namespace BF.Game.Runtime.Battle.Units
{
    /// <summary>
    /// 单位阵亡状态。
    /// 单位 HP 归零后进入此状态，不再参与行动。
    /// </summary>
    public class UnitDeadState : BaseUnitState
    {
        public override void OnEnter()
        {
            // 阵亡时禁用单位交互
            if (Owner != null && Owner.gameObject != null)
            {
                Owner.gameObject.SetActive(false);
            }
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
            if (Owner != null && Owner.gameObject != null)
            {
                Owner.gameObject.SetActive(true);
            }
        }
    }
}
