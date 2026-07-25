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
    /// 战斗 HUD 正式 Window。依赖 Context 和序列化绑定，不在运行时扫描场景 Canvas。
    /// </summary>
    [DisallowMultipleComponent]
    public class BattleHudView : WitUIView<BattleHudContext>
    {
        [Header("Top Bar")]
        [SerializeField] private Text _turnText;
        [SerializeField] private Text _phaseText;

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
            if (_turnText != null && _turnManager != null)
                _turnText.text = $"Turn {_turnManager.TurnNumber}";

            if (_phaseText != null && _turnManager != null)
                _phaseText.text = FormatPhase(_turnManager.CurrentPhase);

            RefreshSelectedUnitInfo();
        }

        private void OnTurnEvent(BFTurnEventData data)
        {
            if (_turnText != null)
                _turnText.text = $"Turn {data.TurnNumber}";
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
            if (_phaseText != null)
                _phaseText.text = FormatPhase(newPhase);

            if (_endTurnButton != null)
                _endTurnButton.interactable = newPhase == BattlePhase.PlayerTurn;
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
