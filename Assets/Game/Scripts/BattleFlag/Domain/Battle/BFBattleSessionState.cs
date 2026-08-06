namespace BF.Game.Battle.Domain
{
    /// <summary>
    /// 一次战斗 Session 的生命周期状态。
    /// </summary>
    public enum BFBattleSessionState
    {
        /// <summary>Session 已创建但尚未运行。</summary>
        Created,

        /// <summary>Session 正在运行，可以发布战斗事实。</summary>
        Running,

        /// <summary>战斗已完成，只允许读取结果和清理订阅。</summary>
        Completed,

        /// <summary>Session 已释放，不再接受任何操作。</summary>
        Disposed
    }
}
