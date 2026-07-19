using BF.Game.Runtime.Battle.Events;
using BF.Game.Runtime.Battle.Managers;
using BF.Game.Runtime.Battle.PlayerInput;
using BF.Game.Runtime.Battle.Units;
using UnityEngine;
using UnityEngine.UI;

namespace BF.Game.Runtime.Battle.Presentation
{
    /// <summary>
    /// 战斗 HUD。显示回合/阶段信息、单位面板、结束回合按钮。
    /// 结束回合按钮高亮逻辑：订阅 TurnManager.OnNoLegalActionChanged（Spec 第 6 节）。
    /// </summary>
    public class BFBattleHUD : MonoBehaviour
    {
        [Header("Top Bar")]
        [SerializeField] private Text _turnText;
        [SerializeField] private Text _phaseText;

        [Header("Unit Info Panel")]
        [SerializeField] private GameObject _unitInfoPanel;
        [SerializeField] private Text _unitNameText;
        [SerializeField] private Image _unitHPFill;
        [SerializeField] private Text _unitHPText;
        [SerializeField] private Text _unitATKText;
        [SerializeField] private Text _unitAPText;

        [Header("Action Buttons")]
        [SerializeField] private Button _endTurnButton;
        [SerializeField] private Color _endTurnNormalColor = Color.white;
        [SerializeField] private Color _endTurnHighlightColor = new Color(1f, 0.8f, 0f, 1f);

        [Header("Battle Result")]
        [SerializeField] private GameObject _resultPopup;
        [SerializeField] private Text _resultText;
        [SerializeField] private Button _resultCloseButton;

        [Header("Event Channels")]
        [SerializeField] private BFTurnEventSO _turnEventChannel;
        [SerializeField] private BFBattleEventSO _battleEventChannel;
        [SerializeField] private BFUnitEventSO _unitEventChannel;

        [Header("Managers")]
        [SerializeField] private BFBattleTurnManager _turnManager;
        [SerializeField] private BFBattleUnitManager _unitManager;
        [SerializeField] private BFBattleInputController _inputController;

        private void Start()
        {
            AutoResolveReferences();
            SubscribeEvents();
        }

        private void AutoResolveReferences()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();

            if (_turnText == null && canvas != null) _turnText = FindChild<Text>(canvas.transform, "TurnText");
            if (_phaseText == null && canvas != null) _phaseText = FindChild<Text>(canvas.transform, "PhaseText");
            if (_unitInfoPanel == null && canvas != null) _unitInfoPanel = FindChild(canvas.transform, "UnitInfoPanel");
            if (_unitNameText == null && _unitInfoPanel != null) _unitNameText = FindChild<Text>(_unitInfoPanel.transform, "UnitName");
            if (_unitHPFill == null && _unitInfoPanel != null) _unitHPFill = FindChild<Image>(_unitInfoPanel.transform, "HPBarFill");
            if (_unitHPText == null && _unitInfoPanel != null) _unitHPText = FindChild<Text>(_unitInfoPanel.transform, "HPText");
            if (_unitATKText == null && _unitInfoPanel != null) _unitATKText = FindChild<Text>(_unitInfoPanel.transform, "ATKText");
            if (_unitAPText == null && _unitInfoPanel != null) _unitAPText = FindChild<Text>(_unitInfoPanel.transform, "APText");
            if (_unitAPText == null && _unitInfoPanel != null) _unitAPText = CreateFallbackAPText();
            if (_endTurnButton == null && canvas != null) _endTurnButton = FindChild<Button>(canvas.transform, "EndTurnButton");
            if (_resultPopup == null && canvas != null) _resultPopup = FindChild(canvas.transform, "ResultPopup");
            if (_resultText == null && _resultPopup != null) _resultText = FindChild<Text>(_resultPopup.transform, "ResultText");
            if (_resultCloseButton == null && _resultPopup != null) _resultCloseButton = FindChild<Button>(_resultPopup.transform, "CloseButton");

            if (_turnManager == null) _turnManager = FindFirstObjectByType<BFBattleTurnManager>();
            if (_unitManager == null) _unitManager = FindFirstObjectByType<BFBattleUnitManager>();
            if (_inputController == null) _inputController = FindFirstObjectByType<BFBattleInputController>();
        }

        private void SubscribeEvents()
        {
            if (_unitInfoPanel != null) _unitInfoPanel.SetActive(false);
            if (_resultPopup != null) _resultPopup.SetActive(false);

            if (_endTurnButton != null)
            {
                _endTurnButton.onClick.RemoveAllListeners();
                _endTurnButton.onClick.AddListener(OnEndTurnClicked);
                Debug.Log("[BFBattleHUD] EndTurn button bound");
            }

            if (_resultCloseButton != null)
            {
                _resultCloseButton.onClick.RemoveAllListeners();
                _resultCloseButton.onClick.AddListener(OnResultCloseClicked);
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
                _unitManager.OnBattleEnded += OnBattleEnded;
            }
        }

        private void OnDestroy()
        {
            if (_turnEventChannel != null) _turnEventChannel.Unregister(OnTurnEvent);
            if (_battleEventChannel != null) _battleEventChannel.Unregister(OnBattleEvent);
            if (_unitEventChannel != null) _unitEventChannel.Unregister(OnUnitEvent);
            if (_turnManager != null)
            {
                _turnManager.OnPhaseChanged -= OnPhaseChanged;
                _turnManager.OnNoLegalActionChanged -= OnNoLegalActionChanged;
            }
            if (_unitManager != null) _unitManager.OnBattleEnded -= OnBattleEnded;
        }

        // ---- Event Handlers ----

        private void OnTurnEvent(BFTurnEventData data)
        {
            if (_turnText != null) _turnText.text = $"Turn {data.TurnNumber}";
        }

        private void OnBattleEvent(BFBattleEventData data)
        {
            if (data.EventType == BFBattleEventType.Victory || data.EventType == BFBattleEventType.Defeat)
                ShowResult(data.EventType == BFBattleEventType.Victory);
        }

        private void OnUnitEvent(BFUnitEventData data)
        {
            if (data.EventType == "Selected") ShowUnitInfo(data.UnitId);
            else if (data.EventType == "Deselected") { if (_unitInfoPanel != null) _unitInfoPanel.SetActive(false); }
            else if (data.EventType == "Moved" || data.EventType == "Attacked") RefreshSelectedUnitInfo();
        }

        private void OnPhaseChanged(BattlePhase oldPhase, BattlePhase newPhase)
        {
            if (_phaseText != null)
            {
                _phaseText.text = newPhase switch
                {
                    BattlePhase.PlayerTurn => "Player Turn",
                    BattlePhase.EnemyTurn => "Enemy Turn",
                    BattlePhase.Resolution => "Battle End",
                    _ => ""
                };
            }

            if (_endTurnButton != null)
                _endTurnButton.interactable = newPhase == BattlePhase.PlayerTurn;
        }

        private void OnNoLegalActionChanged(bool noActions)
        {
            // Spec 第 6 节：无合法操作时高亮结束回合按钮，但不自动结束
            if (_endTurnButton != null)
            {
                var image = _endTurnButton.GetComponent<Image>();
                if (image != null)
                    image.color = noActions ? _endTurnHighlightColor : _endTurnNormalColor;
            }
        }

        private void OnBattleEnded(BattleResult result)
        {
            ShowResult(result.IsPlayerVictory);
        }

        // ---- Unit Info ----

        private void ShowUnitInfo(string unitId)
        {
            var unit = FindUnitById(unitId);
            if (unit != null) RefreshUnitInfo(unit);
        }

        private void RefreshSelectedUnitInfo()
        {
            if (_unitManager != null && _unitManager.SelectedUnit != null)
                RefreshUnitInfo(_unitManager.SelectedUnit);
        }

        private UnitRuntime FindUnitById(string unitId)
        {
            if (_unitManager?.AllUnits == null) return null;
            return _unitManager.AllUnits.Find(u => u != null && u.UnitId == unitId);
        }

        private void RefreshUnitInfo(UnitRuntime unit)
        {
            if (unit == null || _unitInfoPanel == null) return;
            _unitInfoPanel.SetActive(true);

            // HUD 只读取 Identity/Stats 的展示数据，不读取 Combat 或直接修改单位状态。
            var identity = unit.Identity;
            var stats = unit.Stats;

            if (_unitNameText != null) _unitNameText.text = identity.DisplayName;
            float hpRatio = stats.MaxHP > 0 ? (float)stats.CurrentHP / stats.MaxHP : 0f;
            if (_unitHPFill != null)
            {
                _unitHPFill.fillAmount = hpRatio;
                _unitHPFill.color = hpRatio > 0.5f ? Color.green :
                    hpRatio > 0.25f ? new Color(1f, 0.8f, 0f) : Color.red;
            }
            if (_unitHPText != null) _unitHPText.text = $"HP: {stats.CurrentHP}/{stats.MaxHP}";
            if (_unitATKText != null) _unitATKText.text = $"ATK: {stats.Attack}";
            if (_unitAPText != null) _unitAPText.text = $"AP: {stats.RemainingActionPoints}/{stats.MaxActionPoints}";
        }

        // ---- Result Popup ----

        private void ShowResult(bool isVictory)
        {
            if (_resultPopup == null) return;
            _resultPopup.SetActive(true);
            if (_resultText != null) _resultText.text = isVictory ? "VICTORY" : "DEFEAT";
        }

        // ---- Button Callbacks ----

        public void OnEndTurnClicked()
        {
            Debug.Log("[BFBattleHUD] EndTurn button clicked!");
            _inputController?.OnEndTurnClicked();
            if (_unitInfoPanel != null) _unitInfoPanel.SetActive(false);
        }

        public void OnResultCloseClicked()
        {
            if (_resultPopup != null) _resultPopup.SetActive(false);
        }

        // ---- Helpers ----

        private GameObject FindChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child.gameObject;
                var f = FindChild(child, name);
                if (f != null) return f;
            }
            return null;
        }

        private T FindChild<T>(Transform parent, string name) where T : Component
        {
            var go = FindChild(parent, name);
            return go != null ? go.GetComponent<T>() : null;
        }

        private Text CreateFallbackAPText()
        {
            if (_unitInfoPanel == null || _unitATKText == null) return null;

            var apGo = Instantiate(_unitATKText.gameObject, _unitATKText.transform.parent);
            apGo.name = "APText";

            var apRect = apGo.GetComponent<RectTransform>();
            var sourceRect = _unitATKText.GetComponent<RectTransform>();
            if (apRect != null && sourceRect != null)
            {
                apRect.anchorMin = sourceRect.anchorMin;
                apRect.anchorMax = sourceRect.anchorMax;
                apRect.pivot = sourceRect.pivot;
                apRect.anchoredPosition = sourceRect.anchoredPosition + new Vector2(0f, -24f);
                apRect.sizeDelta = sourceRect.sizeDelta;
                apRect.localScale = sourceRect.localScale;
            }

            var text = apGo.GetComponent<Text>();
            if (text != null) text.text = "AP: 0/0";
            return text;
        }
    }
}
