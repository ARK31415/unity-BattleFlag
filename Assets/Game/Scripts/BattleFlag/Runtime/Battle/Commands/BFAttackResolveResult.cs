using BF.Game.Runtime.Battle.Units;

namespace BF.Game.Runtime.Battle.Commands
{
    /// <summary>
    /// 攻击结果合同。表示一次攻击结算后的结果信息。
    /// </summary>
    public readonly struct BFAttackResolveResult
    {
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

        public BFAttackResolveResult(UnitRuntime attacker, UnitRuntime target, int finalDamage, bool targetWasKilled, int targetRemainingHp)
        {
            Attacker = attacker;
            Target = target;
            FinalDamage = finalDamage;
            TargetWasKilled = targetWasKilled;
            TargetRemainingHp = targetRemainingHp;
        }
    }
}
