using System;
using System.Collections.Generic;
using UnityEngine;

namespace BF.Framework.Inventory
{
    /// <summary>
    /// 库存快照，用于存档序列化。
    /// </summary>
    [Serializable]
    public class InventorySnapshot
    {
        /// <summary>
        /// 容器定义 ID 列表。
        /// </summary>
        public List<string> ContainerDefinitionIds = new();

        /// <summary>
        /// 序列化后的物品数据（JSON）。
        /// </summary>
        public List<ItemSnapshotData> ItemData = new();
    }

    /// <summary>
    /// 单个物品快照数据。
    /// </summary>
    [Serializable]
    public class ItemSnapshotData
    {
        public string ContainerId;
        public string ItemId;
        public int Quantity;
    }

    /// <summary>
    /// 库存服务。
    /// 管理多个容器实例，提供统一的物品增减、消耗与查询入口。
    /// 不负责：物品效果逻辑、商店 UI、存档 IO。
    /// </summary>
    public class InventoryService
    {
        private readonly Dictionary<string, ContainerInstance> _containers = new();

        /// <summary>
        /// 创建并注册一个容器实例。
        /// </summary>
        public ContainerInstance CreateContainer(ContainerDefinition definition)
        {
            if (definition == null)
            {
                Debug.LogError("[InventoryService] CreateContainer: definition 为 null。");
                return null;
            }

            var container = new ContainerInstance(definition);
            _containers[definition.ContainerId] = container;
            Debug.Log($"[InventoryService] 创建容器: {definition.ContainerId} (容量: {definition.SlotCount})");
            return container;
        }

        /// <summary>
        /// 获取指定 ID 的容器实例。
        /// </summary>
        public ContainerInstance GetContainer(string containerId)
        {
            _containers.TryGetValue(containerId, out var container);
            return container;
        }

        /// <summary>
        /// 向指定容器添加物品。
        /// </summary>
        /// <returns>未能添加的溢出数量（0 = 全部添加成功）。</returns>
        public int AddItem(string containerId, ItemDefinition itemDef, int quantity)
        {
            var container = GetContainer(containerId);
            if (container == null)
            {
                Debug.LogWarning($"[InventoryService] AddItem: 容器 '{containerId}' 不存在。");
                return quantity;
            }

            return container.AddItem(itemDef, quantity);
        }

        /// <summary>
        /// 从指定容器移除物品。
        /// </summary>
        public bool RemoveItem(string containerId, string itemId, int quantity)
        {
            var container = GetContainer(containerId);
            if (container == null)
            {
                Debug.LogWarning($"[InventoryService] RemoveItem: 容器 '{containerId}' 不存在。");
                return false;
            }

            return container.RemoveItem(itemId, quantity);
        }

        /// <summary>
        /// 消耗物品（移除并记录）。
        /// </summary>
        public bool ConsumeItem(string containerId, string itemId, int quantity = 1)
        {
            bool result = RemoveItem(containerId, itemId, quantity);
            if (result)
                Debug.Log($"[InventoryService] 消耗物品: {itemId} x{quantity}");
            else
                Debug.LogWarning($"[InventoryService] 消耗失败: {itemId} x{quantity} (库存不足)");
            return result;
        }

        /// <summary>
        /// 获取指定物品的数量。
        /// </summary>
        public int GetItemCount(string containerId, string itemId)
        {
            var container = GetContainer(containerId);
            return container?.GetItemCount(itemId) ?? 0;
        }

        /// <summary>
        /// 检查是否持有足够数量的物品。
        /// </summary>
        public bool HasItem(string containerId, string itemId, int quantity)
        {
            return GetItemCount(containerId, itemId) >= quantity;
        }

        /// <summary>
        /// 检查是否可以添加指定物品（空间 + 类型校验）。
        /// </summary>
        public bool CanAddItem(string containerId, ItemDefinition itemDef, int quantity)
        {
            var container = GetContainer(containerId);
            if (container == null || itemDef == null) return false;
            if (!container.Definition.IsItemTypeAllowed(itemDef.ItemType)) return false;

            // 纯计算：不修改容器中任何物品的实际数量。
            int remaining = quantity;

            // 已有同类堆叠能吸收多少？
            foreach (var item in container.Items)
            {
                if (remaining <= 0) break;
                if (item.Definition != null && item.Definition.ItemId == itemDef.ItemId && !item.IsFull)
                {
                    int spaceLeft = itemDef.MaxStack - item.Quantity;
                    int absorbed = Mathf.Min(remaining, spaceLeft);
                    remaining -= absorbed;
                }
            }

            if (remaining <= 0) return true;

            // 还需要多少个新堆叠？
            int newStacksNeeded = 0;
            while (remaining > 0)
            {
                newStacksNeeded++;
                remaining -= itemDef.MaxStack;
            }

            return (container.Items.Count + newStacksNeeded) <= container.Definition.SlotCount;
        }

        /// <summary>
        /// 获取指定容器的所有物品。
        /// </summary>
        public IReadOnlyList<ItemInstance> GetAllItems(string containerId)
        {
            var container = GetContainer(containerId);
            return container?.GetAllItems() ?? new List<ItemInstance>();
        }

        /// <summary>
        /// 移除指定容器。
        /// </summary>
        public void RemoveContainer(string containerId)
        {
            _containers.Remove(containerId);
        }

        /// <summary>
        /// 序列化所有容器状态为 JSON 字符串。
        /// </summary>
        public string SerializeState()
        {
            var snapshot = new InventorySnapshot();

            foreach (var kvp in _containers)
            {
                if (kvp.Value?.Definition == null) continue;
                snapshot.ContainerDefinitionIds.Add(kvp.Value.Definition.ContainerId);

                foreach (var item in kvp.Value.Items)
                {
                    if (item?.Definition == null || item.Quantity <= 0) continue;
                    snapshot.ItemData.Add(new ItemSnapshotData
                    {
                        ContainerId = kvp.Key,
                        ItemId = item.Definition.ItemId,
                        Quantity = item.Quantity
                    });
                }
            }

            return JsonUtility.ToJson(snapshot);
        }

        /// <summary>
        /// 从 JSON 字符串恢复所有容器状态。
        /// 需要先创建容器（CreateContainer）再调用此方法恢复物品。
        /// </summary>
        public void RestoreState(string json, Dictionary<string, ItemDefinition> itemCatalog)
        {
            if (string.IsNullOrEmpty(json)) return;

            var snapshot = JsonUtility.FromJson<InventorySnapshot>(json);
            if (snapshot?.ItemData == null) return;

            foreach (var data in snapshot.ItemData)
            {
                if (!itemCatalog.TryGetValue(data.ItemId, out var itemDef))
                {
                    Debug.LogWarning($"[InventoryService] RestoreState: 物品定义 '{data.ItemId}' 不在 Catalog 中，跳过。");
                    continue;
                }

                AddItem(data.ContainerId, itemDef, data.Quantity);
            }

            Debug.Log($"[InventoryService] 已从存档恢复: {snapshot.ItemData.Count} 条物品记录。");
        }
    }
}
