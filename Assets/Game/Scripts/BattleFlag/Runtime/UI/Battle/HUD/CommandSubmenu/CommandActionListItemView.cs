using System;
using BF.Game.Runtime.UI.Battle.HUD.Commands;
using UnityEngine;
using UnityEngine.UI;

namespace BF.Game.Runtime.UI.Battle.HUD.CommandSubmenu
{
    /// <summary>
    /// 攻击/技能右侧列表的单条行动项。
    /// 它是图标加名称的长条按钮，只回传 ActionId，不执行攻击或技能。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CommandActionListItemView : MonoBehaviour
    {
        [SerializeField] private Button _rootButton;
        [SerializeField] private Image _icon;
        [SerializeField] private Text _nameText;
        [SerializeField] private GameObject _disabledOverlay;
        [SerializeField] private GameObject _selectionFrame;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Color _enabledTextColor = Color.white;
        [SerializeField] private Color _disabledTextColor = new(0.55f, 0.55f, 0.55f, 1f);

        private string _actionId;
        private Action<string> _clicked;

        private void Awake()
        {
            ResolveReferences();
            if (_rootButton != null) _rootButton.onClick.AddListener(HandleClicked);
        }

        private void OnDestroy()
        {
            if (_rootButton != null) _rootButton.onClick.RemoveListener(HandleClicked);
        }

        public void Bind(BattleHudActionViewModel action, Action<string> clicked)
        {
            ResolveReferences();
            _actionId = action.ActionId;
            _clicked = clicked;

            if (_nameText != null)
            {
                _nameText.text = action.DisplayName;
                _nameText.color = action.IsEnabled ? _enabledTextColor : _disabledTextColor;
            }

            if (_icon != null)
            {
                _icon.sprite = action.Icon;
                _icon.enabled = action.Icon != null;
            }

            if (_rootButton != null)
                _rootButton.interactable = action.IsEnabled;

            if (_disabledOverlay != null)
                _disabledOverlay.SetActive(!action.IsEnabled);

            if (_canvasGroup != null)
                _canvasGroup.alpha = action.IsEnabled ? 1f : 0.55f;

            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            if (_selectionFrame != null)
                _selectionFrame.SetActive(selected);
        }

        private void HandleClicked()
        {
            if (string.IsNullOrEmpty(_actionId)) return;
            _clicked?.Invoke(_actionId);
        }

        private void ResolveReferences()
        {
            if (_rootButton == null) _rootButton = GetComponent<Button>();
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
        }
    }
}
