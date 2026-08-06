namespace BF.Game.Battle.Domain.Units
{
    /// <summary>
    /// 单位的规则战斗角色。
    ///
    /// Role 表示战斗定位，不表示单位的普通、精英或 Boss 层级；层级由 BFUnitTier 表达。
    /// </summary>
    public enum BFUnitRole
    {
        /// <summary>近战或基础战士定位。</summary>
        Warrior,

        /// <summary>远程或法术定位。</summary>
        Mage
    }
}
