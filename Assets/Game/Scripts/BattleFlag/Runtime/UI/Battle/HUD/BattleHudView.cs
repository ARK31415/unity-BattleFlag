using System.Collections.Generic;
using BF.Game.Runtime.Battle;
using BF.Game.Runtime.Battle.Events;
using BF.Game.Runtime.Battle.Flow;
using BF.Game.Runtime.Battle.Input;
using BF.Game.Runtime.Battle.Managers;
using BF.Game.Runtime.Battle.PlayerInput;
using BF.Game.Runtime.Battle.Query;
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
        [SerializeField] private BFBattleActionCoordinator _actionCoordinator;
        [SerializeField] private BFBattleSelectionController _selectionController;
        [SerializeField] private BFBattleSelectionCoordinator _selectionCoordinator;
        [SerializeField] private BFBattleInputController _inputController;

        private IBFBattleUnitQuery _unitQuery;
        private IBFBattleActionGateway _actionGateway;
        private readonly IBattleHudCommandProvider _commandProvider = new DefaultBattleHudCommandProvider();
        private readonly List<BattleHudActionViewModel> _mockActions = new();
        private WitUIManager _uiManager;
        private IBattleHudCameraFocusLock _cameraFocusLock;
        private string _selectedTargetRuntimeId;
        private string _resultPopupKey = "battle.result";
        private bool _isSubscribed;
        private bool _hasShownBattleResult;
        private bool _noLegalActions;
        private int _currentTurnNumber;
        private BattlePhase _currentPhase;

        protected override void OnOpened(BattleHudContext context)
        {
            _hasShownBattleResult = false;
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
            // HUD 可能在攻击/技能子菜单打开时因战斗场景销毁而关闭，必须先释放表现侧临时状态。
            // 相机属于常驻场景对象，不能依赖正常的返回/确认路径释放锁定。
            ClearTransientPresentationState();

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

            if (_selectionController != null)
                _selectionController.SelectionChanged -= OnSelectionChanged;

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
                    if (_actionGateway != null && _selectionController != null)
                        _actionGateway.TryWait(_selectionController.SelectedRuntimeId);
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
            _actionCoordinator = context.ActionCoordinator != null ? context.ActionCoordinator : _actionCoordinator;
            _actionGateway = context.ActionGateway ?? _actionCoordinator;
            _selectionController = context.SelectionController != null ? context.SelectionController : _selectionController;
            _selectionCoordinator = context.SelectionCoordinator != null ? context.SelectionCoordinator : _selectionCoordinator;
            _unitQuery = context.UnitQuery ?? _unitQuery;
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

            if (_selectionController != null)
                _selectionController.SelectionChanged += OnSelectionChanged;

            _isSubscribed = true;
        }

        private void RefreshInitialState()
        {
            if (_turnManager != null)
            {
                _currentTurnNumber = _turnManager.TurnNumber;
                _currentPhase = _turnManager.CurrentPhase;
                _noLegalActions = _actionCoordinator != null && !_actionCoordinator.PlayerHasLegalAction();
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
        }

        private void OnBattleEvent(BFBattleEventData data)
        {
            if (data.EventType == BFBattleEventType.Victory)
            {
                _currentPhase = BattlePhase.Resolution;
                CloseCommandSubmenu(releaseCamera: true);
                RefreshPhaseHint();
                ShowResult(
                           BattleResult.Victory(data.BattleId, _turnManager != null ? _turnManager.TurnNumber : 0));
            }
            else if (data.EventType == BFBattleEventType.Defeat)
            {
                _currentPhase = BattlePhase.Resolution;
                CloseCommandSubmenu(releaseCamera: true);
                RefreshPhaseHint();
                ShowResult(
                           BattleResult.Defeat(data.BattleId, _turnManager != null ? _turnManager.TurnNumber : 0));
            }
        }

        private void OnUnitEvent(BFUnitEventData data)
        {
            if (ShouldRefreshForUnitEvent(data.EventType))
                RefreshSelectedUnitBar();
        }

        private static bool ShouldRefreshForUnitEvent(string eventType)
        {
            return eventType == "Moved" || eventType == "Damaged" || eventType == "Waited";
        }

        private void OnSelectionChanged(string runtimeId)
        {
            if (_commandSubmenu != null && _commandSubmenu.CurrentStage != CommandSubmenuView.Stage.Hidden)
                CloseCommandSubmenu(releaseCamera: true);

            RefreshSelectedUnitBar();
        }

        private void OnPhaseChanged(BattlePhase oldPhase, BattlePhase newPhase)
        {
            _currentPhase = newPhase;
            _currentTurnNumber = _turnManager != null ? _turnManager.TurnNumber : _currentTurnNumber;
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

        private void OpenActionSelect()
        {
            if (!TryGetSelectedSnapshot(out var selectedUnit)) return;

            _selectedTargetRuntimeId = null;
            _selectedUnitBar?.Hide();
            _battleModeHint?.Hide();
            _cameraFocusLock?.FocusAndLock(selectedUnit.RuntimeId);
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

                _selectedTargetRuntimeId = null;
                _commandSubmenu.ShowTargetSelect();
                _battleModeHint?.Show("选择目标");
                _inputController?.BeginAttackTargetCommand();
                return;
            }

            if (_commandSubmenu.CurrentStage == CommandSubmenuView.Stage.TargetSelect &&
                !string.IsNullOrWhiteSpace(_selectedTargetRuntimeId))
            {
                if (_actionGateway != null && _selectionController != null &&
                    _actionGateway.TryAttack(
                        _selectionController.SelectedRuntimeId,
                        _selectedTargetRuntimeId))
                    CloseCommandSubmenu(releaseCamera: true);
            }
        }

        private void OnCommandActionSelected(string actionId)
        {
            _selectedTargetRuntimeId = null;
        }

        private void OnInputCommandCancelRequested()
        {
            if (_commandSubmenu != null && _commandSubmenu.CurrentStage == CommandSubmenuView.Stage.TargetSelect)
                ReturnToActionSelect();
        }

        private void OnAttackTargetSelected(string runtimeId)
        {
            if (_commandSubmenu == null || _commandSubmenu.CurrentStage != CommandSubmenuView.Stage.TargetSelect)
                return;

            _selectedTargetRuntimeId = runtimeId;
            _commandSubmenu.SetConfirmInteractable(!string.IsNullOrWhiteSpace(runtimeId));
        }

        private void ReturnToActionSelect()
        {
            if (!TryGetSelectedSnapshot(out var selectedUnit))
            {
                CloseCommandSubmenu(releaseCamera: true);
                RefreshSelectedUnitBar();
                return;
            }

            _selectedTargetRuntimeId = null;
            _inputController?.ExitCommandTargeting();
            _battleModeHint?.Hide();
            _commandSubmenu?.ShowActionSelect(BuildMockActions(selectedUnit));
        }

        private void CloseCommandSubmenu(bool releaseCamera)
        {
            _selectedTargetRuntimeId = null;
            _inputController?.ExitCommandTargeting();
            _commandSubmenu?.Hide();
            _battleModeHint?.Hide();
            if (releaseCamera)
                _cameraFocusLock?.ReleaseLock();
        }

        /// <summary>
        /// 清理 HUD 关闭时的表现侧临时状态。
        /// 不调用战斗输入或棋盘适配器，避免常驻 HUD 在旧战斗对象销毁后访问失效引用。
        /// </summary>
        private void ClearTransientPresentationState()
        {
            _selectedTargetRuntimeId = null;
            _commandSubmenu?.Hide();
            _battleModeHint?.Hide();
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

            if (!TryGetSelectedSnapshot(out var selectedUnit))
            {
                _selectedUnitBar.Hide();
                return;
            }

            bool canShow =
                           _currentPhase == BattlePhase.PlayerTurn &&
                           selectedUnit.Faction == BF.Game.Battle.Domain.Events.BFUnitFaction.Player &&
                           selectedUnit.IsAlive &&
                           !selectedUnit.HasActed;

            if (!canShow)
            {
                _selectedUnitBar.Hide();
                return;
            }

            _selectedUnitBar.Show(_commandProvider.GetCommands(selectedUnit, true, _turnManager), Execute);
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

        private IReadOnlyList<BattleHudActionViewModel> BuildMockActions(BFUnitViewSnapshot selectedUnit)
        {
            _mockActions.Clear();
            bool canAttack = selectedUnit.RemainingActionPoints >= selectedUnit.AttackCost;
            _mockActions.Add(new BattleHudActionViewModel(
                "basic_attack",
                "普通攻击",
                "对攻击范围内的敌方单位发动一次基础攻击。",
                $"效果：造成 {selectedUnit.Attack} 点基础伤害。消耗：{selectedUnit.AttackCost} AP。范围：{selectedUnit.AttackRange} 格。",
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
            var unitName = TryGetSelectedSnapshot(out var snapshot) ? snapshot.DisplayName : "None";
            Debug.Log($"[BattleHUD] Unit details command selected: {unitName}");
        }

        private bool TryGetSelectedSnapshot(out BFUnitViewSnapshot snapshot)
        {
            snapshot = default;
            var runtimeId = _selectionController?.SelectedRuntimeId;
            return _unitQuery != null &&
                   !string.IsNullOrWhiteSpace(runtimeId) &&
                   _unitQuery.TryGetSnapshot(runtimeId, out snapshot);
        }

        private void ShowResult(BattleResult result)
        {
            if (result == null || !result.HasResult || _hasShownBattleResult) return;
            if (_uiManager == null || string.IsNullOrWhiteSpace(_resultPopupKey)) return;
            _hasShownBattleResult = true;

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
