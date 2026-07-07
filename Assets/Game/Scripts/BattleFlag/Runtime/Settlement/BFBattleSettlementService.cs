using System;
using System.Collections.Generic;
using BF.Framework.Inventory;
using BF.Framework.SaveMission;
using BF.Game.Runtime.Battle;
using UnityEngine;

namespace BF.Game.Runtime.Settlement
{
    /// <summary>
    /// 战斗结算结果。
    /// </summary>
    public class SettlementResult
    {
        public bool Success;
        public int GoldEarned;
        public int GoldSpent;
        public List<string> ItemsConsumedIds = new();
        public List<string> ItemsRewardedIds = new();
        public List<string> MissionsCompleted = new();
        public string ErrorMessage;
    }

    /// <summary>
    /// 战斗结算服务。
    /// 在战斗结束后执行：消耗物品结算、奖励发放、任务事件推送、存档触发。
    /// 不负责：战斗内部逻辑、UI 结算画面渲染。
    /// </summary>
    public class BFBattleSettlementService
    {
        private readonly MissionService _missionService;
        private readonly InventoryService _inventoryService;
        private readonly SaveService _saveService;
        private readonly BFGameSession _session;

        /// <summary>
        /// 创建结算服务。
        /// </summary>
        public BFBattleSettlementService(
            MissionService missionService,
            InventoryService inventoryService,
            SaveService saveService,
            BFGameSession session)
        {
            _missionService = missionService ?? throw new ArgumentNullException(nameof(missionService));
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
            _saveService = saveService ?? throw new ArgumentNullException(nameof(saveService));
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        /// <summary>
        /// 执行战斗结算（胜利时调用）。
        /// </summary>
        public SettlementResult SettleBattle(BattleResult result)
        {
            if (result == null || !result.HasResult)
                return new SettlementResult { Success = false, ErrorMessage = "无效的战斗结果。" };

            Debug.Log($"[BFBattleSettlement] 开始结算: {result.BattleId}, 结果: {(result.IsPlayerVictory ? "胜利" : "失败")}");

            if (result.IsPlayerVictory)
            {
                return SettleVictory(result);
            }
            else
            {
                return SettleDefeat(result);
            }
        }

        /// <summary>
        /// 胜利结算：发放奖励、推送任务事件、触发存档。
        /// </summary>
        private SettlementResult SettleVictory(BattleResult result)
        {
            var settlement = new SettlementResult { Success = true };

            // 1. 发放金币奖励
            _session.Gold += result.GoldEarned;
            settlement.GoldEarned = result.GoldEarned;

            // 2. 发放物品奖励
            foreach (var reward in result.ItemsRewarded)
            {
                // 需要物品 Catalog，假设由外部提供或通过其他方式获取
                // 此处记录奖励 ID，由上层配合 _inventoryService 完成实际添加
                settlement.ItemsRewardedIds.Add($"{reward.ItemId}:{reward.Quantity}");
            }

            // 3. 推进任务进度（统一入口 ReportEvent）
            foreach (var progress in result.MissionsProgressed)
            {
                if (string.IsNullOrEmpty(progress.MissionId)) continue;
                // 使用 WinBattle 事件类型推进所有相关任务
                _missionService.ReportEvent(ObjectiveType.WinBattle, result.BattleId, 1);

                // 检查是否有任务完成
                if (_missionService.IsMissionCompleted(progress.MissionId))
                    settlement.MissionsCompleted.Add(progress.MissionId);
            }

            // 4. 推进击杀统计
            _missionService.ReportEvent(ObjectiveType.Kill, "Enemy", result.GoldEarned > 0 ? 1 : 0);

            // 5. 更新会话状态
            _session.TotalBattlesWon++;
            _session.LastBattleId = result.BattleId;

            // 6. 触发存档
            SaveCurrentState();

            Debug.Log($"[BFBattleSettlement] 胜利结算完成: 金币 +{result.GoldEarned}, 完成 {settlement.MissionsCompleted.Count} 个任务。");
            return settlement;
        }

        /// <summary>
        /// 失败结算：不发放奖励，但记录消耗。
        /// </summary>
        private SettlementResult SettleDefeat(BattleResult result)
        {
            var settlement = new SettlementResult { Success = true, GoldEarned = 0 };

            Debug.Log($"[BFBattleSettlement] 失败结算完成（无奖励）。");
            return settlement;
        }

        /// <summary>
        /// 记录局内物品使用。
        /// 在战斗中使用物品时调用，而非结算时。
        /// </summary>
        public bool RecordItemUseInBattle(string itemId, int quantity = 1)
        {
            if (!_inventoryService.HasItem(_session.ActiveContainerId, itemId, quantity))
            {
                Debug.LogWarning($"[BFBattleSettlement] 局内道具使用失败: {itemId} x{quantity} 库存不足。");
                return false;
            }

            bool consumed = _inventoryService.ConsumeItem(_session.ActiveContainerId, itemId, quantity);
            if (consumed)
            {
                // 推送 UseItem 任务事件
                _missionService.ReportEvent(ObjectiveType.UseItem, itemId, quantity);
                Debug.Log($"[BFBattleSettlement] 局内使用道具: {itemId} x{quantity}");
            }
            return consumed;
        }

        /// <summary>
        /// 将当前会话状态写入存档。
        /// </summary>
        public void SaveCurrentState()
        {
            var saveData = new SaveData
            {
                Gold = _session.Gold,
                UnlockedLevelIds = new List<string>(_session.UnlockedLevelIds),
                InventoryState = _inventoryService.SerializeState(),
                MissionState = _missionService.SerializeState(),
                LastBattleId = _session.LastBattleId,
                TotalBattlesWon = _session.TotalBattlesWon
            };

            _saveService.Save(saveData);
        }

        /// <summary>
        /// 从存档恢复会话状态。
        /// </summary>
        public void RestoreFromSave(SaveData data, Dictionary<string, ItemDefinition> itemCatalog)
        {
            if (data == null) return;

            _session.Gold = data.Gold;
            _session.UnlockedLevelIds = data.UnlockedLevelIds ?? new List<string>();
            _session.LastBattleId = data.LastBattleId;
            _session.TotalBattlesWon = data.TotalBattlesWon;

            _inventoryService.RestoreState(data.InventoryState, itemCatalog);
            _missionService.RestoreState(data.MissionState);

            Debug.Log($"[BFBattleSettlement] 已从存档恢复会话状态。");
        }
    }
}
