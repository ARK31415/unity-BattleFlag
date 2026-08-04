using System;
using System.Collections.Generic;
using BF.Game.Runtime.UI.Battle.HUD.Commands;
using UnityEngine;
using UnityEngine.UI;

namespace BF.Game.Runtime.UI.Battle.HUD.SelectedUnitBar
{
    /// <summary>
    /// 选中我方可操作单位后的底部命令栏。
    /// 它只负责根据 Provider 返回的数据生成 Slot，并把点击产生的 CommandId 交给上层 Router。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SelectedUnitBarView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _rootCanvasGroup;
        [SerializeField] private RectTransform _slotContent;
        [SerializeField] private SelectedUnitCommandSlotView _slotPrefab;
        [SerializeField] private Button _pageLeftButton;
        [SerializeField] private Button _pageRightButton;
        [SerializeField] private int _visibleSlotCount = 6;

        private readonly List<SelectedUnitCommandSlotView> _slots = new();
        private readonly List<BattleHudCommandViewModel> _commands = new();
        private Action<string> _commandClicked;
        private int _pageIndex;
        private string _selectedCommandId;

        private void Awake()
        {
            ResolveReferences();
            if (_pageLeftButton != null) _pageLeftButton.onClick.AddListener(PreviousPage);
            if (_pageRightButton != null) _pageRightButton.onClick.AddListener(NextPage);
            Hide();
        }

        private void OnDestroy()
        {
            if (_pageLeftButton != null) _pageLeftButton.onClick.RemoveListener(PreviousPage);
            if (_pageRightButton != null) _pageRightButton.onClick.RemoveListener(NextPage);
        }

        public void Show(IReadOnlyList<BattleHudCommandViewModel> commands, Action<string> commandClicked)
        {
            ResolveReferences();
            _commands.Clear();
            if (commands != null)
                _commands.AddRange(commands);

            _commandClicked = commandClicked;
            _pageIndex = Mathf.Clamp(_pageIndex, 0, Mathf.Max(0, PageCount - 1));
            SetVisible(true);
            RefreshPage();
        }

        public void Hide()
        {
            SetVisible(false);
            _selectedCommandId = null;
        }

        public void SetSelectedCommand(string commandId)
        {
            _selectedCommandId = commandId;
            foreach (var slot in _slots)
                slot.SetSelected(false);

            int pageStart = _pageIndex * VisibleSlotCount;
            for (int i = 0; i < _slots.Count; i++)
            {
                int commandIndex = pageStart + i;
                if (commandIndex >= _commands.Count) continue;
                _slots[i].SetSelected(_commands[commandIndex].CommandId == _selectedCommandId);
            }
        }

        private void RefreshPage()
        {
            int visibleCount = Mathf.Min(VisibleSlotCount, _commands.Count - _pageIndex * VisibleSlotCount);
            EnsureSlotCount(Mathf.Max(0, visibleCount));

            int pageStart = _pageIndex * VisibleSlotCount;
            for (int i = 0; i < _slots.Count; i++)
            {
                int commandIndex = pageStart + i;
                bool hasCommand = commandIndex >= 0 && commandIndex < _commands.Count;
                _slots[i].gameObject.SetActive(hasCommand);
                if (!hasCommand) continue;

                _slots[i].Bind(_commands[commandIndex], HandleCommandClicked);
                _slots[i].SetSelected(_commands[commandIndex].CommandId == _selectedCommandId);
            }

            bool hasMultiplePages = PageCount > 1;
            if (_pageLeftButton != null) _pageLeftButton.gameObject.SetActive(hasMultiplePages);
            if (_pageRightButton != null) _pageRightButton.gameObject.SetActive(hasMultiplePages);
            if (_pageLeftButton != null) _pageLeftButton.interactable = _pageIndex > 0;
            if (_pageRightButton != null) _pageRightButton.interactable = _pageIndex < PageCount - 1;
        }

        private void EnsureSlotCount(int count)
        {
            if (_slotPrefab == null || _slotContent == null) return;

            while (_slots.Count < count)
            {
                var slot = Instantiate(_slotPrefab, _slotContent);
                _slots.Add(slot);
            }
        }

        private void HandleCommandClicked(string commandId)
        {
            _selectedCommandId = commandId;
            SetSelectedCommand(commandId);
            _commandClicked?.Invoke(commandId);
        }

        private void PreviousPage()
        {
            _pageIndex = Mathf.Max(0, _pageIndex - 1);
            RefreshPage();
        }

        private void NextPage()
        {
            _pageIndex = Mathf.Min(PageCount - 1, _pageIndex + 1);
            RefreshPage();
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

        private void ResolveReferences()
        {
            if (_rootCanvasGroup == null) _rootCanvasGroup = GetComponent<CanvasGroup>();
            if (_slotContent == null) _slotContent = transform as RectTransform;
        }

        private int VisibleSlotCount => Mathf.Max(1, _visibleSlotCount);
        private int PageCount => Mathf.Max(1, Mathf.CeilToInt(_commands.Count / (float)VisibleSlotCount));
    }
}
