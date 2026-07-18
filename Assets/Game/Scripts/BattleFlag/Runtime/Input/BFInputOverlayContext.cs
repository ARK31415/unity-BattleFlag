namespace BF.Game.Runtime.Input
{
    /// <summary>
    /// 输入系统的叠加上下文，可在基础上下文之上组合启用。
    /// </summary>
    public enum BFInputOverlayContext
    {
        Global = 0,
        BattleCamera = 1,
        UI = 2,
        Debug = 3
    }
}
