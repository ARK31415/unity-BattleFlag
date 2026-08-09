using BF.Game.Battle.Domain.Units;

namespace BF.Game.Battle.Rules.Units
{
    /// <summary>
    /// 创建单位规则属性的纯 C# 工厂。
    /// </summary>
    public sealed class BFUnitAttributesFactory
    {
        /// <summary>
        /// 根据基础白值创建规则属性，并将当前 HP/AP 初始化到最终上限。
        /// </summary>
        public BFUnitAttributes Create(
            int baseMaxHP,
            int baseMaxActionPoints,
            int baseAttackPower,
            int baseAttackRange = 1,
            int baseAttackCost = 2)
        {
            return new BFUnitAttributes(
                baseMaxHP,
                baseMaxActionPoints,
                baseAttackPower,
                baseAttackRange: baseAttackRange,
                baseAttackCost: baseAttackCost);
        }
    }
}
