using System.Collections.Generic;
using BF.Game.Runtime.Battle;
using BF.Game.Runtime.Battle.Events;
using BF.Game.Runtime.Battle.Managers;
using BF.Game.Runtime.Battle.PlayerInput;
using BF.Game.Runtime.Battle.Units;
using BF.Game.Runtime.UI.Battle.HUD.Camera;
using BF.Game.Runtime.UI.Battle.HUD.CommandSubmenu;
using BF.Game.Runtime.UI.Battle.HUD.Commands;
using BF.Game.Runtime.UI.Battle.HUD.Core;
using BF.Game.Runtime.UI.Battle.HUD.SelectedUnitBar;
using UnityEngine;
using Wit.Framework.UI;

namespace BF.Game.Runtime.UI.Battle
{
    /// <summary>
    /// BattleHUD 的总装配窗口。
    /// 它负责接收战斗上下文、分发给 HUD 子 Prefab、切换 HUD 状态，并把命令点击交给 Router。
    /// 它不直接显示角色详情、不在根节点写死业务按钮，也不直接控制具体相机实现。
    /// </summary>
    [DisallowMultipleComponent]
    public class BattleHudView : WitUIView<BattleHudContext>, IBattleHudCommandRouter
    {
        [Header("HUD Regions")]
        [SerializeField] private TurnBannerView _turnBanner;
        [SerializeField] private BattleModeHintView _battleModeHint;
        [SerializeField] private SelectedUnitBarView _selectedUnitBar;
        [SerializeField] private CommandSubmenuView _commandSubmenu;
        [SerializeField] private EndTurnControlView _endTurnControl;

        [Header("Event Channels")]
        [SerializeField] private BFTurnEventSO _turnEventChannel;
        [SerializeField] private BFBattleEventSO _battleEventChannel;
        [SerializeField] private BFUnitEventSO _unitEventChannel;

        [Header("Managers")]
        [SerializeField] private BFBattleTurnManager _turnManager;
        [SerializeField] private BFBattleUnitManager _unitManager;
        [SerializeField] private BFBattleInputController _inputController;

        private readonly IBattleHudCommandProvider _commandProvider = new DefaultBattleHudCommandProvider();
        private readonly List<BattleHudActionViewModel> _mockActions = new();
        private WitUIManager _uiManager;
        private IBattleHudCameraFocusLock _cameraFocusLock;
        private UnitRuntime _selectedTarget;
        private string _resultPopupKey = "battle.result";
        private bool _isSubscribed;
        private bool _noLegalActions;
        private int _currentTurnNumber;
        private BattlePhase _currentPhase;

        protected override void OnOpened(BattleHudContext context)
        {
            ApplyContext(context);
            SubscribeEvents();
            RefreshInitialState();
        }

        protected override void OnReopened(BattleHudContext context)
        {
            OnClosing();
            OnOpened(context);
        }

        protected override void OnClosing()
        {
            if (!_isSubscribed) return;

            if (_endTurnControl != null) _endTurnControl.Clicked -= OnEndTurnClicked;

            if (_commandSubmenu != null)
            {
                _commandSubmenu.BackRequested -= OnCommandSubmenuBackRequested;
                _commandSubmenu.ConfirmRequested -= OnCommandSubmenuConfirmRequested;
                _commandSubmenu.ActionSelected -= OnCommandActionSelected;
            }

            if (_inputController != null)
            {
                _inputController.CommandCancelRequested -= OnInputCommandCancelRequested;
                _inputController.AttackTargetSelected -= OnAttackTargetSelected;
            }

            if (_turnEventChannel != null) _turnEventChannel.Unregister(OnTurnEvent);
            if (_battleEventChannel != null) _battleEventChannel.Unregister(OnBattleEvent);
            if (_unitEventChannel != null) _unitEventChannel.Unregister(OnUnitEvent);

            if (_turnManager != null)
            {
                _turnManager.OnPhaseChanged -= OnPhaseChanged;
                _turnManager.OnNoLegalActionChanged -= OnNoLegalActionChanged;
            }

            if (_unitManager != null)
            {
                _unitManager.OnUnitSelected -= OnUnitSelected;
                _unitManager.OnUnitDeselected -= OnUnitDeselected;
                _unitManager.OnBattleEnded -= OnBattleEnded;
            }

            _isSubscribed = false;
        }

        public void Execute(string commandId)
        {
            switch (commandId)
            {
                case DefaultBattleHudCommandProvider.MoveCommandId:
                    _selectedUnitBar?.SetSelectedCommand(commandId);
                    _inputController?.BeginMoveCommand();
                    break;

                case DefaultBattleHudCommandProvider.AttackCommandId:
                    _selectedUnitBar?.SetSelectedCommand(commandId);
                    OpenActionSelect();
                    break;

                case DefaultBattleHudCommandProvider.WaitCommandId:
                    _selectedUnitBar?.SetSelectedCommand(commandId);
                    _unitManager?.TryWaitSelectedUnit();
                    break;

                case DefaultBattleHudCommandProvider.UnitDetailsCommandId:
                    LogUnitDetailsCommand();
                    break;
            }
        }

        private void ApplyContext(BattleHudContext context)
        {
            if (context == null) return;

            _turnEventChannel = context.TurnEventChannel != null ? context.TurnEventChannel : _turnEventChannel;
            _battleEventChannel = context.BattleEventChannel != null ? context.BattleEventChannel : _battleEventChannel;
            _unitEventChannel = context.UnitEventChannel != null ? context.UnitEventChannel : _unitEventChannel;
            _turnManager = context.TurnManager != null ? context.TurnManager : _turnManager;
            _unitManager = context.UnitManager != null ? context.UnitManager : _unitManager;
            _inputController = context.InputController != null ? context.InputController : _inputController;
            _cameraFocusLock = context.CameraFocusLock ?? _cameraFocusLock;
            _uiManager = context.UIManager != null ? context.UIManager : _uiManager;

            if (!string.IsNullOrWhiteSpace(context.ResultPopupKey))
                _resultPopupKey = context.ResultPopupKey;
        }

        private void SubscribeEvents()
        {
            if (_isSubscribed) return;

            if (_endTurnControl != null) _endTurnControl.Clicked += OnEndTurnClicked;

            if (_commandSubmenu != null)
            {
                _commandSubmenu.BackRequested += OnCommandSubmenuBackRequested;
                _commandSubmenu.ConfirmRequested += OnCommandSubmenuConfirmRequested;
                _commandSubmenu.ActionSelected += OnCommandActionSelected;
            }

            if (_inputController != null)
            {
                _inputController.CommandCancelRequested += OnInputCommandCancelRequested;
                _inputController.AttackTargetSelected += OnAttackTargetSelected;
            }

            if (_turnEventChannel != null) _turnEventChannel.Register(OnTurnEvent);
            if (_battleEventChannel != null) _battleEventChannel.Register(OnBattleEvent);
            if (_unitEventChannel != null) _unitEventChannel.Register(OnUnitEvent);

            if (_turnManager != null)
            {
                _turnManager.OnPhaseChanged += OnPhaseChanged;
                _turnManager.OnNoLegalActionChanged += OnNoLegalActionChanged;
            }

            if (_unitManager != null)
            {
                _unitManager.OnUnitSelected += OnUnitSelected;
                _unitManager.OnUnitDeselected += OnUnitDeselected;
                _unitManager.OnBattleEnded += OnBattleEnded;
            }

            _isSubscribed = true;
        }

        private void RefreshInitialState()
        {
            if (_turnManager != null)
            {
                _currentTurnNumber = _turnManager.TurnNumber;
                _currentPhase = _turnManager.CurrentPhase;
                _noLegalActions = _unitManager != null && !_unitManager.PlayerHasLegalAction();
            }

            _commandSubmenu?.Hide();
            _battleModeHint?.Hide();
            RefreshTurnBanner();
            RefreshEndTurnControl();
            RefreshSelectedUnitBar();
            RefreshPhaseHint();
        }

        private void OnTurnEvent(BFTurnEventData data)
        {
            _currentTurnNumber = data.TurnNumber;
            RefreshTurnBanner();
        }

        private void OnBattleEvent(BFBattleEventData data)
        {
            if (data.EventType == BFBattleEventType.Victory)
                ShowResult(BattleResult.Victory(data.BattleId, _turnManager != null ? _turnManager.TurnNumber : 0));
            else if (data.EventType == BFBattleEventType.Defeat)
                ShowResult(BattleResult.Defeat(data.BattleId, _turnManager != null ? _turnManager.TurnNumber : 0));
        }

        private void OnUnitEvent(BFUnitEventData data)
        {
            if (data.EventType == "Moved" || data.EventType == "Attacked" || data.EventType == "Waited")
                RefreshSelectedUnitBar();
        }

        private void OnUnitSelected(UnitRuntime unit)
        {
            if (_commandSubmenu != null && _commandSubmenu.CurrentStage != CommandSubmenuView.Stage.Hidden)
                CloseCommandSubmenu(releaseCamera: true);

            RefreshSelectedUnitBar();
        }

        private void OnUnitDeselected(UnitRuntime unit)
        {
            CloseCommandSubmenu(releaseCamera: true);
            RefreshSelectedUnitBar();
        }

        private void OnPhaseChanged(BattlePhase oldPhase, BattlePhase newPhase)
        {
            _currentPhase = newPhase;
            if (newPhase != BattlePhase.PlayerTurn)
                CloseCommandSubmenu(releaseCamera: true);

            RefreshTurnBanner();
            RefreshEndTurnControl();
            RefreshSelectedUnitBar();
            RefreshPhaseHint();
        }

        private void OnNoLegalActionChanged(bool noActions)
        {
            _noLegalActions = noActions;
            RefreshEndTurnControl();
        }

        private void OnBattleEnded(BattleResult result)
        {
            _currentPhase = BattlePhase.Resolution;
            CloseCommandSubmenu(releaseCamera: true);
            RefreshPhaseHint();
            ShowResult(result);
        }

        private void OpenActionSelect()
        {
            var selectedUnit = _unitManager?.SelectedUnit;
            if (selectedUnit == null) return;

            _selectedTarget = null;
            _selectedUnitBar?.Hide();
            _battleModeHint?.Hide();
            _cameraFocusLock?.FocusAndLock(selectedUnit);
            _inputController?.ExitCommandTargeting();
            _commandSubmenu?.ShowActionSelect(BuildMockActions(selectedUnit));
        }

        private void OnCommandSubmenuBackRequested()
        {
            if (_commandSubmenu == null) return;

            if (_commandSubmenu.CurrentStage == CommandSubmenuView.Stage.TargetSelect)
            {
                ReturnToActionSelect();
                return;
            }

            CloseCommandSubmenu(releaseCamera: true);
            RefreshSelectedUnitBar();
        }

        private void OnCommandSubmenuConfirmRequested()
        {
            if (_commandSubmenu == null) return;

            if (_commandSubmenu.CurrentStage == CommandSubmenuView.Stage.ActionSelect)
            {
                if (!_commandSubmenu.TryGetSelectedAction(out var action) || !action.IsEnabled)
                    return;

                _selectedTarget = null;
                _commandSubmenu.ShowTargetSelect();
                _battleModeHint?.Show("选择目标");
                _inputController?.BeginAttackTargetCommand();
                return;
            }

            if (_commandSubmenu.CurrentStage == CommandSubmenuView.Stage.TargetSelect && _selectedTarget != null)
            {
                if (_unitManager != null && _unitManager.TryAttack(_selectedTarget))
                    CloseCommandSubmenu(releaseCamera: true);
            }
        }

        private void OnCommandActionSelected(string actionId)
        {
            _selectedTarget = null;
        }

        private void OnInputCommandCancelRequested()
        {
            if (_commandSubmenu != null && _commandSubmenu.CurrentStage == CommandSubmenuView.Stage.TargetSelect)
                ReturnToActionSelect();
        }

        private void OnAttackTargetSelected(UnitRuntime target)
        {
            if (_commandSubmenu == null || _commandSubmenu.CurrentStage != CommandSubmenuView.Stage.TargetSelect)
                return;

            _selectedTarget = target;
            _commandSubmenu.SetConfirmInteractable(_selectedTarget != null);
        }

        private void ReturnToActionSelect()
        {
            var selectedUnit = _unitManager?.SelectedUnit;
            if (selectedUnit == null)
            {
                CloseCommandSubmenu(releaseCamera: true);
                RefreshSelectedUnitBar();
                return;
            }

            _selectedTarget = null;
            _inputController?.ExitCommandTargeting();
            _battleModeHint?.Hide();
            _commandSubmenu?.ShowActionSelect(BuildMockActions(selectedUnit));
        }

        private void CloseCommandSubmenu(bool releaseCamera)
        {
            _selectedTarget = null;
            _inputController?.ExitCommandTargeting();
            _commandSubmenu?.Hide();
            _battleModeHint?.Hide();
            if (releaseCamera)
                _cameraFocusLock?.ReleaseLock();
        }

        private void RefreshSelectedUnitBar()
        {
            if (_selectedUnitBar == null) return;
            if (_commandSubmenu != null && _commandSubmenu.CurrentStage != CommandSubmenuView.Stage.Hidden)
            {
                _selectedUnitBar.Hide();
                return;
            }

            var selectedUnit = _unitManager?.SelectedUnit;
            bool canShow = selectedUnit != null &&
                           _currentPhase == BattlePhase.PlayerTurn &&
                           selectedUnit.Identity.Faction == UnitFaction.Player &&
                           selectedUnit.Stats.IsAlive &&
                           !selectedUnit.Stats.HasActed;

            if (!canShow)
            {
                _selectedUnitBar.Hide();
                return;
            }

            _selectedUnitBar.Show(_commandProvider.GetCommands(selectedUnit, _turnManager), Execute);
        }

        private void RefreshTurnBanner()
        {
            _turnBanner?.Refresh(_currentTurnNumber, _currentPhase);
        }

        private void RefreshEndTurnControl()
        {
            bool isPlayerTurn = _currentPhase == BattlePhase.PlayerTurn;
            _endTurnControl?.SetState(isPlayerTurn, isPlayerTurn && _noLegalActions);
        }

        private void RefreshPhaseHint()
        {
            if (_battleModeHint == null) return;
            if (_commandSubmenu != null && _commandSubmenu.CurrentStage == CommandSubmenuView.Stage.TargetSelect)
                return;

            switch (_currentPhase)
            {
                case BattlePhase.EnemyTurn:
                    _battleModeHint.Show("敌方行动中");
                    break;
                case BattlePhase.Resolution:
                    _battleModeHint.Show("战斗结算中");
                    break;
                default:
                    _battleModeHint.Hide();
                    break;
            }
        }

        private IReadOnlyList<BattleHudActionViewModel> BuildMockActions(UnitRuntime selectedUnit)
        {
            _mockActions.Clear();
            bool canAttack = selectedUnit != null && selectedUnit.Stats.RemainingActionPoints >= selectedUnit.Stats.AttackCost;
            _mockActions.Add(new BattleHudActionViewModel(
                "basic_attack",
                "普通攻击",
                "对攻击范围内的敌方单位发动一次基础攻击。",
                selectedUnit != null
                    ? $"效果：造成 {selectedUnit.Stats.Attack} 点基础伤害。消耗：{selectedUnit.Stats.AttackCost} AP。范围：{selectedUnit.Stats.AttackRange} 格。"
                    : "效果：无。",
                canAttack,
                "AP 不足，无法发动普通攻击。"));

            for (int i = 1; i <= 8; i++)
            {
                _mockActions.Add(new BattleHudActionViewModel(
                    $"mock_skill_{i}",
                    $"技能占位 {i}",
                    "这是用于验证滚动列表和详情面板的技能占位。",
                    "效果：暂未接入真实技能系统。本条目仅用于测试右侧行动列表滚动和左侧长文本显示。",
                    false,
                    "技能系统暂未接入。"));
            }

            return _mockActions;
        }

        private void LogUnitDetailsCommand()
        {
            var unitName = _unitManager?.SelectedUnit != null
                ? _unitManager.SelectedUnit.Identity.DisplayName
                : "None";
            Debug.Log($"[BattleHUD] Unit details command selected: {unitName}");
        }

        private void ShowResult(BattleResult result)
        {
            if (_uiManager == null || string.IsNullOrWhiteSpace(_resultPopupKey)) return;

            _uiManager.Open(_resultPopupKey, new BattleResultContext(
                result,
                () => _uiManager.Close(_resultPopupKey)));
        }

        private void OnEndTurnClicked()
        {
            CloseCommandSubmenu(releaseCamera: true);
            _inputController?.OnEndTurnClicked();
        }
    }
}
