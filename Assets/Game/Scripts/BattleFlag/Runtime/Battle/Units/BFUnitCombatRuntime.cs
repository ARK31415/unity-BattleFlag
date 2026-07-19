using BF.Game.Runtime.Battle.Commands;
using UnityEngine;

namespace BF.Game.Runtime.Battle.Units
{
    /// <summary>
    /// 单位攻击上下文运行时组件。
    ///
    /// 职责边界：
    /// - 保存一次待结算攻击的目标和命中帧消费状态。
    /// - 保证同一段攻击动画的命中帧只生成一次 BFAttackContext。
    /// - 不计算最终伤害、不扣 AP、不播放动画，也不切换正式逻辑状态。
    /// </summary>
    [DisallowMultipleComponent]
    public class BFUnitCombatRuntime : MonoBehaviour
    {
        private UnitRuntime _queuedAttackTarget;
        private bool _hasQueuedAttack;
        private bool _hasResolvedQueuedAttack;

        /// <summary>是否存在尚未被命中帧消费的待结算攻击。</summary>
        public bool HasQueuedAttack => _hasQueuedAttack && !_hasResolvedQueuedAttack;

        /// <summary>
        /// 开始记录一次待结算攻击。
        ///
        /// 若上一段攻击尚未被消费，本方法会返回 false，避免覆盖命中帧等待中的上下文。
        /// </summary>
        /// <param name="target">攻击目标，调用前应由 UnitManager 完成阵营、距离和 AP 校验。</param>
        /// <returns>true 表示攻击上下文已成功记录。</returns>
        public bool BeginQueuedAttack(UnitRuntime target)
        {
            if (_hasQueuedAttack && !_hasResolvedQueuedAttack) return false;

            _queuedAttackTarget = target;
            _hasQueuedAttack = true;
            _hasResolvedQueuedAttack = false;
            return true;
        }

        /// <summary>
        /// 尝试在攻击动画命中帧消费待结算攻击。
        ///
        /// 消费成功后同一次攻击不会再次返回上下文，防止动画事件重复触发造成多次扣血。
        /// </summary>
        /// <param name="owner">发起攻击的单位根。</param>
        /// <param name="context">输出给结算层的攻击上下文。</param>
        /// <returns>true 表示本次命中帧取得了可结算攻击。</returns>
        public bool TryConsumeQueuedAttack(UnitRuntime owner, out BFAttackContext context)
        {
            context = default;

            if (!_hasQueuedAttack || _hasResolvedQueuedAttack)
            {
                return false;
            }

            if (_queuedAttackTarget == null || !_queuedAttackTarget.Stats.IsAlive)
            {
                ClearQueuedAttack();
                return false;
            }

            context = new BFAttackContext(owner, _queuedAttackTarget);
            _hasResolvedQueuedAttack = true;
            return true;
        }

        /// <summary>清理待结算攻击，通常在结算完成、回合结束或上下文失效时调用。</summary>
        public void ClearQueuedAttack()
        {
            _hasQueuedAttack = false;
            _hasResolvedQueuedAttack = false;
            _queuedAttackTarget = null;
        }
    }
}
