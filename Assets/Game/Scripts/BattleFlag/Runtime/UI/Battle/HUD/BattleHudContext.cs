using BF.Game.Runtime.Battle.Events;
using BF.Game.Runtime.Battle.Flow;
using BF.Game.Runtime.Battle.Input;
using BF.Game.Runtime.Battle.Managers;
using BF.Game.Runtime.Battle.PlayerInput;
using BF.Game.Runtime.Battle.Query;
using BF.Game.Runtime.UI.Battle.HUD.Camera;
using Wit.Framework.UI;

namespace BF.Game.Runtime.UI.Battle
{
    /// <summary>
    /// 战斗 HUD 打开参数。只承载外部依赖，不保存 HUD 内部运行时显示状态。
    /// </summary>
    public sealed class BattleHudContext
    {
        public BFTurnEventSO TurnEventChannel { get; set; }
        public BFBattleEventSO BattleEventChannel { get; set; }
        public BFUnitEventSO UnitEventChannel { get; set; }
        public BFBattleTurnManager TurnManager { get; set; }
        public BFBattleActionCoordinator ActionCoordinator { get; set; }
        public IBFBattleActionGateway ActionGateway { get; set; }
        public BFBattleSelectionController SelectionController { get; set; }
        public BFBattleSelectionCoordinator SelectionCoordinator { get; set; }
        public IBFBattleUnitQuery UnitQuery { get; set; }
        public BFBattleInputController InputController { get; set; }
        public IBattleHudCameraFocusLock CameraFocusLock { get; set; }
        public WitUIManager UIManager { get; set; }
        public string ResultPopupKey { get; set; } = "battle.result";
    }
}
