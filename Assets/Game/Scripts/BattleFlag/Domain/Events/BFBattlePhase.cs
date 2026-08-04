namespace BF.Game.Battle.Domain.Events
{
    /// <summary>
    /// 战斗规则层使用的阶段标识。
    /// </summary>
    public enum BFBattlePhase
    {
        /// <summary>未进入有效阶段。</summary>
        None,

        /// <summary>战斗初始化阶段。</summary>
        Init,

        /// <summary>玩家行动阶段。</summary>
        PlayerTurn,

        /// <summary>敌方行动阶段。</summary>
        EnemyTurn,

        /// <summary>行动结果结算阶段。</summary>
        Resolution
    }
}
