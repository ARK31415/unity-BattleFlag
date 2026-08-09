using System.Collections.Generic;
using BF.Game.Runtime.Battle.Managers;
using BF.Game.Runtime.Battle.Query;

namespace BF.Game.Runtime.UI.Battle.HUD.Commands
{
    /// <summary>
    /// 根据当前选中单位和战斗状态生成 HUD 一级命令列表。
    /// 该接口只负责“有哪些命令可展示”，不执行移动、攻击或等待。
    /// </summary>
    public interface IBattleHudCommandProvider
    {
        IReadOnlyList<BattleHudCommandViewModel> GetCommands(
            BFUnitViewSnapshot unit,
            bool hasUnit,
            BFBattleTurnManager turnManager);
    }
}
