using UnityEngine;

namespace BF.Game.Runtime.Battle.Units
{
    /// <summary>
    /// 单位攻击状态。
    /// 单位正在执行攻击行为，等待命中帧结算。
    /// </summary>
    public class UnitAttackState : BaseUnitState
    {
        private UnitRuntime _targetUnit;

        public override void OnEnter()
        {
            // 攻击状态只负责进入攻击表现；实际伤害由动画命中帧通知 ResolutionManager 结算。
            // 进入攻击状态，触发攻击动画
            Debug.Log($"[UnitAttackState] {Owner.Identity.DisplayName} 进入攻击状态。");
            
            var presenter = Owner.GetComponent<Presentation.BFUnitAnimationPresenter>();
            if (_targetUnit != null)
            {
                presenter?.FaceTarget(Owner.Grid.GridPosition, _targetUnit.Grid.GridPosition);
            }

            presenter?.PlayAttack();
        }

        public override void LogicUpdate()
        {
            // 攻击逻辑由动画命中帧事件驱动
        }

        public override void PhysicsUpdate()
        {
        }

        public override void OnExit()
        {
            _targetUnit = null;
            Debug.Log($"[UnitAttackState] {Owner.Identity.DisplayName} 退出攻击状态。");
        }

        /// <summary>
        /// 设置攻击目标。
        /// </summary>
        public void SetTarget(UnitRuntime target)
        {
            _targetUnit = target;
        }

        /// <summary>
        /// 获取攻击目标。
        /// </summary>
        public UnitRuntime GetTarget()
        {
            return _targetUnit;
        }
    }
}
