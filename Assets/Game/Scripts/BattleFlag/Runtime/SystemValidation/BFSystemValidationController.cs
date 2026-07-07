using System.Collections;
using System.Collections.Generic;
using BF.Framework.Inventory;
using BF.Framework.SaveMission;
using BF.Game.Runtime.Settlement;
using UnityEngine;

namespace BF.Game.Runtime.SystemValidation
{
    /// <summary>
    /// 系统验证关控制器。
    /// 在 Awake 中创建所有 P2 子系统，按顺序执行验证步骤：
    /// 创建默认会话 → 创建容器 → 加载商品 → 购买道具 → 验证库存 →
    /// 模拟局内道具使用 → 模拟战斗结算 → 写入存档 → 读档验证持久化。
    /// 不负责：正式关卡逻辑、UI 美化。
    /// </summary>
    public class BFSystemValidationController : MonoBehaviour
    {
        [Header("SO Assets")]
        [SerializeField] private ItemDefinition _healthPotionDef;
        [SerializeField] private ItemDefinition _attackElixirDef;
        [SerializeField] private ContainerDefinition _backpackDef;
        [SerializeField] private MissionDefinition _missionDefeatEnemies;
        [SerializeField] private MissionDefinition _missionUsePotion;
        [SerializeField] private MissionDefinition _missionWinBattle;

        private BFGameSession _session;
        private InventoryService _inventory;
        private ShopService _shop;
        private MissionService _mission;
        private SaveService _save;
        private BFBattleSettlementService _settlement;

        private List<string> _stepResults = new();

        private IEnumerator Start()
        {
            LogStep("=== P2 系统验证开始 ===", true);

            // Step 1: 创建默认会话
            _session = BFGameSession.CreateDefault();
            LogStep("Step 1: 创建默认会话", _session != null, $"Gold={_session.Gold}");

            // Step 2: 创建服务实例
            _inventory = new InventoryService();
            _save = new SaveService();
            _mission = new MissionService();
            _shop = new ShopService(_inventory, _session.ActiveContainerId);
            _settlement = new BFBattleSettlementService(_mission, _inventory, _save, _session);
            LogStep("Step 2: 创建服务实例", true);

            // Step 3: 创建背包容器
            var container = _inventory.CreateContainer(_backpackDef);
            LogStep("Step 3: 创建背包容器", container != null, $"ContainerId={_backpackDef?.ContainerId}");

            // Step 4: 加载商店目录
            var catalog = new List<ItemDefinition> { _healthPotionDef, _attackElixirDef };
            _shop.LoadCatalog(catalog);
            _shop.GetGold = () => _session.Gold;
            _shop.SetGold = (v) => _session.Gold = v;
            LogStep("Step 4: 加载商店目录", _shop.GetAvailableItems().Count == 2);

            // Step 5: 购买道具
            var purchaseResult = _shop.Purchase("BFHealthPotion", 2);
            LogStep("Step 5: 购买生命药水 x2", purchaseResult.Success,
                $"花费={purchaseResult.TotalCost}, 剩余金币={_session.Gold}");

            // Step 6: 验证库存
            int potionCount = _inventory.GetItemCount(_session.ActiveContainerId, "BFHealthPotion");
            LogStep("Step 6: 验证库存（应有 2 瓶药水）", potionCount == 2, $"实际数量={potionCount}");

            // Step 7: 加载任务目录并激活任务
            var missionCatalog = new List<MissionDefinition> { _missionDefeatEnemies, _missionUsePotion, _missionWinBattle };
            _mission.LoadCatalog(missionCatalog);
            foreach (var def in missionCatalog)
            {
                if (def != null) _mission.ActivateMission(def.MissionId);
            }
            LogStep("Step 7: 激活任务", _mission.GetActiveMissions().Count == 3,
                $"活跃任务数={_mission.GetActiveMissions().Count}");

            // Step 8: 模拟局内道具使用
            _mission.ReportEvent(ObjectiveType.UseItem, "BFHealthPotion", 1);
            _settlement.RecordItemUseInBattle("BFHealthPotion", 1);
            potionCount = _inventory.GetItemCount(_session.ActiveContainerId, "BFHealthPotion");
            LogStep("Step 8: 局内使用药水 x1", potionCount == 1, $"剩余={potionCount}");

            // Step 9: 模拟击杀敌人
            _mission.ReportEvent(ObjectiveType.Kill, "Enemy", 2);
            bool defeatDone = _mission.IsMissionCompleted("Mission_DefeatEnemies");
            LogStep("Step 9: 击杀敌人 x2 → 任务完成", defeatDone);

            // Step 10: 模拟战斗胜利结算
            var battleResult = Battle.BattleResult.Victory("ValidationBattle", 3);
            battleResult.GoldEarned = 150;
            var settlement = _settlement.SettleBattle(battleResult);
            LogStep("Step 10: 战斗胜利结算", settlement.Success,
                $"金币+{settlement.GoldEarned}, 完成{settlement.MissionsCompleted.Count}个任务");

            // Step 11: 手动写入存档
            _settlement.SaveCurrentState();
            LogStep("Step 11: 写入存档", _save.HasSave());

            // Step 12: 读档验证持久化
            var loadedData = _save.Load();
            bool loadOk = loadedData != null && loadedData.Gold > 0;
            LogStep("Step 12: 读档验证", loadOk, $"金币={loadedData?.Gold}, 版本={loadedData?.Version}");

            // Step 13: 验证完整闭环
            bool hasInventoryData = !string.IsNullOrEmpty(loadedData?.InventoryState);
            bool hasMissionData = !string.IsNullOrEmpty(loadedData?.MissionState);
            LogStep("Step 13: 完整闭环（库存+任务已持久化）", hasInventoryData && hasMissionData,
                $"库存数据:{(hasInventoryData ? "有" : "无")}, 任务数据:{(hasMissionData ? "有" : "无")}");

            // 汇总
            int passed = 0, failed = 0;
            foreach (var r in _stepResults)
            {
                if (r.Contains("[PASS]")) passed++;
                else if (r.Contains("[FAIL]")) failed++;
            }
            Debug.Log($"[SystemValidation] === 验证完成: {passed} 通过, {failed} 失败 ===");
            yield return null;
        }

        private void LogStep(string description, bool passed, string detail = "")
        {
            string result = passed ? "[PASS]" : "[FAIL]";
            string line = $"{result} {description}";
            if (!string.IsNullOrEmpty(detail)) line += $" ({detail})";
            _stepResults.Add(line);
            Debug.Log($"[SystemValidation] {line}");
        }

        private void OnGUI()
        {
            if (_stepResults.Count == 0) return;

            GUILayout.BeginArea(new Rect(20, 20, 600, 500));
            GUILayout.Label("P2 系统验证结果", new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold });

            foreach (var result in _stepResults)
            {
                var style = new GUIStyle(GUI.skin.label) { fontSize = 16 };
                if (result.Contains("[FAIL]"))
                    style.normal.textColor = Color.red;
                else if (result.Contains("[PASS]"))
                    style.normal.textColor = Color.green;

                GUILayout.Label($"  {result}", style);
            }

            GUILayout.EndArea();
        }
    }
}
