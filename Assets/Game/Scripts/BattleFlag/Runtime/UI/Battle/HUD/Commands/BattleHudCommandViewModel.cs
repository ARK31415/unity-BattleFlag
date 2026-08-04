using UnityEngine;

namespace BF.Game.Runtime.UI.Battle.HUD.Commands
{
    /// <summary>
    /// BattleHUD 命令槽位的显示数据。
    /// Slot 只读取该数据刷新图标、文字和可用状态，具体业务由 Router 处理。
    /// </summary>
    public readonly struct BattleHudCommandViewModel
    {
        public BattleHudCommandViewModel(
            string commandId,
            string displayName,
            BattleHudCommandKind kind,
            bool isEnabled,
            string disabledReason = null,
            Sprite icon = null)
        {
            CommandId = commandId;
            DisplayName = displayName;
            Kind = kind;
            IsEnabled = isEnabled;
            DisabledReason = disabledReason;
            Icon = icon;
        }

        public string CommandId { get; }
        public string DisplayName { get; }
        public BattleHudCommandKind Kind { get; }
        public bool IsEnabled { get; }
        public string DisabledReason { get; }
        public Sprite Icon { get; }
    }
}
