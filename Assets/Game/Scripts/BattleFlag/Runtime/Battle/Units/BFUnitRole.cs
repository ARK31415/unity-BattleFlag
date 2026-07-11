using UnityEngine;

namespace BF.Game.Runtime.Battle.Units
{
    /// <summary>
    /// 单位职业枚举（MVP 测试用）。
    /// </summary>
    public enum BFUnitRole
    {
        /// <summary>战士：近战，攻击消耗 2 点 AP，攻击范围 1。</summary>
        Warrior,
        /// <summary>法师：远程，攻击消耗 3 点 AP，攻击范围 2。</summary>
        Mage
    }
}
