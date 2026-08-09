namespace BF.Game.Battle.Domain.Events
{
    /// <summary>
    /// 表示战斗阶段、回合和轮次已经完成一次规则状态更新。
    ///
    /// 事件中的阶段与计数值均为更新后的结果；监听者可以直接使用这些值刷新自身状态。
    /// </summary>
    public readonly struct BFBattlePhaseChangedEvent
    {
        /// <summary>
        /// 创建战斗阶段变化事件。
        /// </summary>
        /// <param name="battleId">本场战斗的唯一标识。</param>
        /// <param name="previousPhase">状态更新前的战斗阶段。</param>
        /// <param name="currentPhase">状态更新后的战斗阶段。</param>
        /// <param name="turnNumber">状态更新后的回合编号。</param>
        /// <param name="roundNumber">状态更新后的轮次编号。</param>
        public BFBattlePhaseChangedEvent(
            string battleId,
            BFBattlePhase previousPhase,
            BFBattlePhase currentPhase,
            int turnNumber,
            int roundNumber)
        {
            BattleId = battleId ?? string.Empty;
            PreviousPhase = previousPhase;
            CurrentPhase = currentPhase;
            TurnNumber = turnNumber;
            RoundNumber = roundNumber;
        }

        /// <summary>
        /// 本场战斗的唯一标识。
        /// </summary>
        public string BattleId { get; }

        /// <summary>
        /// 状态更新前的战斗阶段。
        /// </summary>
        public BFBattlePhase PreviousPhase { get; }

        /// <summary>
        /// 状态更新后的战斗阶段。
        /// </summary>
        public BFBattlePhase CurrentPhase { get; }

        /// <summary>
        /// 状态更新后的回合编号；战斗正式进入玩家行动阶段后从 1 开始，初始化阶段可能为 0。
        /// </summary>
        public int TurnNumber { get; }

        /// <summary>
        /// 状态更新后的轮次编号。
        /// </summary>
        public int RoundNumber { get; }
    }
}
