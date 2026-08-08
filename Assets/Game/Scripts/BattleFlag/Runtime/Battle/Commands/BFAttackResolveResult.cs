using BF.Game.Runtime.Battle.Units;

namespace BF.Game.Runtime.Battle.Commands
{
    /// <summary>
    /// 攻击结果合同。表示一次攻击结算后的结果信息。
    /// </summary>
    public readonly struct BFAttackResolveResult
    {
        /// <summary>是否成功完成规则攻击结算。</summary>
        public bool Succeeded { get; }

        /// <summary>结算失败原因；成功时为空字符串。</summary>
        public string FailureReason { get; }

        /// <summary>攻击发起者。</summary>
        public UnitRuntime Attacker { get; }
        
        /// <summary>攻击目标。</summary>
        public UnitRuntime Target { get; }
        
        /// <summary>最终造成的伤害值。</summary>
        public int FinalDamage { get; }
        
        /// <summary>目标是否被击杀。</summary>
        public bool TargetWasKilled { get; }
        
        /// <summary>目标剩余生命值。</summary>
        public int TargetRemainingHp { get; }

        private BFAttackResolveResult(
            bool succeeded,
            string failureReason,
            UnitRuntime attacker,
            UnitRuntime target,
            int finalDamage,
            bool targetWasKilled,
            int targetRemainingHp)
        {
            Succeeded = succeeded;
            FailureReason = failureReason;
            Attacker = attacker;
            Target = target;
            FinalDamage = finalDamage;
            TargetWasKilled = targetWasKilled;
            TargetRemainingHp = targetRemainingHp;
        }

        /// <summary>创建成功结算结果。</summary>
        public static BFAttackResolveResult Success(
            UnitRuntime attacker,
            UnitRuntime target,
            int finalDamage,
            bool targetWasKilled,
            int targetRemainingHp)
        {
            return new BFAttackResolveResult(
                true,
                string.Empty,
                attacker,
                target,
                finalDamage,
                targetWasKilled,
                targetRemainingHp);
        }

        /// <summary>创建明确失败结算结果。</summary>
        public static BFAttackResolveResult Failure(string failureReason)
        {
            return new BFAttackResolveResult(
                false,
                string.IsNullOrWhiteSpace(failureReason) ? "攻击结算失败。" : failureReason,
                null,
                null,
                0,
                false,
                0);
        }
    }
}
