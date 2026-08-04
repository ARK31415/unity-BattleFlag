namespace BF.Game.Battle.Domain.Events
{
    /// <summary>
    /// 表示战斗规则状态已经初始化完成并进入运行阶段。
    ///
    /// 规则层应在上下文初始化完成后发布该事实，监听者不应将其当作启动命令再次执行初始化。
    /// </summary>
    public readonly struct BFBattleStartedEvent
    {
        /// <summary>
        /// 创建战斗开始事件。
        /// </summary>
        /// <param name="battleId">本场战斗的唯一标识。</param>
        public BFBattleStartedEvent(string battleId)
        {
            BattleId = battleId ?? string.Empty;
        }

        /// <summary>
        /// 本场战斗的唯一标识。
        /// </summary>
        public string BattleId { get; }
    }
}
