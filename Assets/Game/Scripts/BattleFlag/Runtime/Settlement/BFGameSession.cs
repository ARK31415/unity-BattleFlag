using System;
using System.Collections.Generic;

namespace BF.Game.Runtime.Settlement
{
    /// <summary>
    /// 游戏会话状态载体（非 Singleton）。
    /// 承载跨场景的全局玩家状态：金币、容器 ID、已解锁关卡、活跃任务列表。
    /// 在 Boot 阶段创建/加载，注入到各子系统。
    /// 不负责：持久化 IO、UI 表现。
    /// </summary>
    [Serializable]
    public class BFGameSession
    {
        /// <summary>
        /// 玩家金币。
        /// </summary>
        public int Gold;

        /// <summary>
        /// 玩家背包容器 ID。
        /// </summary>
        public string ActiveContainerId = "PlayerBackpack";

        /// <summary>
        /// 已解锁的关卡 ID 列表。
        /// </summary>
        public List<string> UnlockedLevelIds = new();

        /// <summary>
        /// 当前活跃的任务 ID 列表。
        /// </summary>
        public List<string> ActiveMissionIds = new();

        /// <summary>
        /// 总胜利次数。
        /// </summary>
        public int TotalBattlesWon;

        /// <summary>
        /// 最近一场战斗的 ID。
        /// </summary>
        public string LastBattleId;

        /// <summary>
        /// 从存档创建会话。
        /// </summary>
        public static BFGameSession CreateFromSave(Framework.SaveMission.SaveData data)
        {
            if (data == null) return CreateDefault();

            return new BFGameSession
            {
                Gold = data.Gold,
                UnlockedLevelIds = data.UnlockedLevelIds ?? new List<string>(),
                ActiveMissionIds = new List<string>(),
                TotalBattlesWon = data.TotalBattlesWon,
                LastBattleId = data.LastBattleId,
                ActiveContainerId = "PlayerBackpack"
            };
        }

        /// <summary>
        /// 创建默认新会话。
        /// </summary>
        public static BFGameSession CreateDefault()
        {
            return new BFGameSession
            {
                Gold = 500,
                UnlockedLevelIds = new List<string> { "BFBattleTest" },
                ActiveMissionIds = new List<string>(),
                TotalBattlesWon = 0,
                LastBattleId = string.Empty,
                ActiveContainerId = "PlayerBackpack"
            };
        }
    }
}
