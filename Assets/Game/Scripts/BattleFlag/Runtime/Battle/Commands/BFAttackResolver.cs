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
        /// <param name="unitStateRules">当前战斗会话的单位规则入口。</param>
        public BFAttackResolver(BFUnitStateRules unitStateRules)
        {
            _unitStateRules = unitStateRules ?? throw new System.ArgumentNullException(nameof(unitStateRules));
        }

        /// <summary>
        /// 结算攻击并返回结果。
        /// </summary>
        public BFAttackResolveResult Resolve(BFAttackContext context)
        {
            if (context.Attacker == null || context.Target == null)
            {
                Debug.LogWarning("[BFAttackResolver] 攻击者或目标为空，无法结算。");
                return BFAttackResolveResult.Failure("攻击者或目标为空。");
            }

            if (!context.Target.Stats.IsAlive)
            {
                Debug.LogWarning("[BFAttackResolver] 目标已死亡，无法结算。");
                return BFAttackResolveResult.Failure("目标已经死亡。");
            }

            // 正式战斗单位必须绑定规则状态；未绑定单位不能进入规则结算。
            if (!context.Target.IsRuleBound || !context.Attacker.IsRuleBound)
            {
                Debug.LogWarning("[BFAttackResolver] 攻击者或目标未绑定规则状态，无法结算。");
                return BFAttackResolveResult.Failure("攻击者或目标未绑定规则状态。");
            }

            // 攻击者 AP、目标伤害和死亡状态由规则入口作为单个命令提交；
            // 伤害值由规则攻击力决定，提交成功后统一刷新投影并触发表现反馈。
            var attackResult = _unitStateRules.TryResolveAttack(
                new AttackRequest(
                    context.Attacker.RuntimeId,
                    context.Target.RuntimeId,
                    context.AttackCost));
            if (!attackResult.Succeeded)
            {
                Debug.LogWarning($"[BFAttackResolver] 攻击规则结算被拒绝：{attackResult.FailureReason}");
                return BFAttackResolveResult.Failure(attackResult.FailureReason);
            }

            context.Attacker.RefreshRuleStateProjection();
            context.Target.RefreshRuleStateProjection();
            context.Target.ApplyRuleDamagePresentation(attackResult.TargetWasKilled);

            return BFAttackResolveResult.Success(
                context.Attacker,
                context.Target,
                attackResult.Damage,
                attackResult.TargetWasKilled,
                attackResult.TargetRemainingHp
            );
        }
    }
}
