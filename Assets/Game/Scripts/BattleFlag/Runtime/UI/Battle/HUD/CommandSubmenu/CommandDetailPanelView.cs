using BF.Game.Runtime.UI.Battle.HUD.Commands;
using UnityEngine;
using UnityEngine.UI;

namespace BF.Game.Runtime.UI.Battle.HUD.CommandSubmenu
{
    /// <summary>
    /// 攻击/技能子界面左侧详情面板。
    /// 第一版只显示名称、描述和效果，消耗、范围、冷却等信息写入效果正文。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CommandDetailPanelView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Text _nameText;
        [SerializeField] private Text _descriptionText;
        [SerializeField] private Text _effectText;

        public void Show(BattleHudActionViewModel action)
        {
            if (_icon != null)
            {
                _icon.sprite = action.Icon;
                _icon.enabled = action.Icon != null;
            }

            if (_nameText != null)
                _nameText.text = action.DisplayName;
            if (_descriptionText != null)
                _descriptionText.text = action.Description;
            if (_effectText != null)
                _effectText.text = action.Effect;
        }

        public void Clear()
        {
            if (_icon != null)
                _icon.enabled = false;
            if (_nameText != null)
                _nameText.text = string.Empty;
            if (_descriptionText != null)
                _descriptionText.text = string.Empty;
            if (_effectText != null)
                _effectText.text = string.Empty;
        }
    }
}
