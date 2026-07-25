using BF.Game.Runtime.Battle.Units;
using UnityEngine;
using UnityEngine.UI;
using Wit.Framework.UI;

namespace BF.Game.Runtime.UI.Battle
{
    /// <summary>
    /// 战斗 HUD 内部的单位信息 Widget，只负责显示单位身份和数值快照。
    /// </summary>
    public sealed class UnitInfoWidget : WitUIWidget<UnitInfoWidgetData>
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Text _unitNameText;
        [SerializeField] private Image _unitHPFill;
        [SerializeField] private Text _unitHPText;
        [SerializeField] private Text _unitATKText;
        [SerializeField] private Text _unitAPText;

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

            if (_unitNameText != null) _unitNameText.text = data.DisplayName;
            if (_unitHPText != null) _unitHPText.text = $"HP: {data.CurrentHP}/{data.MaxHP}";
            if (_unitATKText != null) _unitATKText.text = $"ATK: {data.Attack}";
            if (_unitAPText != null) _unitAPText.text = $"AP: {data.RemainingActionPoints}/{data.MaxActionPoints}";

            if (_unitHPFill == null) return;

            _unitHPFill.fillAmount = data.HPRatio;
            _unitHPFill.color = data.HPRatio > 0.5f ? Color.green :
                data.HPRatio > 0.25f ? new Color(1f, 0.8f, 0f) : Color.red;
        }
    }

    /// <summary>
    /// 单位信息 Widget 的只读显示数据。
    /// </summary>
    public readonly struct UnitInfoWidgetData
    {
        public UnitInfoWidgetData(
            string displayName,
            int currentHP,
            int maxHP,
            int attack,
            int remainingActionPoints,
            int maxActionPoints)
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Unit" : displayName;
            CurrentHP = currentHP;
            MaxHP = maxHP;
            Attack = attack;
            RemainingActionPoints = remainingActionPoints;
            MaxActionPoints = maxActionPoints;
        }

        public string DisplayName { get; }
        public int CurrentHP { get; }
        public int MaxHP { get; }
        public int Attack { get; }
        public int RemainingActionPoints { get; }
        public int MaxActionPoints { get; }
        public float HPRatio => MaxHP > 0 ? (float)CurrentHP / MaxHP : 0f;

        public static UnitInfoWidgetData FromUnit(UnitRuntime unit)
        {
            var identity = unit.Identity;
            var stats = unit.Stats;
            return new UnitInfoWidgetData(
                identity.DisplayName,
                stats.CurrentHP,
                stats.MaxHP,
                stats.Attack,
                stats.RemainingActionPoints,
                stats.MaxActionPoints);
        }
    }
}
