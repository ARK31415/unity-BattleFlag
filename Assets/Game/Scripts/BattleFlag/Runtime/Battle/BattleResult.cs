using System;
using BF.Game.Runtime.Battle.Units;

namespace BF.Game.Runtime.Battle
{
    /// <summary>
    /// 战斗结果数据结构。
    /// </summary>
    public class BattleResult
    {
        /// <summary>
        /// 是否已产生结果。
        /// </summary>
        public bool HasResult { get; set; }

        /// <summary>
        /// 获胜阵营。
        /// </summary>
        public UnitFaction WinnerFaction { get; set; } = UnitFaction.None;

        /// <summary>
        /// 是否玩家胜利。
        /// </summary>
        public bool IsPlayerVictory => WinnerFaction == UnitFaction.Player;

        /// <summary>
        /// 战斗 ID。
        /// </summary>
        public string BattleId { get; set; }

        /// <summary>
        /// 总回合数。
        /// </summary>
        public int TotalTurns { get; set; }

        /// <summary>
        /// 创建胜利结果。
        /// </summary>
        public static BattleResult Victory(string battleId, int turns)
        {
            return new BattleResult
            {
                HasResult = true,
                WinnerFaction = UnitFaction.Player,
                BattleId = battleId,
                TotalTurns = turns
            };
        }

        /// <summary>
        /// 创建失败结果。
        /// </summary>
        public static BattleResult Defeat(string battleId, int turns)
        {
            return new BattleResult
            {
                HasResult = true,
                WinnerFaction = UnitFaction.Enemy,
                BattleId = battleId,
                TotalTurns = turns
            };
        }
    }
}
