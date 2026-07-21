namespace BF.Game.Runtime.Input
{
    /// <summary>
    /// Profile 强类型标识，表达一个完整的输入启用状态快照。
    /// 用于 BFInputConfig 配置和运行时 BFInputManager.ApplyProfile。
    /// </summary>
    public enum BFInputProfileId
    {
        BattleHud = 0,
        ModalUi = 10
    }
}
