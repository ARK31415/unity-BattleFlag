using BF.Game.Runtime.Battle;
using BF.Game.Runtime.Battle.Events;
using BF.Game.Runtime.Battle.Managers;
using BF.Game.Runtime.Battle.PlayerInput;
using BF.Game.Runtime.Battle.Units;
using UnityEngine;
using UnityEngine.UI;
using Wit.Framework.UI;

namespace BF.Game.Runtime.UI.Battle
{
    /// <summary>
    /// 战斗 HUD 正式 Window，由 WitUIManager 按 battle.hud key 打开。
    ///
    /// 职责边界：
    /// - 负责显示回合提示（合并 Turn 数字 + 阶段文字）、选中单位信息和结束回合按钮。
    /// - 负责把按钮交互转发给输入控制器，把胜负结果转发给 WitUIManager 打开结算弹窗。
    /// - 不负责战斗规则计算，也不负责场景装配。
    /// - 所有 UI 控件通过 SerializeField 绑定，不在运行时扫描 Canvas。
    ///
    /// 运行前提：
    /// - Prefab 上挂载此组件，并拖入 Banner 背景 Text、Button、UnitInfoWidget、事件通道和 Manager 引用。
    /// - 通过 BattleHudContext 接收事件通道、Manager、输入控制器和 UIManager 依赖。
    /// </summary>
    [DisallowMultipleComponent]
    public class BattleHudView : WitUIView<BattleHudContext>
    {
        [Header("Top Bar")]
        // 合并的回合提示文字，格式 "Turn {N} · {阶段}"。
        [SerializeField] private Text _turnPhaseText;

        [Header("Unit Info")]
        [SerializeField] private UnitInfoWidget _unitInfoWidget;

        [Header("Action Buttons")]
        [SerializeField] private Button _endTurnButton;
        [SerializeField] private Color _endTurnNormalColor = Color.white;
        [SerializeField] private Color _endTurnHighlightColor = new Color(1f, 0.8f, 0f, 1f);

        [Header("Event Channels")]
        [SerializeField] private BFTurnEventSO _turnEventChannel;
        [SerializeField] private BFBattleEventSO _battleEventChannel;
        [SerializeField] private BFUnitEventSO _unitEventChannel;

        [Header("Managers")]
        [SerializeField] private BFBattleTurnManager _turnManager;
        [SerializeField] private BFBattleUnitManager _unitManager;
        [SerializeField] private BFBattleInputController _inputController;

        private WitUIManager _uiManager;
        private string _resultPopupKey = "battle.result";
        private bool _isSubscribed;
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

            if (_endTurnButton != null)
                _endTurnButton.onClick.RemoveListener(OnEndTurnClicked);

            if (_turnEventChannel != null) _turnEventChannel.Unregister(OnTurnEvent);
            if (_battleEventChannel != null) _battleEventChannel.Unregister(OnBattleEvent);
            if (_unitEventChannel != null) _unitEventChannel.Unregister(OnUnitEvent);

            if (_turnManager != null)
            {
                _turnManager.OnPhaseChanged -= OnPhaseChanged;
                _turnManager.OnNoLegalActionChanged -= OnNoLegalActionChanged;
            }

            if (_unitManager != null)
                _unitManager.OnBattleEnded -= OnBattleEnded;

            _isSubscribed = false;
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
            _uiManager = context.UIManager != null ? context.UIManager : _uiManager;

            if (!string.IsNullOrWhiteSpace(context.ResultPopupKey))
                _resultPopupKey = context.ResultPopupKey;
        }

        private void SubscribeEvents()
        {
            if (_isSubscribed) return;

            _unitInfoWidget?.SetVisible(false);

            if (_endTurnButton != null)
            {
                _endTurnButton.onClick.RemoveListener(OnEndTurnClicked);
                _endTurnButton.onClick.AddListener(OnEndTurnClicked);
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
                _unitManager.OnBattleEnded += OnBattleEnded;

            _isSubscribed = true;
        }

        private void RefreshInitialState()
        {
            if (_turnManager != null)
            {
                _currentTurnNumber = _turnManager.TurnNumber;
                _currentPhase = _turnManager.CurrentPhase;
            }

            RefreshTurnPhaseText();
            RefreshSelectedUnitInfo();
        }

        private void OnTurnEvent(BFTurnEventData data)
        {
            _currentTurnNumber = data.TurnNumber;
            RefreshTurnPhaseText();
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
            if (data.EventType == "Selected")
                ShowUnitInfo(data.UnitId);
            else if (data.EventType == "Deselected")
                _unitInfoWidget?.SetVisible(false);
            else if (data.EventType == "Moved" || data.EventType == "Attacked")
                RefreshSelectedUnitInfo();
        }

        private void OnPhaseChanged(BattlePhase oldPhase, BattlePhase newPhase)
        {
            _currentPhase = newPhase;
            RefreshTurnPhaseText();

            if (_endTurnButton != null)
                _endTurnButton.interactable = newPhase == BattlePhase.PlayerTurn;
        }

        // 将缓存的回合数和阶段合并写入同一 Text，格式 "Turn {N} · {阶段}"。
        private void RefreshTurnPhaseText()
        {
            if (_turnPhaseText == null) return;

            _turnPhaseText.text = $"Turn {_currentTurnNumber} · {FormatPhase(_currentPhase)}";
        }

        private void OnNoLegalActionChanged(bool noActions)
        {
            if (_endTurnButton == null) return;

            var image = _endTurnButton.GetComponent<Image>();
            if (image != null)
                image.color = noActions ? _endTurnHighlightColor : _endTurnNormalColor;
        }

        private void OnBattleEnded(BattleResult result)
        {
            ShowResult(result);
        }

        private void ShowUnitInfo(string unitId)
        {
            var unit = FindUnitById(unitId);
            if (unit != null)
                _unitInfoWidget?.SetData(UnitInfoWidgetData.FromUnit(unit));
        }

        private void RefreshSelectedUnitInfo()
        {
            if (_unitManager == null || _unitManager.SelectedUnit == null)
                return;

            _unitInfoWidget?.SetData(UnitInfoWidgetData.FromUnit(_unitManager.SelectedUnit));
        }

        private UnitRuntime FindUnitById(string unitId)
        {
            if (_unitManager?.AllUnits == null) return null;
            return _unitManager.AllUnits.Find(u => u != null && u.UnitId == unitId);
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
            _inputController?.OnEndTurnClicked();
            _unitInfoWidget?.SetVisible(false);
        }

        private static string FormatPhase(BattlePhase phase)
        {
            return phase switch
            {
                BattlePhase.PlayerTurn => "Player Turn",
                BattlePhase.EnemyTurn => "Enemy Turn",
                BattlePhase.Resolution => "Battle End",
                _ => string.Empty
            };
        }
    }
}
