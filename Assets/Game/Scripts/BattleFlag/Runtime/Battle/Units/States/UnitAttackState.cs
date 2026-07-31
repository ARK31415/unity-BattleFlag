using UnityEngine;

namespace BF.Game.Runtime.Battle.Units
{
    /// <summary>
    /// 单位攻击状态。
    /// 单位正在执行攻击行为，触发攻击动画并等待命中帧结算。
    /// 实际伤害由动画命中帧通过 Animation Event 通知 ResolutionManager 结算。
    /// </summary>
    public class UnitAttackState : BaseUnitState
    {
        /// <summary>攻击目标单位。</summary>
        private UnitRuntime _targetUnit;

        /// <summary>
        /// 进入攻击状态：触发朝向目标面朝 + 播放攻击动画。
        /// </summary>
        public override void OnEnter()
        {
            Debug.Log($"[UnitAttackState] {Owner.Identity.DisplayName} 进入攻击状态。");

            var presenter = Owner.GetComponent<Presentation.BFUnitAnimationPresenter>();
            if (_targetUnit != null)
            {
                presenter?.FaceTarget(Owner.Grid.GridPosition, _targetUnit.Grid.GridPosition);
            }

            presenter?.PlayAttack();
        }

        /// <summary>
        /// 逻辑更新：攻击逻辑由动画命中帧事件驱动，此处不做逐帧处理。
        /// </summary>
        public override void LogicUpdate()
        {
        }

        /// <summary>
        /// 物理更新：攻击状态无独立物理更新需求。
        /// </summary>
        public override void PhysicsUpdate()
        {
        }

        /// <summary>
        /// 退出攻击状态：清理攻击目标引用。
        /// </summary>
        public override void OnExit()
        {
            _targetUnit = null;
            Debug.Log($"[UnitAttackState] {Owner.Identity.DisplayName} 退出攻击状态。");
        }

        /// <summary>
        /// 设置攻击目标，由 UnitManager 在攻击上下文记录成功后调用。
        /// </summary>
        /// <param name="target">攻击目标单位。</param>
        public void SetTarget(UnitRuntime target)
        {
            _targetUnit = target;
        }

        /// <summary>
        /// 获取当前攻击目标。
        /// </summary>
        /// <returns>攻击目标单位，可能为 null。</returns>
        public UnitRuntime GetTarget()
        {
            return _targetUnit;
        }
    }
}
