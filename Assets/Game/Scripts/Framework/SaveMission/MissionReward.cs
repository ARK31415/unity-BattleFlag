using System;
using System.Collections.Generic;

namespace BF.Framework.SaveMission
{
    /// <summary>
    /// 任务奖励条目。
    /// </summary>
    [Serializable]
    public class MissionReward
    {
        /// <summary>
        /// 奖励物品 ID。
        /// </summary>
        public string ItemId;

        /// <summary>
        /// 奖励数量。
        /// </summary>
        public int Quantity;
    }

    /// <summary>
    /// 任务领取奖励结果。
    /// </summary>
    [Serializable]
    public class ClaimResult
    {
        /// <summary>
        /// 是否成功。
        /// </summary>
        public bool Success;

        /// <summary>
        /// 失败原因。
        /// </summary>
        public string ErrorMessage;

        /// <summary>
        /// 获得的金币奖励。
        /// </summary>
        public int GoldReward;

        /// <summary>
        /// 获得的物品奖励列表。
        /// </summary>
        public List<MissionReward> ItemRewards;

        public static ClaimResult Ok(int gold, List<MissionReward> items) => new()
        {
            Success = true,
            GoldReward = gold,
            ItemRewards = items ?? new List<MissionReward>()
        };

        public static ClaimResult Fail(string message) => new()
        {
            Success = false,
            ErrorMessage = message
        };
    }
}
