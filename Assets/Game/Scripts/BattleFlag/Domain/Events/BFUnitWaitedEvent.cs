namespace BF.Game.Battle.Domain.Events
{
    /// <summary>
    /// 表示一次等待已经完成规则提交（剩余 AP 结算为 0）。
    ///
    /// 该事件只描述规则资源已经结算的事实，不包含动画、音频或 UI 播放指令。
    /// </summary>
    public readonly struct BFUnitWaitedEvent
    {
        /// <summary>
        /// 创建等待提交完成事件。
        /// </summary>
        /// <param name="battleId">本场战斗的唯一标识。</param>
        /// <param name="runtimeId">完成等待的单位 RuntimeId。</param>
        /// <param name="turnNumber">发生本次等待时的回合编号。</param>
        public BFUnitWaitedEvent(string battleId, string runtimeId, int turnNumber)
        {
            BattleId = battleId ?? string.Empty;
            RuntimeId = runtimeId ?? string.Empty;
            TurnNumber = turnNumber;
        }

        /// <summary>本场战斗的唯一标识。</summary>
        public string BattleId { get; }

        /// <summary>完成等待的单位 RuntimeId。</summary>
        public string RuntimeId { get; }

        /// <summary>发生本次等待时的回合编号。</summary>
        public int TurnNumber { get; }
    }
}
