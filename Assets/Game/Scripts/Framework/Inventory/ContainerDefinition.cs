using System;
using System.Collections.Generic;
using UnityEngine;

namespace BF.Framework.Inventory
{
    /// <summary>
    /// 容器定义 ScriptableObject。
    /// 描述容器的静态属性（ID、容量、允许类型），不携带运行时可变状态。
    /// 不负责：实际物品存储、容器实例管理。
    /// </summary>
    [CreateAssetMenu(fileName = "BFContainerDefinition", menuName = "BF/Inventory/Container Definition")]
    public class ContainerDefinition : ScriptableObject
    {
        [SerializeField] private string _containerId;
        [SerializeField] private int _slotCount = 20;

        /// <summary>
        /// 允许存放的物品类型列表。为空表示允许所有类型。
        /// </summary>
        [SerializeField] private List<ItemType> _allowedItemTypes = new();

        /// <summary>
        /// 容器唯一标识。
        /// </summary>
        public string ContainerId => _containerId;

        /// <summary>
        /// 容器槽位数量。
        /// </summary>
        public int SlotCount => _slotCount;

        /// <summary>
        /// 允许的物品类型列表。
        /// </summary>
        public IReadOnlyList<ItemType> AllowedItemTypes => _allowedItemTypes;

        /// <summary>
        /// 检查指定物品类型是否允许放入此容器。
        /// </summary>
        public bool IsItemTypeAllowed(ItemType itemType)
        {
            if (_allowedItemTypes == null || _allowedItemTypes.Count == 0)
                return true;
            return _allowedItemTypes.Contains(itemType);
        }
    }
}
