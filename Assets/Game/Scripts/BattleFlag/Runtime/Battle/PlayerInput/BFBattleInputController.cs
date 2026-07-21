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
    ///
    /// 输入消费迁移为直接使用 BFInputManager.Actions 的强类型 Action，
    /// 不再通过字符串 key 查询旧输入上下文，改为直接使用 BFInputManager.Actions 强类型 Action。
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

            DisposeInputSubscriptions();
        }

        private void Start()
        {
            if (_camera == null) _camera = Camera.main;

            if (_unitManager != null)
            {
                _unitManager.OnUnitMoveCompleted += UnitManager_OnUnitMoveCompleted;
            }

            if (_selectAction == null)
                RegisterInputActions();
        }

        private void RegisterInputActions()
        {
            _inputManager ??= BFInputManager.Instance;
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
                if (unit.Identity.Faction == UnitFaction.Enemy)
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
            if (unit.Identity.Faction != UnitFaction.Player || !unit.Stats.IsAlive) return;
            if (unit.Stats.HasActed) return;

            _unitManager.TrySelectUnit(unit);

            var reachable = _unitManager.GetReachableCellsForSelected();
            Debug.Log($"[Input] Selected {unit.Identity.DisplayName}, reachable: {reachable.Count}");

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
