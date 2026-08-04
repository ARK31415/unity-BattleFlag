namespace BF.Game.Battle.Domain.Events
{
    /// <summary>
    /// 表示战斗胜负结果已经由规则层确定。
    ///
    /// 该事件发布时，战斗结果已经写入上下文；监听者不应在回调中重新计算胜负。
    /// </summary>
    public readonly struct BFBattleCompletedEvent
    {
        /// <summary>
        /// 创建战斗完成事件。
        /// </summary>
        /// <param name="battleId">本场战斗的唯一标识。</param>
        /// <param name="winnerFaction">获胜阵营。</param>
        /// <param name="totalTurns">本场战斗使用的总回合数。</param>
        public BFBattleCompletedEvent(string battleId, BFUnitFaction winnerFaction, int totalTurns)
        {
            BattleId = battleId ?? string.Empty;
            WinnerFaction = winnerFaction;
            TotalTurns = totalTurns;
        }

        /// <summary>
        /// 本场战斗的唯一标识。
        /// </summary>
        public string BattleId { get; }

        /// <summary>
        /// 获胜阵营。
        /// </summary>
        public BFUnitFaction WinnerFaction { get; }

        /// <summary>
        /// 本场战斗使用的总回合数。
        /// </summary>
        public int TotalTurns { get; }
    }
}
