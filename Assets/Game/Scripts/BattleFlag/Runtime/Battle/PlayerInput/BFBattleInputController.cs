using System;
using System.Collections.Generic;
using BF.Game.Runtime.Battle.Managers;
using BF.Game.Runtime.Battle.Units;
using BF.Game.Runtime.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace BF.Game.Runtime.Battle.PlayerInput
{
    /// <summary>
    /// 鐜╁杈撳叆鎺у埗鍣ㄣ€備粎瑙ｉ噴鐜╁杈撳叆锛堥€変腑/绉诲姩/鏀诲嚮/缁撴潫鍥炲悎锛夛紝
    /// 璋冪敤涓変釜 Manager 鐨勫叕寮€鍚堝悓銆傝嚜韬笉鎸佹湁鏍稿績閫昏緫銆?
    ///
    /// 杈撳叆娑堣垂杩佺Щ涓虹洿鎺ヤ娇鐢?BFInputManager.Actions 鐨勫己绫诲瀷 Action锛?
    /// 涓嶅啀閫氳繃瀛楃涓?key 鏌ヨ鏃ц緭鍏ヤ笂涓嬫枃锛屾敼涓虹洿鎺ヤ娇鐢?BFInputManager.Actions 寮虹被鍨?Action銆?
    /// </summary>
    [DisallowMultipleComponent]
    public class BFBattleInputController : MonoBehaviour
    {
        [Header("Managers")]
        [SerializeField] private BFBattleTurnManager _turnManager;
        [SerializeField] private BFBattleBoardManager _boardManager;
        [SerializeField] private BFBattleUnitManager _unitManager;

        [Header("Camera")]
        [SerializeField] private Camera _camera;

        [Header("Input")]
        [SerializeField] private BFInputManager _inputManager;

        private InputAction _pointAction;
        private InputAction _selectAction;
        private InputAction _cancelAction;
        private InputAction _endTurnAction;
        private readonly List<RaycastResult> _uiRaycastResults = new();
        private Vector2 _lastPointerPosition;
        private BattleInputMode _inputMode;

        public event Action CommandCancelRequested;
        public event Action<UnitRuntime> AttackTargetSelected;

        private enum BattleInputMode
        {
            Selection,
            MoveTarget,
            AttackTarget
        }

        private void OnEnable()
        {
            RegisterInputActions();
        }

        private void OnDisable()
        {
            DisposeInputSubscriptions();
        }

        private void OnDestroy()
        {
            if (_unitManager != null)
            {
                _unitManager.OnUnitMoveCompleted -= UnitManager_OnUnitMoveCompleted;
            }

            DisposeInputSubscriptions();
        }

        private void Start()
        {
            ResolveCrossSceneReferences();

            if (_unitManager != null)
            {
                _unitManager.OnUnitMoveCompleted += UnitManager_OnUnitMoveCompleted;
            }
        }

        private void Update()
        {
            ResolveCrossSceneReferences();
        }

        private void ResolveCrossSceneReferences()
        {
            if (_camera == null)
                _camera = Camera.main;

            if (_selectAction == null)
                RegisterInputActions();
        }

        private void RegisterInputActions()
        {
            if (_selectAction != null)
                return;

            if (_inputManager == null || _inputManager.Actions == null)
                _inputManager = BFInputManager.Instance;

            if (_inputManager?.Actions == null) return;

            _pointAction = _inputManager.Actions.Battle.Point;
            _selectAction = _inputManager.Actions.Battle.Select;
            _cancelAction = _inputManager.Actions.Battle.Cancel;
            _endTurnAction = _inputManager.Actions.Battle.EndTurn;

            _selectAction.performed += OnSelectPerformed;
            _cancelAction.performed += OnCancelPerformed;
            _endTurnAction.performed += OnEndTurnPerformed;
        }

        private void DisposeInputSubscriptions()
        {
            if (_selectAction != null) _selectAction.performed -= OnSelectPerformed;
            if (_cancelAction != null) _cancelAction.performed -= OnCancelPerformed;
            if (_endTurnAction != null) _endTurnAction.performed -= OnEndTurnPerformed;

            _selectAction = null;
            _cancelAction = null;
            _endTurnAction = null;
            _pointAction = null;
        }

        private void OnSelectPerformed(InputAction.CallbackContext ctx)
        {
            HandleClick();
        }

        private void OnCancelPerformed(InputAction.CallbackContext ctx)
        {
            if (!CanHandleBattleInput()) return;
            if (_inputMode == BattleInputMode.AttackTarget)
            {
                CommandCancelRequested?.Invoke();
                return;
            }

            CancelSelection();
        }

        private void OnEndTurnPerformed(InputAction.CallbackContext ctx)
        {
            OnEndTurnClicked();
        }

        private bool CanHandleBattleInput()
        {
            if (_turnManager == null || _unitManager == null) return false;
            if (_unitManager.IsActionLocked) return false;
            return _turnManager.CurrentPhase == BattlePhase.PlayerTurn;
        }

        private void HandleClick()
        {
            if (!CanHandleBattleInput()) return;

            Vector2 screenPosition = _pointAction != null
                ? _pointAction.ReadValue<Vector2>()
                : _lastPointerPosition;
            _lastPointerPosition = screenPosition;

            if (IsPointerOverBlockingUI(screenPosition)) return;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            Vector3 mouseWorld = _camera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, 0f));
            mouseWorld.z = 0f;

            RaycastHit2D hit = Physics2D.Raycast(mouseWorld, Vector2.zero);

            if (hit.collider == null)
            {
                if (_inputMode == BattleInputMode.MoveTarget)
                    TryMoveToWorld(mouseWorld);
                else if (_inputMode == BattleInputMode.AttackTarget)
                    CommandCancelRequested?.Invoke();
                else
                    CancelSelection();
                return;
            }

            var clickedUnit = hit.collider.gameObject.GetComponent<UnitRuntime>();
            if (clickedUnit != null)
                HandleUnitClick(clickedUnit);
            else if (_inputMode == BattleInputMode.MoveTarget)
                TryMoveToWorld(mouseWorld);
            else if (_inputMode == BattleInputMode.AttackTarget)
                CommandCancelRequested?.Invoke();
        }

        private bool IsPointerOverBlockingUI(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return false;

            _uiRaycastResults.Clear();
            var pointerEventData = new PointerEventData(eventSystem)
            {
                position = screenPosition
            };
            eventSystem.RaycastAll(pointerEventData, _uiRaycastResults);
            return _uiRaycastResults.Count > 0;
        }

        private void HandleUnitClick(UnitRuntime unit)
        {
            if (_inputMode == BattleInputMode.MoveTarget)
            {
                SelectUnit(unit);
                return;
            }

            if (_inputMode == BattleInputMode.AttackTarget)
            {
                if (_unitManager.SelectedUnit != null &&
                    unit.Identity.Faction != _unitManager.SelectedUnit.Identity.Faction)
                {
                    AttackTargetSelected?.Invoke(unit);
                }
                return;
            }

            SelectUnit(unit);
        }

        private void SelectUnit(UnitRuntime unit)
        {
            if (unit == null || !unit.Stats.IsAlive) return;
            if (!_unitManager.TrySelectUnit(unit)) return;

            bool canActWithSelectedUnit = unit.Identity.Faction == UnitFaction.Player && !unit.Stats.HasActed;
            if (!canActWithSelectedUnit)
            {
                _inputMode = BattleInputMode.Selection;
                _boardManager?.ResetCellColors();
                return;
            }

            Debug.Log($"[Input] Selected {unit.Identity.DisplayName}");
            _inputMode = BattleInputMode.Selection;
            _boardManager?.ResetCellColors();
        }

        private void TryMoveToWorld(Vector3 worldPos)
        {
            if (_inputMode != BattleInputMode.MoveTarget || _unitManager.SelectedUnit == null) return;

            Vector2Int targetCell = _boardManager.WorldToCell(worldPos);
            var reachable = _unitManager.GetReachableCellsForSelected();
            if (!reachable.Contains(targetCell)) return;

            if (!_unitManager.TryMoveUnit(targetCell)) return;

            _inputMode = BattleInputMode.Selection;
            _boardManager?.ResetCellColors();
        }

        private void HighlightAttackTargets()
        {
            var targets = _unitManager.GetAttackableTargets();
            _boardManager?.HighlightAttackTargets(targets);
        }

        private void UnitManager_OnUnitMoveCompleted(UnitRuntime unit)
        {
            if (_turnManager == null || _unitManager == null) return;
            if (_turnManager.CurrentPhase != BattlePhase.PlayerTurn) return;
            if (_unitManager.SelectedUnit != unit) return;

            _boardManager?.ResetCellColors();
            _inputMode = BattleInputMode.Selection;
        }

        public void CancelSelection()
        {
            if (_unitManager != null && _unitManager.IsActionLocked) return;

            _unitManager?.DeselectUnit();
            _inputMode = BattleInputMode.Selection;
            _boardManager?.ResetCellColors();
        }

        public void BeginMoveCommand()
        {
            if (!CanHandleBattleInput()) return;
            if (_unitManager?.SelectedUnit == null) return;

            var reachable = _unitManager.GetReachableCellsForSelected();
            Debug.Log($"[Input] Move command for {_unitManager.SelectedUnit.Identity.DisplayName}, reachable: {reachable.Count}");
            _boardManager?.ResetCellColors();
            _boardManager?.HighlightCells(reachable,
                _boardManager != null ? _boardManager.ReachableColor : new Color(1f, 0.92f, 0.2f, 0.75f));
            _inputMode = BattleInputMode.MoveTarget;
        }

        public void BeginAttackTargetCommand()
        {
            if (!CanHandleBattleInput()) return;
            if (_unitManager?.SelectedUnit == null) return;

            _boardManager?.ResetCellColors();
            HighlightAttackTargets();
            _inputMode = BattleInputMode.AttackTarget;
        }

        public void ExitCommandTargeting()
        {
            _inputMode = BattleInputMode.Selection;
            _boardManager?.ResetCellColors();
        }

        public void OnEndTurnClicked()
        {
            if (_unitManager != null && _unitManager.IsActionLocked) return;

            CancelSelection();
            _turnManager?.EndTurn();
        }
    }
}

