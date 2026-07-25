using BF.Game.Runtime.Battle.Units;
using UnityEngine;
using UnityEngine.UI;
using Wit.Framework.UI;

namespace BF.Game.Runtime.UI.Battle
{
    /// <summary>
    /// 战斗 HUD 内部的单位信息 Widget，只负责显示单位身份和数值快照。
    ///
    /// 排版结构：
    /// - 顶部：Icon 占位符 + 单位名称（带阵营色：己方蓝 / 敌方红）。
    /// - 中部：HP 和 AP 用 Bar 进度条控件，视觉上归为一组。
    /// - 底部：ATK 等纯数值属性独立一组，不使用 Bar 控件。
    ///
    /// 不负责：
    /// - 不自行查找 BFBattleUnitManager 或其他全局 Manager。
    /// - 不修改单位状态，只做纯展示。
    /// - 状态效果图标本次不实现，后续 Spec 再加。
    /// </summary>
    public sealed class UnitInfoWidget : WitUIWidget<UnitInfoWidgetData>
    {
        [Header("Panel")]
        // Widget 整体容器，SetVisible 控制此 GameObject 的激活状态。
        [SerializeField] private GameObject _panel;

        [Header("Header")]
        // 单位名称左侧的 Icon 占位符，后续替换为实际单位肖像。
        [SerializeField] private Image _unitIcon;
        // 单位名称文字，根据阵营设置颜色。
        [SerializeField] private Text _unitNameText;

        [Header("Bar Group (HP/AP)")]
        // HP 进度条填充 Image，fillAmount 由 HPRatio 驱动。
        [SerializeField] private Image _hpBarFill;
        [SerializeField] private Text _hpText;
        // AP 进度条填充 Image，fillAmount 由 APRatio 驱动。
        [SerializeField] private Image _apBarFill;
        [SerializeField] private Text _apText;

        [Header("Value Group")]
        [SerializeField] private Text _unitATKText;

        [Header("Colors")]
        // 己方单位名称颜色，默认蓝色。
        [SerializeField] private Color _playerNameColor = new Color(0.3f, 0.6f, 1f, 1f);
        // 敌方单位名称颜色，默认红色。
        [SerializeField] private Color _enemyNameColor = new Color(1f, 0.3f, 0.3f, 1f);

        public override void SetVisible(bool visible)
        {
            if (_panel != null)
                _panel.SetActive(visible);
            else
                base.SetVisible(visible);
        }

        protected override void OnDataChanged(UnitInfoWidgetData data)
        {
            SetVisible(true);

            // Icon 占位（当前无肖像素材，保留引用位）。
            if (_unitIcon != null)
                _unitIcon.gameObject.SetActive(false);

            // 单位名称 + 阵营色。
            if (_unitNameText != null)
            {
                _unitNameText.text = data.DisplayName;
                _unitNameText.color = data.IsPlayer ? _playerNameColor : _enemyNameColor;
            }

            // HP 进度条 + 文字。
            if (_hpText != null)
                _hpText.text = $"HP: {data.CurrentHP}/{data.MaxHP}";

            if (_hpBarFill != null)
            {
                _hpBarFill.fillAmount = data.HPRatio;
                _hpBarFill.color = data.HPRatio > 0.5f ? Color.green :
                    data.HPRatio > 0.25f ? new Color(1f, 0.8f, 0f) : Color.red;
            }

            // AP 进度条 + 文字。
            if (_apText != null)
                _apText.text = $"AP: {data.RemainingActionPoints}/{data.MaxActionPoints}";

            if (_apBarFill != null)
                _apBarFill.fillAmount = data.APRatio;

            // ATK 纯数值，独立一组。
            if (_unitATKText != null)
                _unitATKText.text = $"ATK: {data.Attack}";
        }
    }

    /// <summary>
    /// 单位信息 Widget 的只读显示数据。
    /// 承载名称、数值、进度比和阵营标识，不持有运行时对象引用。
    /// </summary>
    public readonly struct UnitInfoWidgetData
    {
        public UnitInfoWidgetData(
            string displayName,
            int currentHP,
            int maxHP,
            int attack,
            int remainingActionPoints,
            int maxActionPoints,
            bool isPlayer)
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Unit" : displayName;
            CurrentHP = currentHP;
            MaxHP = maxHP;
            Attack = attack;
            RemainingActionPoints = remainingActionPoints;
            MaxActionPoints = maxActionPoints;
            IsPlayer = isPlayer;
        }

        public string DisplayName { get; }
        public int CurrentHP { get; }
        public int MaxHP { get; }
        public int Attack { get; }
        public int RemainingActionPoints { get; }
        public int MaxActionPoints { get; }
        public bool IsPlayer { get; }
        public float HPRatio => MaxHP > 0 ? (float)CurrentHP / MaxHP : 0f;
        public float APRatio => MaxActionPoints > 0 ? (float)RemainingActionPoints / MaxActionPoints : 0f;

        public static UnitInfoWidgetData FromUnit(UnitRuntime unit)
        {
            var identity = unit.Identity;
            var stats = unit.Stats;
            var faction = unit.Identity.Faction;
            return new UnitInfoWidgetData(
                identity.DisplayName,
                stats.CurrentHP,
                stats.MaxHP,
                stats.Attack,
                stats.RemainingActionPoints,
                stats.MaxActionPoints,
                faction == UnitFaction.Player);
        }
    }
}
