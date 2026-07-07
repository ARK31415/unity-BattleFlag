using System;
using System.Collections.Generic;
using UnityEngine;

namespace BF.Framework.Inventory
{
    /// <summary>
    /// 容器运行时实例。
    /// 管理容器内的物品存储、增减与查询。可序列化以支持存档。
    /// 不负责：物品定义加载、UI 拖拽逻辑。
    /// 预留：网格型库存接口（v2）。
    /// </summary>
    [Serializable]
    public class ContainerInstance
    {
        /// <summary>
        /// 关联的容器定义。
        /// </summary>
        [field: SerializeField] public ContainerDefinition Definition { get; private set; }

        /// <summary>
        /// 容器内物品列表。
        /// </summary>
        [field: SerializeField] public List<ItemInstance> Items { get; private set; } = new();

        public ContainerInstance(ContainerDefinition definition)
        {
            Definition = definition;
            Items = new List<ItemInstance>();
        }

        /// <summary>
        /// 向容器添加物品，自动处理堆叠。返回未能添加的数量（0 表示全部添加成功）。
        /// </summary>
        public int AddItem(ItemDefinition itemDef, int quantity)
        {
            if (itemDef == null || quantity <= 0) return quantity;
            if (!Definition.IsItemTypeAllowed(itemDef.ItemType)) return quantity;

            int remaining = quantity;

            // 先尝试堆叠到已有同类物品
            foreach (var existing in Items)
            {
                if (remaining <= 0) break;
                if (existing.Definition == null) continue;
                if (existing.Definition.ItemId != itemDef.ItemId) continue;
                if (existing.IsFull) continue;

                remaining = existing.AddAmount(remaining);
            }

            // 如果还有剩余，创建新堆叠
            while (remaining > 0 && !IsFull)
            {
                int stackQty = Mathf.Min(remaining, itemDef.MaxStack);
                Items.Add(new ItemInstance(itemDef, stackQty));
                remaining -= stackQty;
            }

            // 清理空堆叠
            CleanupEmptyStacks();
            return remaining;
        }

        /// <summary>
        /// 从容器移除指定数量的物品。返回是否成功移除全部数量。
        /// </summary>
        public bool RemoveItem(string itemId, int quantity)
        {
            if (string.IsNullOrEmpty(itemId) || quantity <= 0) return false;

            int totalAvailable = GetItemCount(itemId);
            if (totalAvailable < quantity) return false;

            int remaining = quantity;
            foreach (var item in Items)
            {
                if (remaining <= 0) break;
                if (item.Definition == null) continue;
                if (item.Definition.ItemId != itemId) continue;

                int removed = item.RemoveAmount(remaining);
                remaining -= removed;
            }

            CleanupEmptyStacks();
            return remaining <= 0;
        }

        /// <summary>
        /// 获取指定物品的总数量。
        /// </summary>
        public int GetItemCount(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;

            int total = 0;
            foreach (var item in Items)
            {
                if (item.Definition != null && item.Definition.ItemId == itemId)
                    total += item.Quantity;
            }
            return total;
        }

        /// <summary>
        /// 检查是否持有指定数量的物品。
        /// </summary>
        public bool HasItem(string itemId, int quantity)
        {
            return GetItemCount(itemId) >= quantity;
        }

        /// <summary>
        /// 容器是否已满（无法放入新物品）。
        /// </summary>
        public bool IsFull => Items.Count >= Definition.SlotCount;

        /// <summary>
        /// 清空容器。
        /// </summary>
        public void Clear()
        {
            Items.Clear();
        }

        /// <summary>
        /// 获取所有物品列表（只读）。
        /// </summary>
        public IReadOnlyList<ItemInstance> GetAllItems() => Items;

        /// <summary>
        /// 创建此容器的深拷贝。
        /// </summary>
        public ContainerInstance Clone()
        {
            var clone = new ContainerInstance(Definition);
            foreach (var item in Items)
            {
                if (item != null)
                    clone.Items.Add(item.Clone());
            }
            return clone;
        }

        /// <summary>
        /// 预留（v2）：获取指定网格位置的物品。
        /// </summary>
        public ItemInstance GetItemAtSlot(int x, int y)
        {
            int index = y * Definition.SlotCount + x;
            if (index >= 0 && index < Items.Count)
                return Items[index];
            return null;
        }

        /// <summary>
        /// 预留（v2）：将物品放置到指定网格位置。
        /// </summary>
        public void PlaceItemAtSlot(ItemInstance item, int x, int y)
        {
            int index = y * Definition.SlotCount + x;
            if (index < 0 || index >= Definition.SlotCount) return;

            while (Items.Count <= index)
                Items.Add(null);

            Items[index] = item;
            CleanupEmptyStacks();
        }

        /// <summary>
        /// 清理数量为 0 或 null 的物品条目。
        /// </summary>
        private void CleanupEmptyStacks()
        {
            Items.RemoveAll(item => item == null || item.Quantity <= 0);
        }
    }
}
