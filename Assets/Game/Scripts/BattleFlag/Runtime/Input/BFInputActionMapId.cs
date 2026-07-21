namespace BF.Game.Runtime.Input
{
    /// <summary>
    /// Action Map 强类型标识，对应 BFInputActions 中定义的四个 Action Map。
    /// 用于 BFInputConfig 的 Map Group 配置，避免 Inspector 中使用自由字符串。
    /// </summary>
    public enum BFInputActionMapId
    {
        Battle = 0,
        BattleCamera = 10,
        Global = 20,
        UI = 30
    }
}
