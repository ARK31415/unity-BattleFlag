using System;
using UnityEngine;

namespace BF.Framework.Inventory
{
    /// <summary>
    /// 物品运行时实例。
    /// 携带物品定义引用与当前数量，可序列化以支持存档。
    /// 不负责：物品效果逻辑、UI 表现。
    /// </summary>
    [Serializable]
    public class ItemInstance
    {
        /// <summary>
        /// 物品定义引用。
        /// </summary>
        [field: SerializeField] public ItemDefinition Definition { get; private set; }

        /// <summary>
        /// 当前堆叠数量。
        /// </summary>
        [field: SerializeField] public int Quantity { get; set; }

        /// <summary>
        /// 创建物品实例。
        /// </summary>
        public ItemInstance(ItemDefinition definition, int quantity = 1)
        {
            Definition = definition;
            Quantity = Mathf.Max(1, quantity);
        }

        /// <summary>
        /// 判断是否可以与另一个实例堆叠。
        /// </summary>
        public bool CanStackWith(ItemInstance other)
        {
            if (other == null || other.Definition == null || Definition == null)
                return false;
            return Definition.ItemId == other.Definition.ItemId
                   && Quantity < Definition.MaxStack
                   && other.Quantity < other.Definition.MaxStack;
        }

        /// <summary>
        /// 尝试添加数量，返回未能添加的溢出量。
        /// </summary>
        public int AddAmount(int amount)
        {
            if (Definition == null) return amount;

            int spaceLeft = Definition.MaxStack - Quantity;
            int added = Mathf.Min(amount, spaceLeft);
            Quantity += added;
            return amount - added;
        }

        /// <summary>
        /// 尝试移除数量，返回实际移除量。
        /// </summary>
        public int RemoveAmount(int amount)
        {
            int removed = Mathf.Min(amount, Quantity);
            Quantity -= removed;
            return removed;
        }

        /// <summary>
        /// 是否已达到最大堆叠。
        /// </summary>
        public bool IsFull => Definition != null && Quantity >= Definition.MaxStack;

        /// <summary>
        /// 创建此实例的深拷贝。
        /// </summary>
        public ItemInstance Clone()
        {
            return new ItemInstance(Definition, Quantity);
        }
    }
}
