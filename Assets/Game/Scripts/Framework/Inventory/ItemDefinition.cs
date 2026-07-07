using UnityEngine;

namespace BF.Framework.Inventory
{
    /// <summary>
    /// 物品类型枚举。
    /// </summary>
    public enum ItemType
    {
        /// <summary>消耗品（药水、卷轴等）。</summary>
        Consumable,
        /// <summary>装备（武器、防具等）。</summary>
        Equipment,
        /// <summary>关键道具（任务物品等）。</summary>
        KeyItem
    }

    /// <summary>
    /// 物品定义 ScriptableObject。
    /// 描述物品的静态属性，不携带运行时可变状态。
    /// 不负责：库存数量追踪、运行时效果逻辑。
    /// </summary>
    [CreateAssetMenu(fileName = "BFItemDefinition", menuName = "BF/Inventory/Item Definition")]
    public class ItemDefinition : ScriptableObject
    {
        [SerializeField] private string _itemId;
        [SerializeField] private string _itemName;
        [SerializeField] private ItemType _itemType = ItemType.Consumable;
        [TextArea(2, 4)]
        [SerializeField] private string _description;
        [SerializeField] private Sprite _icon;
        [SerializeField] private int _maxStack = 99;
        [SerializeField] private int _buyPrice;
        [SerializeField] private int _sellPrice;

        /// <summary>
        /// 物品唯一标识。
        /// </summary>
        public string ItemId => _itemId;

        /// <summary>
        /// 物品显示名称。
        /// </summary>
        public string ItemName => _itemName;

        /// <summary>
        /// 物品类型。
        /// </summary>
        public ItemType ItemType => _itemType;

        /// <summary>
        /// 物品描述。
        /// </summary>
        public string Description => _description;

        /// <summary>
        /// 物品图标。
        /// </summary>
        public Sprite Icon => _icon;

        /// <summary>
        /// 最大堆叠数量。
        /// </summary>
        public int MaxStack => _maxStack;

        /// <summary>
        /// 购买价格。
        /// </summary>
        public int BuyPrice => _buyPrice;

        /// <summary>
        /// 出售价格。
        /// </summary>
        public int SellPrice => _sellPrice;
    }
}
