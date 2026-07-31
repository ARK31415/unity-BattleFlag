namespace BF.Game.Runtime.UI.Battle.HUD.Commands
{
    /// <summary>
    /// BattleHUD 命令的粗粒度类型，只用于 UI 状态分流，不承载具体战斗规则。
    /// </summary>
    public enum BattleHudCommandKind
    {
        Move,
        Attack,
        Wait,
        UnitDetails
    }
}
