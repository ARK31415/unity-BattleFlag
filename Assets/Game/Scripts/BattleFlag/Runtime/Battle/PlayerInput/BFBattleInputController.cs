using BF.Game.Runtime.Battle.Commands;
using BF.Game.Runtime.Battle.Flow;
using BF.Game.Runtime.Battle.Grid;
using BF.Game.Runtime.Battle.Units;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BF.Game.Runtime.Battle.PlayerInput
{
    [DisallowMultipleComponent]
    public class BFBattleInputController : MonoBehaviour
    {
        [SerializeField] private BattleCommandService _commandService;
        [SerializeField] private BattleStateController _stateController;
        [SerializeField] private BFGridManager _gridManager;
        [SerializeField] private Camera _camera;

        private bool _isMoveMode;

        private void Start()
        {
            if (_camera == null) _camera = Camera.main;
        }

        private void Update()
        {
            if (_stateController == null || _commandService == null) return;
            if (_stateController.CurrentPhase != BattlePhase.PlayerTurn) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame)
                HandleClick();

            if (mouse.rightButton.wasPressedThisFrame)
                CancelSelection();
        }

        private void HandleClick()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector3 mouseWorld = _camera.ScreenToWorldPoint(
                new Vector3(mouse.position.x.ReadValue(), mouse.position.y.ReadValue(), 0f));
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
                    _commandService.TryAttack(unit);
                    _isMoveMode = false;
                    _gridManager.ResetCellColors();
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

            _commandService.TrySelectUnit(unit);

            var reachable = _commandService.GetReachableCellsForSelected();
            Debug.Log($"[Input] 选中 {unit.DisplayName}, 可达格: {reachable.Count}");

            _gridManager.ResetCellColors();
            _gridManager.HighlightCells(reachable, new Color(1f, 0.92f, 0.2f, 0.75f));
            _isMoveMode = true;
        }

        private void TryMoveToWorld(Vector3 worldPos)
        {
            if (!_isMoveMode || _commandService.SelectedUnit == null) return;

            Vector2Int targetCell = _gridManager.WorldToCell(worldPos);
            var reachable = _commandService.GetReachableCellsForSelected();
            if (!reachable.Contains(targetCell)) return;

            _commandService.TryMoveUnit(targetCell);
            _isMoveMode = false;
            _gridManager.ResetCellColors();
            HighlightAttackTargets();
        }

        private void HighlightAttackTargets()
        {
            var targets = _commandService.GetAttackableTargets();
            foreach (var t in targets)
            {
                var cells = new System.Collections.Generic.List<Vector2Int> { t.GridPosition };
                _gridManager.HighlightCells(cells, new Color(1f, 0.2f, 0.2f, 0.75f));
            }
        }

        public void CancelSelection()
        {
            _commandService.DeselectUnit();
            _isMoveMode = false;
            if (_gridManager != null) _gridManager.ResetCellColors();
        }

        public void OnEndTurnClicked()
        {
            CancelSelection();
            _commandService.EndTurn();
        }
    }
}
