using System;
using System.Collections.Generic;
using BF.Game.Runtime.UI.Battle.HUD.Commands;
using UnityEngine;
using UnityEngine.UI;

namespace BF.Game.Runtime.UI.Battle.HUD.CommandSubmenu
{
    /// <summary>
    /// 攻击/技能子界面右侧行动列表。
    /// 列表使用数据驱动生成长条行动项，当前选中项负责驱动左侧详情刷新。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CommandActionListPanelView : MonoBehaviour
    {
        [SerializeField] private Text _headerText;
        [SerializeField] private RectTransform _content;
        [SerializeField] private CommandActionListItemView _itemPrefab;

        private readonly List<CommandActionListItemView> _items = new();
        private readonly List<BattleHudActionViewModel> _actions = new();
        private Action<string> _actionSelected;
        private string _selectedActionId;

        public void Bind(IReadOnlyList<BattleHudActionViewModel> actions, Action<string> actionSelected)
        {
            _actions.Clear();
            if (actions != null)
                _actions.AddRange(actions);

            _actionSelected = actionSelected;
            if (_headerText != null)
                _headerText.text = "行动选择";

            EnsureItemCount(_actions.Count);
            for (int i = 0; i < _items.Count; i++)
            {
                bool hasAction = i < _actions.Count;
                _items[i].gameObject.SetActive(hasAction);
                if (!hasAction) continue;
                _items[i].Bind(_actions[i], HandleActionClicked);
            }

            SelectFirstEnabled();
        }

        public bool TryGetAction(string actionId, out BattleHudActionViewModel action)
        {
            foreach (var candidate in _actions)
            {
                if (candidate.ActionId != actionId) continue;
                action = candidate;
                return true;
            }

            action = default;
            return false;
        }

        public bool TryGetSelectedAction(out BattleHudActionViewModel action)
        {
            return TryGetAction(_selectedActionId, out action);
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        private void SelectFirstEnabled()
        {
            foreach (var action in _actions)
            {
                if (!action.IsEnabled) continue;
                Select(action.ActionId, notify: true);
                return;
            }

            Select(null, notify: false);
        }

        private void HandleActionClicked(string actionId)
        {
            Select(actionId, notify: true);
        }

        private void Select(string actionId, bool notify)
        {
            _selectedActionId = actionId;
            for (int i = 0; i < _items.Count; i++)
            {
                bool selected = i < _actions.Count && _actions[i].ActionId == _selectedActionId;
                _items[i].SetSelected(selected);
            }

            if (notify && !string.IsNullOrEmpty(_selectedActionId))
                _actionSelected?.Invoke(_selectedActionId);
        }

        private void EnsureItemCount(int count)
        {
            if (_itemPrefab == null || _content == null) return;

            while (_items.Count < count)
            {
                var item = Instantiate(_itemPrefab, _content);
                _items.Add(item);
            }
        }
    }
}
