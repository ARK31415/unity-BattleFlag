using System;
using System.Collections.Generic;
using BF.Game.Runtime.UI.Battle.HUD.Commands;
using UnityEngine;
using UnityEngine.UI;

namespace BF.Game.Runtime.UI.Battle.HUD.CommandSubmenu
{
    /// <summary>
    /// 攻击/技能下级操作台。
    /// 它管理行动选择和目标选择两个阶段，并统一持有返回/确定全局控制按钮。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CommandSubmenuView : MonoBehaviour
    {
        public enum Stage
        {
            Hidden,
            ActionSelect,
            TargetSelect
        }

        [SerializeField] private CanvasGroup _rootCanvasGroup;
        [SerializeField] private CommandDetailPanelView _detailPanel;
        [SerializeField] private CommandActionListPanelView _actionListPanel;
        [SerializeField] private Button _backButton;
        [SerializeField] private Button _confirmButton;

        private Stage _stage = Stage.Hidden;

        public event Action BackRequested;
        public event Action ConfirmRequested;
        public event Action<string> ActionSelected;

        public Stage CurrentStage => _stage;

        private void Awake()
        {
            if (_backButton != null) _backButton.onClick.AddListener(HandleBack);
            if (_confirmButton != null) _confirmButton.onClick.AddListener(HandleConfirm);
            Hide();
        }

        private void OnDestroy()
        {
            if (_backButton != null) _backButton.onClick.RemoveListener(HandleBack);
            if (_confirmButton != null) _confirmButton.onClick.RemoveListener(HandleConfirm);
        }

        public void ShowActionSelect(IReadOnlyList<BattleHudActionViewModel> actions)
        {
            _stage = Stage.ActionSelect;
            SetVisible(true);
            if (_actionListPanel != null)
            {
                _actionListPanel.SetVisible(true);
                _actionListPanel.Bind(actions, HandleActionSelected);
            }

            SetConfirmInteractable(_actionListPanel != null && _actionListPanel.TryGetSelectedAction(out _));
        }

        public void ShowTargetSelect()
        {
            _stage = Stage.TargetSelect;
            SetVisible(true);
            if (_actionListPanel != null)
                _actionListPanel.SetVisible(false);
            SetConfirmInteractable(false);
        }

        public void Hide()
        {
            _stage = Stage.Hidden;
            SetVisible(false);
            _detailPanel?.Clear();
        }

        public bool TryGetSelectedAction(out BattleHudActionViewModel action)
        {
            if (_actionListPanel != null)
                return _actionListPanel.TryGetSelectedAction(out action);

            action = default;
            return false;
        }

        public void SetConfirmInteractable(bool interactable)
        {
            if (_confirmButton != null)
                _confirmButton.interactable = interactable;
        }

        private void HandleActionSelected(string actionId)
        {
            if (_actionListPanel != null && _actionListPanel.TryGetAction(actionId, out var action))
                _detailPanel?.Show(action);

            ActionSelected?.Invoke(actionId);
            SetConfirmInteractable(true);
        }

        private void HandleBack()
        {
            BackRequested?.Invoke();
        }

        private void HandleConfirm()
        {
            ConfirmRequested?.Invoke();
        }

        private void SetVisible(bool visible)
        {
            if (_rootCanvasGroup == null)
            {
                gameObject.SetActive(visible);
                return;
            }

            _rootCanvasGroup.alpha = visible ? 1f : 0f;
            _rootCanvasGroup.interactable = visible;
            _rootCanvasGroup.blocksRaycasts = visible;
            gameObject.SetActive(visible);
        }
    }
}
