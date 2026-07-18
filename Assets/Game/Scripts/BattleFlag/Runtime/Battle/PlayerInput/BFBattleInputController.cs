using BF.Game.Runtime.Battle.Managers;
using BF.Game.Runtime.Battle.Units;
using BF.Game.Runtime.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BF.Game.Runtime.Battle.PlayerInput
{
    /// <summary>
    /// 玩家输入控制器。仅解释玩家输入（选中/移动/攻击/结束回合），
    /// 调用三个 Manager 的公开合同。自身不持有核心逻辑。
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
        [SerializeField] private BFInputContextManager _inputContextManager;

        private BFInputActionSubscription _selectSubscription;
        private BFInputActionSubscription _cancelSubscription;
        private BFInputActionSubscription _endTurnSubscription;
        private InputAction _pointAction;
        private Vector2 _lastPointerPosition;
        private bool _isMoveMode;

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
        }

        private void Start()
        {
            if (_camera == null) _camera = Camera.main;

            if (_unitManager != null)
            {
                _unitManager.OnUnitMoveCompleted += UnitManager_OnUnitMoveCompleted;
            }

            if (_selectSubscription == null)
                RegisterInputActions();
        }

        private void RegisterInputActions()
        {
            _inputContextManager ??= BFInputContextManager.Instance;
            if (_inputContextManager == null) return;

            _inputContextManager.TryGetAction(BFInputActionId.BattlePoint, out _pointAction);
            _inputContextManager.TryRegisterPerformed(
                BFInputActionId.BattleSelect,
                _ => HandleClick(),
                out _selectSubscription);
            _inputContextManager.TryRegisterPerformed(
                BFInputActionId.BattleCancel,
                _ => HandleCancelInput(),
                out _cancelSubscription);
            _inputContextManager.TryRegisterPerformed(
                BFInputActionId.BattleEndTurn,
                _ => OnEndTurnClicked(),
                out _endTurnSubscription);
        }

        private void DisposeInputSubscriptions()
        {
            _selectSubscription?.Dispose();
            _cancelSubscription?.Dispose();
            _endTurnSubscription?.Dispose();
            _selectSubscription = null;
            _cancelSubscription = null;
            _endTurnSubscription = null;
            _pointAction = null;
        }

        private bool CanHandleBattleInput()
        {
            if (_turnManager == null || _unitManager == null) return false;
            if (_unitManager.IsActionLocked) return false;
            return _turnManager.CurrentPhase == BattlePhase.PlayerTurn;
        }

        private void HandleCancelInput()
        {
            if (!CanHandleBattleInput()) return;

            CancelSelection();
        }

        private void HandleClick()
        {
            if (!CanHandleBattleInput()) return;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            Vector2 screenPosition = _pointAction != null
                ? _pointAction.ReadValue<Vector2>()
                : _lastPointerPosition;
            _lastPointerPosition = screenPosition;

            Vector3 mouseWorld = _camera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, 0f));
            mouseWorld.z = 0f;

            RaycastHit2D hit = Physics2D.Raycast(mouseWorld, Vector2.zero);

            if (hit.collider == null)
            {
                if (_isMoveMode)
                    TryMoveToWorld(mouseWorld);
                else
                    CancelSelection();
                return;
            }

            var clickedUnit = hit.collider.gameObject.GetComponent<UnitRuntime>();
            if (clickedUnit != null)
                HandleUnitClick(clickedUnit);
            else if (_isMoveMode)
                TryMoveToWorld(mouseWorld);
        }

        private void HandleUnitClick(UnitRuntime unit)
        {
            if (_isMoveMode)
            {
                if (unit.Faction == UnitFaction.Enemy)
                {
                    _unitManager.TryAttack(unit);
                    _isMoveMode = false;
                    _boardManager?.ResetCellColors();
                }
                else
                {
                    SelectUnit(unit);
                }
            }
            else
            {
                SelectUnit(unit);
            }
        }

        private void SelectUnit(UnitRuntime unit)
        {
            if (unit.Faction != UnitFaction.Player || !unit.IsAlive) return;
            if (unit.HasActed) return;

            _unitManager.TrySelectUnit(unit);

            var reachable = _unitManager.GetReachableCellsForSelected();
            Debug.Log($"[Input] Selected {unit.DisplayName}, reachable: {reachable.Count}");

            _boardManager?.ResetCellColors();
            _boardManager?.HighlightCells(reachable,
                _boardManager != null ? _boardManager.ReachableColor : new Color(1f, 0.92f, 0.2f, 0.75f));
            _isMoveMode = true;
        }

        private void TryMoveToWorld(Vector3 worldPos)
        {
            if (!_isMoveMode || _unitManager.SelectedUnit == null) return;

            Vector2Int targetCell = _boardManager.WorldToCell(worldPos);
            var reachable = _unitManager.GetReachableCellsForSelected();
            if (!reachable.Contains(targetCell)) return;

            if (!_unitManager.TryMoveUnit(targetCell)) return;

            _isMoveMode = false;
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
            HighlightAttackTargets();
        }

        public void CancelSelection()
        {
            if (_unitManager != null && _unitManager.IsActionLocked) return;

            _unitManager?.DeselectUnit();
            _isMoveMode = false;
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
