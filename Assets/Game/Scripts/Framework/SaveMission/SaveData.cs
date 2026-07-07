using System;
using System.Collections.Generic;
using UnityEngine;

namespace BF.Framework.SaveMission
{
    /// <summary>
    /// 可序列化的全局存档数据。
    /// 定义持久化边界，不包含场景对象引用。
    /// 不负责：存档 IO、数据校验。
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>
        /// 存档版本号，用于兼容性校验。
        /// </summary>
        public int Version = 1;

        /// <summary>
        /// 玩家金币。
        /// </summary>
        public int Gold;

        /// <summary>
        /// 已解锁的关卡 ID 列表。
        /// </summary>
        public List<string> UnlockedLevelIds = new();

        /// <summary>
        /// 库存状态的序列化 JSON。
        /// </summary>
        public string InventoryState;

        /// <summary>
        /// 任务状态的序列化 JSON。
        /// </summary>
        public string MissionState;

        /// <summary>
        /// 最近一场战斗的 ID。
        /// </summary>
        public string LastBattleId;

        /// <summary>
        /// 总胜利次数。
        /// </summary>
        public int TotalBattlesWon;

        /// <summary>
        /// 保存时间戳。
        /// </summary>
        public string SavedAtTimestamp;

        /// <summary>
        /// 创建默认存档。
        /// </summary>
        public static SaveData CreateDefault()
        {
            return new SaveData
            {
                Version = 1,
                Gold = 500,
                UnlockedLevelIds = new List<string> { "BFBattleTest" },
                InventoryState = string.Empty,
                MissionState = string.Empty,
                LastBattleId = string.Empty,
                TotalBattlesWon = 0,
                SavedAtTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
        }

        /// <summary>
        /// 序列化为 JSON 字符串。
        /// </summary>
        public string ToJson()
        {
            return JsonUtility.ToJson(this, true);
        }

        /// <summary>
        /// 从 JSON 字符串反序列化。
        /// </summary>
        public static SaveData FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            return JsonUtility.FromJson<SaveData>(json);
        }
    }
}
