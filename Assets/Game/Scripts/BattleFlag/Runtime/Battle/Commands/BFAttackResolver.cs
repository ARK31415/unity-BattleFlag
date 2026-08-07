using BF.Game.Battle.Rules.Units;
using BF.Game.Runtime.Battle.Units;
using UnityEngine;

namespace BF.Game.Runtime.Battle.Commands
{
    /// <summary>
    /// 攻击结算协作者。负责根据攻击上下文计算并应用最终伤害。
    /// 本期维持与当前 MVP 同等复杂度的最小伤害公式。
    /// </summary>
    public class BFAttackResolver
    {
        private readonly BFUnitStateRules _unitStateRules;

        /// <summary>
        /// 创建攻击结算协作者。
        /// </summary>
        /// <param name="unitStateRules">
        /// 当前战斗会话的单位规则入口；为空时仅保留未绑定 Runtime 的旧兼容路径。
        /// </param>
        public BFAttackResolver(BFUnitStateRules unitStateRules = null)
        {
            _unitStateRules = unitStateRules;
        }

        /// <summary>
        /// 结算攻击并返回结果。
        /// </summary>
        public BFAttackResolveResult Resolve(BFAttackContext context)
        {
            if (context.Attacker == null || context.Target == null)
            {
                Debug.LogWarning("[BFAttackResolver] 攻击者或目标为空，无法结算。");
                return default;
            }

            if (!context.Target.Stats.IsAlive)
            {
                Debug.LogWarning("[BFAttackResolver] 目标已死亡，无法结算。");
                return default;
            }

            int finalDamage = Mathf.Max(0, context.BaseAttack);

            bool targetWasKilled;
            if (context.Target.IsRuleBound)
            {
                if (_unitStateRules == null || !_unitStateRules.TryApplyDamage(
                        context.Target.RuntimeId,
                        finalDamage,
                        out targetWasKilled))
                {
                    Debug.LogWarning("[BFAttackResolver] 规则伤害入口拒绝了本次攻击。");
                    return default;
                }

                // 规则状态成功更新后，才把结果投影到 Runtime 并触发表现反馈。
                context.Target.RefreshRuleStateProjection();
                context.Target.ApplyRuleDamagePresentation(targetWasKilled);
            }
            else
            {
                // 旧场景单位没有 Session 绑定时保留原有兼容行为。
                context.Target.ApplyResolvedDamage(finalDamage);
                targetWasKilled = !context.Target.Stats.IsAlive;
            }

            int targetRemainingHp = context.Target.Stats.CurrentHP;

            return new BFAttackResolveResult(
                context.Attacker,
                context.Target,
                finalDamage,
                targetWasKilled,
                targetRemainingHp
            );
        }
    }
}
