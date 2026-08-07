using UnityEngine;

namespace BF.Game.Runtime.Battle.Units
{
    /// <summary>
    /// 单位表现攻击状态。
    /// 规则结算由规则/适配流程完成，动画命中帧只通知适配层执行既定结算。
    /// </summary>
    public class BFUnit_PresentationAttackState : BFUnit_PresentationState
    {
        private UnitRuntime _targetUnit;

        /// <inheritdoc />
        public override void OnEnter()
        {
            Debug.Log($"[BFUnit_PresentationAttackState] {Owner.Identity.DisplayName} 进入攻击状态。");

            var presenter = Owner.GetComponent<Presentation.BFUnitAnimationPresenter>();
            if (_targetUnit != null)
            {
                presenter?.FaceTarget(Owner.Grid.GridPosition, _targetUnit.Grid.GridPosition);
            }

            presenter?.PlayAttack();
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
            _targetUnit = null;
            Debug.Log($"[BFUnit_PresentationAttackState] {Owner.Identity.DisplayName} 退出攻击状态。");
        }

        /// <summary>设置表现攻击目标。</summary>
        public void SetTarget(UnitRuntime target)
        {
            _targetUnit = target;
        }

        /// <summary>获取当前表现攻击目标。</summary>
        public UnitRuntime GetTarget()
        {
            return _targetUnit;
        }
    }
}
