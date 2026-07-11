using System.Collections.Generic;
using BF.Game.Runtime.Battle.Units;
using Pathfinding;
using UnityEngine;

namespace BF.Game.Runtime.Battle.Managers
{
    /// <summary>
    /// 棋盘管理器。管理 10×8 A* GridGraph、格子占用、可达格/攻击范围查询、
    /// 格子高亮颜色（Spec 第 5 节：默认半透明、可达黄色、攻击红色）。
    /// 实现 IMovementHandler 接口。
    /// 不负责谁行动、胜负判定。
    /// </summary>
    public class BFBattleBoardManager : MonoBehaviour, IMovementHandler
    {
        [Header("Cell Visuals")]
        [SerializeField] private GameObject _cellPrefab;

        [Header("Cell Colors (Spec §5)")]
        [SerializeField] private Color _defaultColorA = new Color(0.85f, 0.85f, 0.85f);
        [SerializeField] private Color _defaultColorB = Color.white;
        [SerializeField] private Color _reachableColor = new Color(1f, 0.92f, 0.2f, 0.75f);
        [SerializeField] private Color _attackRangeColor = new Color(1f, 0.2f, 0.2f, 0.75f);

        public const int DefaultWidth = 10;
        public const int DefaultHeight = 8;

        private GridGraph _grid;
        private Seeker _seeker;
        private readonly List<GameObject> _cellVisuals = new();

        public GridGraph Grid => _grid;
        public int Width => _grid != null ? _grid.Width : DefaultWidth;
        public int Height => _grid != null ? _grid.Depth : DefaultHeight;

        // ============================================================
        // 初始化
        // ============================================================

        private void Awake()
        {
            var astar = AstarPath.active;
            if (astar == null)
            {
                astar = gameObject.AddComponent<AstarPath>();
                astar.logPathResults = PathLog.OnlyErrors;
            }

            _grid = astar.data.gridGraph;
            if (_grid == null)
            {
                _grid = astar.data.AddGraph(typeof(GridGraph)) as GridGraph;
                ConfigGrid();
                astar.Scan();
                Debug.Log($"[BFBattleBoardManager] Created A*: {_grid.Width}x{_grid.Depth}");
            }
            else
            {
                bool rescan = ConfigGrid();
                if (rescan)
                {
                    astar.Scan();
                    Debug.Log($"[BFBattleBoardManager] Reconfigured A*: {_grid.Width}x{_grid.Depth}");
                }
                else
                {
                    Debug.Log($"[BFBattleBoardManager] Reusing A*: {_grid.Width}x{_grid.Depth}");
                }
            }

            _seeker = GetComponent<Seeker>();
            if (_seeker == null) _seeker = gameObject.AddComponent<Seeker>();
        }

        private void Start()
        {
            GenerateVisuals();
            Debug.Log($"[BFBattleBoardManager] Ready: {Width}x{Height}");
        }

        /// <summary>
        /// 将场景中的单位对齐到棋盘格子上（由 BFBattleRoot 在初始化流程中调用）。
        /// </summary>
        public void SnapUnitsToGrid(List<UnitRuntime> units)
        {
            foreach (var unit in units)
            {
                Vector2Int cell = WorldToCell(unit.transform.position);
                unit.GridPosition = cell;
                unit.transform.position = (Vector3)CellToWorld(cell);
                unit.MovementHandler = this;
                OccupyCell(cell, unit.UnitId);
            }
            Debug.Log($"[BFBattleBoardManager] Snapped {units.Count} units to grid");
        }

        // ============================================================
        // A* 配置
        // ============================================================

        private bool ConfigGrid()
        {
            bool changed = false;
            if (_grid.Width != DefaultWidth || _grid.Depth != DefaultHeight)
            {
                _grid.SetDimensions(DefaultWidth, DefaultHeight, 1f);
                changed = true;
            }

            if (!_grid.is2D)
            {
                _grid.is2D = true;
                changed = true;
            }

            if (!_grid.collision.use2D)
            {
                _grid.collision.use2D = true;
                changed = true;
            }

            if (_grid.neighbours != NumNeighbours.Four)
            {
                _grid.neighbours = NumNeighbours.Four;
                changed = true;
            }

            if (_grid.cutCorners)
            {
                _grid.cutCorners = false;
                changed = true;
            }

            var targetCenter = new Vector3(5f, 4f, 0f);
            if (_grid.center != targetCenter)
            {
                _grid.center = targetCenter;
                changed = true;
            }

            return changed;
        }

        // ============================================================
        // 坐标转换
        // ============================================================

        public Vector2 CellToWorld(Vector2Int cell)
        {
            if (_grid == null || cell.x < 0 || cell.x >= Width || cell.y < 0 || cell.y >= Height)
                return Vector2.zero;

            var node = _grid.GetNode(cell.x, cell.y);
            return node != null ? (Vector2)(Vector3)node.position : Vector2.zero;
        }

        public Vector2Int WorldToCell(Vector3 worldPos)
        {
            if (_grid == null) return Vector2Int.zero;

            var nearest = _grid.GetNearest(worldPos);
            return nearest.node is GridNodeBase gridNode
                ? new Vector2Int(gridNode.XCoordinateInGrid, gridNode.ZCoordinateInGrid)
                : Vector2Int.zero;
        }

        // ============================================================
        // 格子查询
        // ============================================================

        public bool IsCellInBounds(Vector2Int cell)
            => cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;

        public bool IsCellOccupied(Vector2Int cell)
        {
            if (_grid == null || !IsCellInBounds(cell)) return false;
            var node = _grid.GetNode(cell.x, cell.y);
            return node != null && !node.Walkable;
        }

        public string GetOccupant(Vector2Int cell)
            => IsCellOccupied(cell) ? "occupied" : null;

        // ============================================================
        // 占用管理（IMovementHandler）
        // ============================================================

        public void OccupyCell(Vector2Int cell, string uid)
        {
            if (_grid == null || !IsCellInBounds(cell)) return;
            var node = _grid.GetNode(cell.x, cell.y);
            if (node != null) node.Walkable = false;
        }

        public void ReleaseCell(Vector2Int cell, string uid)
        {
            if (_grid == null || !IsCellInBounds(cell)) return;
            var node = _grid.GetNode(cell.x, cell.y);
            if (node != null) node.Walkable = true;
        }

        // ============================================================
        // 可达格（Manhattan 距离，洪水填充）
        // ============================================================

        public List<Vector2Int> GetReachableCells(Vector2Int pos, int range, string uid)
        {
            var cells = new List<Vector2Int>();
            for (int dx = -range; dx <= range; dx++)
            {
                for (int dy = -range; dy <= range; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) > range) continue;

                    var cell = new Vector2Int(pos.x + dx, pos.y + dy);
                    if (!IsCellInBounds(cell) || IsCellOccupied(cell)) continue;

                    cells.Add(cell);
                }
            }
            return cells;
        }

        public bool IsCellReachable(Vector2Int from, Vector2Int to, int range, string uid)
            => Mathf.Abs(to.x - from.x) + Mathf.Abs(to.y - from.y) <= range
               && IsCellInBounds(to)
               && !IsCellOccupied(to);

        // ============================================================
        // A* 寻路
        // ============================================================

        public List<Vector2Int> FindPath(Vector2Int from, Vector2Int to, string uid)
        {
            var result = new List<Vector2Int>();
            if (_seeker == null || _grid == null) return result;

            var fromNode = _grid.GetNode(from.x, from.y);
            var toNode = _grid.GetNode(to.x, to.y);
            bool fromWalkable = fromNode != null && fromNode.Walkable;
            bool toWalkable = toNode != null && toNode.Walkable;

            if (fromNode != null) fromNode.Walkable = true;
            if (toNode != null) toNode.Walkable = true;

            var path = _seeker.StartPath((Vector3)CellToWorld(from), (Vector3)CellToWorld(to));
            path.BlockUntilCalculated();

            if (fromNode != null) fromNode.Walkable = fromWalkable;
            if (toNode != null) toNode.Walkable = toWalkable;

            if (!path.error && path.vectorPath != null)
            {
                foreach (var waypoint in path.vectorPath)
                {
                    result.Add(WorldToCell(waypoint));
                }
                if (result.Count > 0 && result[0] == from) result.RemoveAt(0);
            }

            return result;
        }

        // ============================================================
        // 可视化
        // ============================================================

        private void GenerateVisuals()
        {
            _cellVisuals.Clear();

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    var pos = CellToWorld(new Vector2Int(x, y));
                    if (_cellPrefab == null) continue;

                    var go = Instantiate(_cellPrefab, pos, Quaternion.identity, transform);
                    go.name = $"Cell_{x}_{y}";

                    var sr = go.GetComponent<SpriteRenderer>();
                    if (sr) sr.color = (x + y) % 2 == 1 ? _defaultColorA : _defaultColorB;

                    _cellVisuals.Add(go);
                }
            }

            Debug.Log($"[BFBattleBoardManager] Visualized {Width}x{Height}");
        }

        private int GetVisualIndex(Vector2Int cell)
            => cell.x * Height + cell.y;

        /// <summary>高亮指定格子列表。</summary>
        public void HighlightCells(List<Vector2Int> cells, Color color)
        {
            foreach (var cell in cells)
            {
                int index = GetVisualIndex(cell);
                if (index < 0 || index >= _cellVisuals.Count) continue;

                var sr = _cellVisuals[index].GetComponent<SpriteRenderer>();
                if (sr) sr.color = color;
            }
        }

        /// <summary>高亮可攻击目标的格子（红色）。</summary>
        public void HighlightAttackTargets(List<UnitRuntime> targets)
        {
            foreach (var t in targets)
            {
                int index = GetVisualIndex(t.GridPosition);
                if (index < 0 || index >= _cellVisuals.Count) continue;

                var sr = _cellVisuals[index].GetComponent<SpriteRenderer>();
                if (sr) sr.color = _attackRangeColor;
            }
        }

        /// <summary>重置所有格子颜色为棋盘默认。</summary>
        public void ResetCellColors()
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    int index = GetVisualIndex(new Vector2Int(x, y));
                    if (index >= _cellVisuals.Count) continue;

                    var sr = _cellVisuals[index].GetComponent<SpriteRenderer>();
                    if (sr) sr.color = (x + y) % 2 == 1 ? _defaultColorA : _defaultColorB;
                }
            }
        }

        /// <summary>公开可达格高亮颜色供外部参考。</summary>
        public Color ReachableColor => _reachableColor;

        /// <summary>公开攻击范围高亮颜色供外部参考。</summary>
        public Color AttackRangeColor => _attackRangeColor;
    }
}
