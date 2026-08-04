namespace BF.Game.Battle.Domain.Events
{
    /// <summary>
    /// 表示一次攻击已经完成最终规则结算。
    ///
    /// 该事件只描述结算事实，不包含动画、音频或 UI 播放指令；表现层应根据事实自行决定反馈方式。
    /// </summary>
    public readonly struct BFAttackResolvedEvent
    {
        /// <summary>
        /// 创建攻击结算完成事件。
        /// </summary>
        /// <param name="battleId">本场战斗的唯一标识。</param>
        /// <param name="attackerId">发起攻击的单位 ID。</param>
        /// <param name="targetId">承受攻击的单位 ID。</param>
        /// <param name="finalDamage">规则结算后的最终伤害值。</param>
        /// <param name="targetRemainingHp">结算完成后目标单位的剩余生命值。</param>
        /// <param name="targetWasDefeated">目标单位是否在本次结算中被击败。</param>
        /// <param name="turnNumber">发生本次攻击时的回合编号。</param>
        public BFAttackResolvedEvent(
            string battleId,
            string attackerId,
            string targetId,
            int finalDamage,
            int targetRemainingHp,
            bool targetWasDefeated,
            int turnNumber)
        {
            BattleId = battleId ?? string.Empty;
            AttackerId = attackerId ?? string.Empty;
            TargetId = targetId ?? string.Empty;
            FinalDamage = finalDamage;
            TargetRemainingHp = targetRemainingHp;
            TargetWasDefeated = targetWasDefeated;
            TurnNumber = turnNumber;
        }

        /// <summary>
        /// 本场战斗的唯一标识。
        /// </summary>
        public string BattleId { get; }

        /// <summary>
        /// 发起攻击的单位 ID。
        /// </summary>
        public string AttackerId { get; }

        /// <summary>
        /// 承受攻击的单位 ID。
        /// </summary>
        public string TargetId { get; }

        /// <summary>
        /// 规则结算后的最终伤害值。
        /// </summary>
        public int FinalDamage { get; }

        /// <summary>
        /// 结算完成后目标单位的剩余生命值。
        /// </summary>
        public int TargetRemainingHp { get; }

        /// <summary>
        /// 指示目标单位是否在本次结算中被击败。
        /// </summary>
        public bool TargetWasDefeated { get; }

        /// <summary>
        /// 发生本次攻击时的回合编号。
        /// </summary>
        public int TurnNumber { get; }
    }
}
