namespace BF.Game.Runtime.Input
{
    /// <summary>
    /// 项目输入动作的稳定标识，避免业务脚本散落 Action Map 和 Action 名称字符串。
    /// </summary>
    public enum BFInputActionId
    {
        BattlePoint = 0,
        BattleSelect = 1,
        BattleCancel = 2,
        BattleEndTurn = 3,
        BattleCameraMove = 4,
        BattleCameraZoom = 5,
        GlobalPause = 6
    }
}
