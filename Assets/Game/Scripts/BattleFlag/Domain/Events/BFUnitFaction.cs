namespace BF.Game.Battle.Domain.Events
{
    /// <summary>
    /// 战斗规则层使用的单位阵营标识。
    /// </summary>
    public enum BFUnitFaction
    {
        /// <summary>未指定阵营。</summary>
        None,

        /// <summary>玩家阵营。</summary>
        Player,

        /// <summary>敌方阵营。</summary>
        Enemy
    }
}
