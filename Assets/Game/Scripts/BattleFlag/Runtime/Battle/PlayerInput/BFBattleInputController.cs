using BF.Game.Runtime.Battle.Managers;
using BF.Game.Runtime.Battle.Units;
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

        private bool _isMoveMode;

        private void Start()
        {
            if (_camera == null) _camera = Camera.main;
        }

        private void Update()
        {
            if (_turnManager == null || _unitManager == null) return;
            if (_turnManager.CurrentPhase != BattlePhase.PlayerTurn) return;

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

            _unitManager.TryMoveUnit(targetCell);
            _isMoveMode = false;
            _boardManager?.ResetCellColors();
            HighlightAttackTargets();
        }

        private void HighlightAttackTargets()
        {
            var targets = _unitManager.GetAttackableTargets();
            _boardManager?.HighlightAttackTargets(targets);
        }

        public void CancelSelection()
        {
            _unitManager?.DeselectUnit();
            _isMoveMode = false;
            _boardManager?.ResetCellColors();
        }

        public void OnEndTurnClicked()
        {
            CancelSelection();
            _turnManager?.EndTurn();
        }
    }
}
