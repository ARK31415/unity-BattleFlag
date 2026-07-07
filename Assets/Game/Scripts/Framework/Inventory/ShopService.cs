using System;
using System.Collections.Generic;
using UnityEngine;

namespace BF.Framework.Inventory
{
    /// <summary>
    /// 购买结果。
    /// </summary>
    [Serializable]
    public class ShopResult
    {
        /// <summary>
        /// 是否购买成功。
        /// </summary>
        public bool Success;

        /// <summary>
        /// 错误信息（失败时）。
        /// </summary>
        public string ErrorMessage;

        /// <summary>
        /// 总花费。
        /// </summary>
        public int TotalCost;

        /// <summary>
        /// 创建成功结果。
        /// </summary>
        public static ShopResult Ok(int totalCost) => new()
        {
            Success = true,
            TotalCost = totalCost
        };

        /// <summary>
        /// 创建失败结果。
        /// </summary>
        public static ShopResult Fail(string message) => new()
        {
            Success = false,
            ErrorMessage = message,
            TotalCost = 0
        };
    }

    /// <summary>
    /// 商店服务。
    /// 管理商品目录、购买校验与交易执行。
    /// 不负责：UI 表现、商品刷新逻辑、货币定义。
    /// </summary>
    public class ShopService
    {
        private readonly InventoryService _inventory;
        private readonly string _defaultContainerId;

        /// <summary>
        /// 商店商品目录。
        /// </summary>
        public List<ItemDefinition> ShopCatalog { get; private set; } = new();

        /// <summary>
        /// 玩家金币获取/设置委托。
        /// </summary>
        public Func<int> GetGold { get; set; }
        public Action<int> SetGold { get; set; }

        /// <summary>
        /// 创建商店服务。
        /// </summary>
        /// <param name="inventory">库存服务。</param>
        /// <param name="defaultContainerId">默认存入的容器 ID。</param>
        public ShopService(InventoryService inventory, string defaultContainerId)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _defaultContainerId = defaultContainerId;
        }

        /// <summary>
        /// 从 ScriptableObject 目录加载商品列表。
        /// </summary>
        public void LoadCatalog(List<ItemDefinition> catalog)
        {
            ShopCatalog = catalog ?? new List<ItemDefinition>();
            Debug.Log($"[ShopService] 加载商品目录: {ShopCatalog.Count} 件商品。");
        }

        /// <summary>
        /// 获取所有可购买商品。
        /// </summary>
        public IReadOnlyList<ItemDefinition> GetAvailableItems() => ShopCatalog;

        /// <summary>
        /// 检查是否买得起指定商品。
        /// </summary>
        public bool CanAfford(string itemId, int quantity = 1)
        {
            var itemDef = ShopCatalog.Find(i => i.ItemId == itemId);
            if (itemDef == null) return false;

            int currentGold = GetGold?.Invoke() ?? 0;
            return currentGold >= itemDef.BuyPrice * quantity;
        }

        /// <summary>
        /// 购买物品。先校验金币和库存空间，再扣钱加货。
        /// </summary>
        public ShopResult Purchase(string itemId, int quantity = 1)
        {
            if (quantity <= 0)
                return ShopResult.Fail("购买数量必须大于 0。");

            var itemDef = ShopCatalog.Find(i => i.ItemId == itemId);
            if (itemDef == null)
                return ShopResult.Fail($"商品 '{itemId}' 不在商店目录中。");

            if (itemDef.BuyPrice <= 0)
                return ShopResult.Fail($"商品 '{itemId}' 不可购买。");

            int currentGold = GetGold?.Invoke() ?? 0;
            int totalCost = itemDef.BuyPrice * quantity;

            if (currentGold < totalCost)
                return ShopResult.Fail($"金币不足：需要 {totalCost}，当前 {currentGold}。");

            // 校验库存空间
            if (!_inventory.CanAddItem(_defaultContainerId, itemDef, quantity))
                return ShopResult.Fail($"背包空间不足，无法购买 {itemDef.ItemName} x{quantity}。");

            // 扣钱
            SetGold?.Invoke(currentGold - totalCost);

            // 加货
            int overflow = _inventory.AddItem(_defaultContainerId, itemDef, quantity);
            if (overflow > 0)
            {
                // 回滚金币
                int actualAdded = quantity - overflow;
                int refund = itemDef.BuyPrice * overflow;
                SetGold?.Invoke((GetGold?.Invoke() ?? 0) + refund);
                Debug.LogWarning($"[ShopService] 购买部分成功: {itemId} x{actualAdded}，退回 {refund} 金币。");
                return ShopResult.Ok(totalCost - refund);
            }

            Debug.Log($"[ShopService] 购买成功: {itemId} x{quantity}，花费 {totalCost} 金币。");
            return ShopResult.Ok(totalCost);
        }

        /// <summary>
        /// 出售物品。以出售价格回收购入。
        /// </summary>
        public ShopResult Sell(string itemId, int quantity = 1)
        {
            if (quantity <= 0)
                return ShopResult.Fail("出售数量必须大于 0。");

            var itemDef = ShopCatalog.Find(i => i.ItemId == itemId);
            if (itemDef == null)
            {
                // 不在商店目录也可以出售，按默认价格
                if (!_inventory.HasItem(_defaultContainerId, itemId, quantity))
                    return ShopResult.Fail($"库存中没有 '{itemId}' x{quantity}。");
                _inventory.RemoveItem(_defaultContainerId, itemId, quantity);
                Debug.Log($"[ShopService] 出售成功: {itemId} x{quantity}（未知物品，无金币收益）。");
                return ShopResult.Ok(0);
            }

            if (!_inventory.HasItem(_defaultContainerId, itemId, quantity))
                return ShopResult.Fail($"库存中没有 {itemDef.ItemName} x{quantity}。");

            _inventory.RemoveItem(_defaultContainerId, itemId, quantity);
            int totalRevenue = itemDef.SellPrice * quantity;
            int currentGold = GetGold?.Invoke() ?? 0;
            SetGold?.Invoke(currentGold + totalRevenue);

            Debug.Log($"[ShopService] 出售成功: {itemId} x{quantity}，获得 {totalRevenue} 金币。");
            return ShopResult.Ok(totalRevenue);
        }
    }
}
