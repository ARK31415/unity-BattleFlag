namespace BF.Game.Runtime.Battle
{
    /// <summary>
    /// 一次战斗会话的生命周期状态。
    /// </summary>
    public enum BFBattleSessionState
    {
        /// <summary>会话已创建，但尚未开始运行。</summary>
        Created,

        /// <summary>会话正在运行，可以发布战斗领域事件。</summary>
        Running,

        /// <summary>战斗已经完成，可以读取结果，但不能新增订阅或发布事件。</summary>
        Completed,

        /// <summary>会话已经释放，不再允许访问上下文或事件总线。</summary>
        Disposed
    }
}
