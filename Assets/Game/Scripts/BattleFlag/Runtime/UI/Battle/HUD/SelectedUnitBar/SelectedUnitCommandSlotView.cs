using System;
using BF.Game.Runtime.UI.Battle.HUD.Commands;
using UnityEngine;
using UnityEngine.UI;

namespace BF.Game.Runtime.UI.Battle.HUD.SelectedUnitBar
{
    /// <summary>
    /// 底部单位命令栏的单个槽位视图。
    /// 槽位只显示命令数据并回传 CommandId，不知道移动、攻击、等待等具体业务。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SelectedUnitCommandSlotView : MonoBehaviour
    {
        [SerializeField] private Button _rootButton;
        [SerializeField] private Image _icon;
        [SerializeField] private Text _labelText;
        [SerializeField] private GameObject _disabledOverlay;
        [SerializeField] private GameObject _selectionFrame;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Color _enabledTextColor = Color.white;
        [SerializeField] private Color _disabledTextColor = new(0.55f, 0.55f, 0.55f, 1f);

        private string _commandId;
        private Action<string> _clicked;

        private void Awake()
        {
            ResolveReferences();
            if (_rootButton != null)
                _rootButton.onClick.AddListener(HandleClicked);
        }

        private void OnDestroy()
        {
            if (_rootButton != null)
                _rootButton.onClick.RemoveListener(HandleClicked);
        }

        public void Bind(BattleHudCommandViewModel command, Action<string> clicked)
        {
            ResolveReferences();
            _commandId = command.CommandId;
            _clicked = clicked;

            if (_labelText != null)
            {
                _labelText.text = command.DisplayName;
                _labelText.color = command.IsEnabled ? _enabledTextColor : _disabledTextColor;
            }

            if (_icon != null)
            {
                _icon.sprite = command.Icon;
                _icon.enabled = command.Icon != null;
            }

            if (_rootButton != null)
                _rootButton.interactable = command.IsEnabled;

            if (_disabledOverlay != null)
                _disabledOverlay.SetActive(!command.IsEnabled);

            if (_canvasGroup != null)
                _canvasGroup.alpha = command.IsEnabled ? 1f : 0.55f;

            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            if (_selectionFrame != null)
                _selectionFrame.SetActive(selected);
        }

        private void HandleClicked()
        {
            if (string.IsNullOrEmpty(_commandId)) return;
            _clicked?.Invoke(_commandId);
        }

        private void ResolveReferences()
        {
            if (_rootButton == null) _rootButton = GetComponent<Button>();
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
        }
    }
}
