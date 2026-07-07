using UnityEngine;

namespace BF.Game.Runtime.Battle.Units
{
    /// <summary>
    /// 单位攻击状态。
    /// 单位正在执行攻击行为。
    /// </summary>
    public class UnitAttackState : BaseUnitState
    {
        private UnitRuntime _targetUnit;

        public override void OnEnter()
        {
            _targetUnit = null;
        }

        public override void LogicUpdate()
        {
            // 攻击逻辑由 BattleCommandService 驱动
        }

        public override void PhysicsUpdate()
        {
        }

        public override void OnExit()
        {
            _targetUnit = null;
        }

        /// <summary>
        /// 设置攻击目标。
        /// </summary>
        public void SetTarget(UnitRuntime target)
        {
            _targetUnit = target;
        }
    }
}
