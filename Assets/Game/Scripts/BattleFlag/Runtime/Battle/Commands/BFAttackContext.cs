using BF.Game.Runtime.Battle.Units;
using UnityEngine;

namespace BF.Game.Runtime.Battle.Commands
{
    /// <summary>
    /// 攻击上下文数据合同。表示一次待结算攻击的参与者、基础伤害与结算状态。
    /// </summary>
    public readonly struct BFAttackContext
    {
        /// <summary>攻击发起者。</summary>
        public UnitRuntime Attacker { get; }
        
        /// <summary>攻击目标。</summary>
        public UnitRuntime Target { get; }
        
        /// <summary>攻击消耗的行动点数。</summary>
        public int AttackCost { get; }
        
        /// <summary>基础攻击力。</summary>
        public int BaseAttack { get; }
        
        /// <summary>攻击范围。</summary>
        public int AttackRange { get; }
        
        /// <summary>是否已被消费（防止重复结算）。</summary>
        public bool Consumed { get; }

        public BFAttackContext(UnitRuntime attacker, UnitRuntime target)
        {
            Attacker = attacker;
            Target = target;
            AttackCost = attacker != null ? attacker.AttackCost : 0;
            BaseAttack = attacker != null ? attacker.Attack : 0;
            AttackRange = attacker != null ? attacker.AttackRange : 0;
            Consumed = false;
        }

        /// <summary>返回一个已消费标记的上下文副本。</summary>
        public BFAttackContext AsConsumed()
        {
            return new BFAttackContext(Attacker, Target, AttackCost, BaseAttack, AttackRange, true);
        }

        private BFAttackContext(UnitRuntime attacker, UnitRuntime target, int attackCost, int baseAttack, int attackRange, bool consumed)
        {
            Attacker = attacker;
            Target = target;
            AttackCost = attackCost;
            BaseAttack = baseAttack;
            AttackRange = attackRange;
            Consumed = consumed;
        }
    }
}
