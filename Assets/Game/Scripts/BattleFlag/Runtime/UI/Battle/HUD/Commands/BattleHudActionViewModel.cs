using UnityEngine;

namespace BF.Game.Runtime.UI.Battle.HUD.Commands
{
    /// <summary>
    /// 攻击/技能子界面右侧行动列表的显示数据。
    /// 第一版包含普通攻击与技能占位，后续可由真实技能系统替换数据来源。
    /// </summary>
    public readonly struct BattleHudActionViewModel
    {
        public BattleHudActionViewModel(
            string actionId,
            string displayName,
            string description,
            string effect,
            bool isEnabled = true,
            string disabledReason = null,
            Sprite icon = null)
        {
            ActionId = actionId;
            DisplayName = displayName;
            Description = description;
            Effect = effect;
            IsEnabled = isEnabled;
            DisabledReason = disabledReason;
            Icon = icon;
        }

        public string ActionId { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string Effect { get; }
        public bool IsEnabled { get; }
        public string DisabledReason { get; }
        public Sprite Icon { get; }
    }
}
