namespace BF.Game.Runtime.Battle.Units
{
    /// <summary>
    /// 单位阵营枚举，用于选择控制、敌我判断和胜负判定。
    /// </summary>
    public enum UnitFaction
    {
        /// <summary>无阵营（默认值）。</summary>
        None,
        /// <summary>玩家阵营（单位面朝右）。</summary>
        Player,
        /// <summary>敌方阵营（单位面朝左）。</summary>
        Enemy
    }
}
