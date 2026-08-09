namespace BF.Game.Battle.Domain.Events
{
    /// <summary>
    /// 表示一个单位已经被规则判定为击败。
    ///
    /// 这是通用的单位击败事实，任务、成就、音频和表现适配器等监听者都应读取该事件，
    /// 而不是分别从攻击结算事件推断击败结果。
    /// </summary>
    public readonly struct BFUnitDefeatedEvent
    {
        /// <summary>
        /// 创建单位击败事件。
        /// </summary>
        /// <param name="battleId">本场战斗的唯一标识。</param>
        /// <param name="runtimeId">被击败的单位 RuntimeId。</param>
        /// <param name="faction">被击败单位所属阵营。</param>
        /// <param name="defeatedByRuntimeId">造成击败的单位 RuntimeId；无明确来源时为空字符串。</param>
        /// <param name="turnNumber">发生击败时的回合编号。</param>
        public BFUnitDefeatedEvent(
            string battleId,
            string runtimeId,
            BFUnitFaction faction,
            string defeatedByRuntimeId,
            int turnNumber)
        {
            BattleId = battleId ?? string.Empty;
            RuntimeId = runtimeId ?? string.Empty;
            Faction = faction;
            DefeatedByRuntimeId = defeatedByRuntimeId ?? string.Empty;
            TurnNumber = turnNumber;
        }

        /// <summary>
        /// 本场战斗的唯一标识。
        /// </summary>
        public string BattleId { get; }

        /// <summary>
        /// 被击败的单位 RuntimeId。
        /// </summary>
        public string RuntimeId { get; }

        /// <summary>
        /// 被击败单位所属阵营。
        /// </summary>
        public BFUnitFaction Faction { get; }

        /// <summary>
        /// 造成击败的单位 RuntimeId；无明确来源时为空字符串。
        /// </summary>
        public string DefeatedByRuntimeId { get; }

        /// <summary>
        /// 发生击败时的回合编号。
        /// </summary>
        public int TurnNumber { get; }
    }
}
