using BF.Game.Battle.Domain.Events;

namespace BF.Game.Battle.Domain
{
    /// <summary>
    /// 纯规则战斗结果。
    ///
    /// 结果由规则流程在战斗完成前写入 Context，表现层只能读取结果，不负责重新计算胜负。
    /// </summary>
    public sealed class BattleResult
    {
        private BattleResult(string battleId, BFUnitFaction winnerFaction, int totalTurns)
        {
            HasResult = true;
            BattleId = battleId ?? string.Empty;
            WinnerFaction = winnerFaction;
            TotalTurns = totalTurns;
        }

        /// <summary>表示结果是否已经产生。</summary>
        public bool HasResult { get; }

        /// <summary>获胜阵营。</summary>
        public BFUnitFaction WinnerFaction { get; }

        /// <summary>对应的战斗身份。</summary>
        public string BattleId { get; }

        /// <summary>战斗完成时的总回合数。</summary>
        public int TotalTurns { get; }

        /// <summary>创建玩家胜利结果。</summary>
        public static BattleResult Victory(string battleId, int totalTurns)
        {
            return new BattleResult(battleId, BFUnitFaction.Player, totalTurns);
        }

        /// <summary>创建敌方胜利结果。</summary>
        public static BattleResult Defeat(string battleId, int totalTurns)
        {
            return new BattleResult(battleId, BFUnitFaction.Enemy, totalTurns);
        }
    }
}
