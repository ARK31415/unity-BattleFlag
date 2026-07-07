using System.Collections.Generic;

namespace BF.Game.Runtime.Battle
{
    /// <summary>
    /// 战斗结算中的任务进度变更。
    /// </summary>
    [System.Serializable]
    public struct MissionProgressDelta
    {
        public string MissionId;
        public int ProgressAdded;

        public MissionProgressDelta(string missionId, int progressAdded)
        {
            MissionId = missionId;
            ProgressAdded = progressAdded;
        }
    }

    /// <summary>
    /// 战斗结算中的物品变更记录。
    /// </summary>
    [System.Serializable]
    public struct ItemChangeRecord
    {
        /// <summary>
        /// 物品定义 ID。
        /// </summary>
        public string ItemId;

        /// <summary>
        /// 变更数量。
        /// </summary>
        public int Quantity;

        public ItemChangeRecord(string itemId, int quantity)
        {
            ItemId = itemId;
            Quantity = quantity;
        }
    }

    /// <summary>
    /// 战斗结果数据结构。
    /// 包含胜负判断结果与结算信息（P2 扩展：金币、物品消耗与奖励、任务进度）。
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
        public Units.UnitFaction WinnerFaction { get; set; } = Units.UnitFaction.None;

        /// <summary>
        /// 是否玩家胜利。
        /// </summary>
        public bool IsPlayerVictory => WinnerFaction == Units.UnitFaction.Player;

        /// <summary>
        /// 战斗 ID。
        /// </summary>
        public string BattleId { get; set; }

        /// <summary>
        /// 总回合数。
        /// </summary>
        public int TotalTurns { get; set; }

        // === P2 结算扩展字段 ===

        /// <summary>
        /// 获得的金币奖励。
        /// </summary>
        public int GoldEarned { get; set; }

        /// <summary>
        /// 战斗中消耗的物品列表。
        /// </summary>
        public List<ItemChangeRecord> ItemsConsumed { get; set; } = new();

        /// <summary>
        /// 战斗奖励物品列表。
        /// </summary>
        public List<ItemChangeRecord> ItemsRewarded { get; set; } = new();

        /// <summary>
        /// 战斗中的任务进度变更列表。
        /// </summary>
        public List<MissionProgressDelta> MissionsProgressed { get; set; } = new();

        /// <summary>
        /// 创建胜利结果。
        /// </summary>
        public static BattleResult Victory(string battleId, int turns)
        {
            return new BattleResult
            {
                HasResult = true,
                WinnerFaction = Units.UnitFaction.Player,
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
                WinnerFaction = Units.UnitFaction.Enemy,
                BattleId = battleId,
                TotalTurns = turns
            };
        }
    }
}
