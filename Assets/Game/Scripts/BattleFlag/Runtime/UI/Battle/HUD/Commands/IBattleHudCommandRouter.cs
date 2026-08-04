namespace BF.Game.Runtime.UI.Battle.HUD.Commands
{
    /// <summary>
    /// BattleHUD 命令执行入口。
    /// SelectedUnitBar 将 Slot 点击产生的 CommandId 交给 Router，由 Router 决定进入移动、攻击子界面或其它业务。
    /// </summary>
    public interface IBattleHudCommandRouter
    {
        void Execute(string commandId);
    }
}
