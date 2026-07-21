namespace BF.Game.Runtime.Input
{
    /// <summary>
    /// Map Group 强类型标识，表达一组需要共同启停的 Action Map。
    /// 用于 BFInputConfig 配置和运行时 BFInputManager.EnableGroup/DisableGroup。
    /// </summary>
    public enum BFInputMapGroupId
    {
        BattleGameplay = 0,
        HudUi = 10,
        ModalUi = 20
    }
}
