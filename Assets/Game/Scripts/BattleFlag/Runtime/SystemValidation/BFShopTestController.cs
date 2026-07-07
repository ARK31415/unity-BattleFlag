using BF.Framework.Inventory;
using BF.Game.Runtime.Settlement;
using System.Collections.Generic;
using UnityEngine;

namespace BF.Game.Runtime.SystemValidation
{
    /// <summary>
    /// 简易商店测试控制器。
    /// 在独立场景中提供可交互的商店 UI（OnGUI），
    /// 支持查看商品、购买、查看背包、出售。
    /// </summary>
    public class BFShopTestController : MonoBehaviour
    {
        [Header("SO Assets")]
        [SerializeField] private ItemDefinition _healthPotionDef;
        [SerializeField] private ItemDefinition _attackElixirDef;
        [SerializeField] private ContainerDefinition _backpackDef;

        private BFGameSession _session;
        private InventoryService _inventory;
        private ShopService _shop;

        private string _statusMessage = "";
        private float _statusTimer;

        private void Start()
        {
            // 初始化
            _session = BFGameSession.CreateDefault(); // Gold=500
            _inventory = new InventoryService();
            _inventory.CreateContainer(_backpackDef);

            _shop = new ShopService(_inventory, _session.ActiveContainerId);
            _shop.LoadCatalog(new List<ItemDefinition> { _healthPotionDef, _attackElixirDef });
            _shop.GetGold = () => _session.Gold;
            _shop.SetGold = (v) => _session.Gold = v;

            SetStatus("商店已就绪，欢迎光临！");
        }

        private void Update()
        {
            if (_statusTimer > 0)
            {
                _statusTimer -= Time.deltaTime;
                if (_statusTimer <= 0) _statusMessage = "";
            }
        }

        private void OnGUI()
        {
            var titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            var sectionStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            var itemStyle = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            var btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 16 };
            var statusStyle = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            var goldStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };

            // === 顶部标题栏 ===
            GUILayout.BeginArea(new Rect(20, 20, 560, 60));
            GUILayout.Label("🏪 BattleFlag 商店测试", titleStyle);
            GUILayout.EndArea();

            // 金币
            GUILayout.BeginArea(new Rect(600, 20, 200, 60));
            goldStyle.normal.textColor = Color.yellow;
            GUILayout.Label($"💰 金币: {_session.Gold}", goldStyle);
            GUILayout.EndArea();

            // === 商品区 ===
            GUILayout.BeginArea(new Rect(20, 100, 380, 300));
            GUILayout.Label("── 商品列表 ──", sectionStyle);
            GUILayout.Space(10);

            foreach (var item in _shop.GetAvailableItems())
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{(item.ItemType == ItemType.Consumable ? "🧪" : "📦")} {item.ItemName}", itemStyle, GUILayout.Width(180));
                GUILayout.Label($"💰{item.BuyPrice}G", itemStyle, GUILayout.Width(70));

                bool canAfford = _shop.CanAfford(item.ItemId, 1);
                bool canAdd = _inventory.CanAddItem(_session.ActiveContainerId, item, 1);

                GUI.enabled = canAfford && canAdd;
                if (GUILayout.Button("购买 x1", btnStyle, GUILayout.Width(100)))
                {
                    var result = _shop.Purchase(item.ItemId, 1);
                    if (result.Success)
                        SetStatus($"✅ 购买了 {item.ItemName} x1，花费 {result.TotalCost}G");
                    else
                        SetStatus($"❌ {result.ErrorMessage}");
                }
                GUI.enabled = true;

                GUILayout.EndHorizontal();

                if (!canAfford) GUILayout.Label("  ⚠ 金币不足", statusStyle);
                else if (!canAdd) GUILayout.Label("  ⚠ 背包已满", statusStyle);
            }
            GUILayout.EndArea();

            // === 背包区 ===
            GUILayout.BeginArea(new Rect(420, 100, 380, 300));
            GUILayout.Label("── 我的背包 ──", sectionStyle);
            GUILayout.Space(10);

            var items = _inventory.GetAllItems(_session.ActiveContainerId);
            if (items.Count == 0)
            {
                GUILayout.Label("  （空）", itemStyle);
            }
            else
            {
                foreach (var item in items)
                {
                    var def = item.Definition;
                    string itemName = def != null ? def.ItemName : "未知物品";

                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{(def?.ItemType == ItemType.Consumable ? "🧪" : "📦")} {itemName} x{item.Quantity}", itemStyle, GUILayout.Width(200));

                    if (def != null && def.SellPrice > 0)
                    {
                        if (GUILayout.Button($"出售 x1 (+{def.SellPrice}G)", btnStyle, GUILayout.Width(150)))
                        {
                            var result = _shop.Sell(def.ItemId, 1);
                            if (result.Success)
                                SetStatus($"✅ 出售了 {itemName} x1，获得 {def.SellPrice}G");
                            else
                                SetStatus($"❌ {result.ErrorMessage}");
                        }
                    }
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndArea();

            // === 状态消息 ===
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                GUILayout.BeginArea(new Rect(20, 420, 780, 40));
                if (_statusMessage.StartsWith("✅"))
                    statusStyle.normal.textColor = Color.green;
                else if (_statusMessage.StartsWith("❌"))
                    statusStyle.normal.textColor = Color.red;
                else
                    statusStyle.normal.textColor = Color.white;

                GUILayout.Label(_statusMessage, statusStyle);
                GUILayout.EndArea();
            }

            // === 底部操作 ===
            GUILayout.BeginArea(new Rect(20, 470, 780, 40));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("重置 (金币=500, 清空背包)", btnStyle))
            {
                _session.Gold = 500;
                _inventory.GetContainer(_session.ActiveContainerId)?.Clear();
                SetStatus("🔄 已重置");
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void SetStatus(string msg)
        {
            _statusMessage = msg;
            _statusTimer = 3f;
            Debug.Log($"[ShopTest] {msg}");
        }

    }
}
