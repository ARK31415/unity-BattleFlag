using BF.Game.Battle.Domain.Units;

namespace BF.Game.Battle.Domain.Events
{
    /// <summary>
    /// 表示一次移动已经完成规则提交。
    ///
    /// 该事件只描述规则位置与 AP 已经提交的事实，不包含动画、音频或 UI 播放指令；
    /// 棋盘或 A* 同步失败不改变该事实的有效性。
    /// </summary>
    public readonly struct BFUnitMovedEvent
    {
        /// <summary>
        /// 创建移动提交完成事件。
        /// </summary>
        /// <param name="battleId">本场战斗的唯一标识。</param>
        /// <param name="runtimeId">完成移动的单位 RuntimeId。</param>
        /// <param name="fromGridPosition">移动前的规则位置。</param>
        /// <param name="toGridPosition">移动后的规则位置。</param>
        /// <param name="actionPointCost">本次移动消耗的行动点。</param>
        /// <param name="turnNumber">发生本次移动时的回合编号。</param>
        public BFUnitMovedEvent(
            string battleId,
            string runtimeId,
            BFGridPosition fromGridPosition,
            BFGridPosition toGridPosition,
            int actionPointCost,
            int turnNumber)
        {
            BattleId = battleId ?? string.Empty;
            RuntimeId = runtimeId ?? string.Empty;
            FromGridPosition = fromGridPosition;
            ToGridPosition = toGridPosition;
            ActionPointCost = actionPointCost;
            TurnNumber = turnNumber;
        }

        /// <summary>本场战斗的唯一标识。</summary>
        public string BattleId { get; }

        /// <summary>完成移动的单位 RuntimeId。</summary>
        public string RuntimeId { get; }

        /// <summary>移动前的规则位置。</summary>
        public BFGridPosition FromGridPosition { get; }

        /// <summary>移动后的规则位置。</summary>
        public BFGridPosition ToGridPosition { get; }

        /// <summary>本次移动消耗的行动点。</summary>
        public int ActionPointCost { get; }

        /// <summary>发生本次移动时的回合编号。</summary>
        public int TurnNumber { get; }
    }
}
