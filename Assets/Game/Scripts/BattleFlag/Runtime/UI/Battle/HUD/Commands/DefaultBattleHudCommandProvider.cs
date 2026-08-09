using System.Collections.Generic;
using BF.Game.Battle.Domain.Events;
using BF.Game.Runtime.Battle.Managers;
using BF.Game.Runtime.Battle.Query;

namespace BF.Game.Runtime.UI.Battle.HUD.Commands
{
    /// <summary>
    /// BattleHUD 第一版固定命令提供器。
    /// 它按 Spec 返回移动、攻击、等待、角色详情四个命令，同时保留列表驱动结构，避免底栏写死按钮字段。
    /// </summary>
    public sealed class DefaultBattleHudCommandProvider : IBattleHudCommandProvider
    {
        public const string MoveCommandId = "move";
        public const string AttackCommandId = "attack";
        public const string WaitCommandId = "wait";
        public const string UnitDetailsCommandId = "unit_details";

        private static readonly BattleHudCommandViewModel[] EmptyCommands = { };

        public IReadOnlyList<BattleHudCommandViewModel> GetCommands(
            BFUnitViewSnapshot unit,
            bool hasUnit,
            BFBattleTurnManager turnManager)
        {
            if (!hasUnit)
                return EmptyCommands;

            bool isPlayerTurn = turnManager == null || turnManager.CurrentPhase == BattlePhase.PlayerTurn;
            bool canAct = isPlayerTurn &&
                          unit.Faction == BFUnitFaction.Player &&
                          unit.IsAlive &&
                          !unit.HasActed;

            return new[]
            {
                new BattleHudCommandViewModel(MoveCommandId, "移动", BattleHudCommandKind.Move, canAct, "该单位当前无法移动"),
                new BattleHudCommandViewModel(AttackCommandId, "攻击", BattleHudCommandKind.Attack, canAct, "该单位当前无法攻击"),
                new BattleHudCommandViewModel(WaitCommandId, "等待", BattleHudCommandKind.Wait, canAct, "该单位当前无法等待"),
                new BattleHudCommandViewModel(UnitDetailsCommandId, "角色详情", BattleHudCommandKind.UnitDetails, true)
            };
        }
    }
}
