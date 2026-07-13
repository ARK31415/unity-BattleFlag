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

            if (!context.Target.IsAlive)
            {
                Debug.LogWarning("[BFAttackResolver] 目标已死亡，无法结算。");
                return default;
            }

            int finalDamage = Mathf.Max(0, context.BaseAttack);
            
            context.Target.ApplyResolvedDamage(finalDamage);
            
            bool targetWasKilled = !context.Target.IsAlive;
            int targetRemainingHp = context.Target.CurrentHP;

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
