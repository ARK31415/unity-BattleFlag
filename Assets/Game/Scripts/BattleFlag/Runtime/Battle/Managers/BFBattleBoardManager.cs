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

        private GridGraph _grid;
        private readonly List<GameObject> _cellVisuals = new();
        private static readonly Vector2Int[] NeighborOffsets =
        {
            new(1, 0),
            new(-1, 0),
            new(0, 1),
            new(0, -1)
        };

        public GridGraph Grid => _grid;
        public int Width => _grid?.Width ?? 0;
        public int Height => _grid?.Depth ?? 0;

        // ============================================================
        // 初始化
        // ============================================================

        private void Awake()
        {
            var astar = AstarPath.active;
            if (astar == null)
            {
                Debug.LogError("[BFBattleBoardManager] Scene is missing an AstarPath component.");
                enabled = false;
                return;
            }

            _grid = astar.data.gridGraph;
            if (_grid == null)
            {
                Debug.LogError("[BFBattleBoardManager] Scene AstarPath has no GridGraph.");
                enabled = false;
                return;
            }
        }

        private void Start()
        {
            if (!EnsureGridReady()) return;

            Debug.Log($"[BFBattleBoardManager] Using scene A*: {_grid.Width}x{_grid.Depth}");

            GenerateVisuals();
            Debug.Log($"[BFBattleBoardManager] Ready: {Width}x{Height}");
        }

        /// <summary>
        /// 将场景中的单位对齐到棋盘格子上（由 BFBattleRoot 在初始化流程中调用）。
        /// </summary>
        public void SnapUnitsToGrid(List<UnitRuntime> units)
        {
            if (!EnsureGridReady()) return;

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

        private bool EnsureGridReady()
        {
            if (_grid == null)
            {
                var astar = AstarPath.active;
                if (astar != null && astar.data != null)
                {
                    _grid = astar.data.gridGraph;
                }
            }

            if (_grid == null)
            {
                Debug.LogError("[BFBattleBoardManager] GridGraph not available after Start.");
                enabled = false;
                return false;
            }

            if (!_grid.isScanned)
            {
                AstarPath.active.Scan();
            }

            if (_grid.isScanned) return true;

            Debug.LogError("[BFBattleBoardManager] Scene GridGraph could not be scanned.");
            enabled = false;
            return false;
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
            if (range <= 0 || !IsCellInBounds(pos)) return cells;

            for (int dx = -range; dx <= range; dx++)
            {
                for (int dy = -range; dy <= range; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) > range) continue;

                    var cell = new Vector2Int(pos.x + dx, pos.y + dy);
                    if (!IsCellInBounds(cell) || IsCellOccupied(cell)) continue;

                    var path = FindPath(pos, cell, uid);
                    if (path.Count > 0 && path.Count <= range)
                    {
                        cells.Add(cell);
                    }
                }
            }
            return cells;
        }

        public bool IsCellReachable(Vector2Int from, Vector2Int to, int range, string uid)
        {
            if (range <= 0 || from == to) return false;
            if (!IsCellInBounds(from) || !IsCellInBounds(to)) return false;
            if (IsCellOccupied(to)) return false;

            var path = FindPath(from, to, uid);
            return path.Count > 0 && path.Count <= range;
        }

        // ============================================================
        // A* 寻路
        // ============================================================

        public List<Vector2Int> FindPath(Vector2Int from, Vector2Int to, string uid)
        {
            var result = new List<Vector2Int>();
            if (_grid == null) return result;
            if (!IsCellInBounds(from) || !IsCellInBounds(to)) return result;
            if (from == to || IsCellOccupied(to)) return result;

            var open = new List<Vector2Int> { from };
            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var gScore = new Dictionary<Vector2Int, int>
            {
                [from] = 0
            };

            while (open.Count > 0)
            {
                var current = GetLowestScoreCell(open, gScore, to);
                if (current == to)
                {
                    return ReconstructPath(cameFrom, current, from);
                }

                open.Remove(current);

                foreach (var offset in NeighborOffsets)
                {
                    var neighbor = current + offset;
                    if (!CanTraverseCell(neighbor, from)) continue;

                    int tentativeScore = gScore[current] + 1;
                    if (gScore.TryGetValue(neighbor, out int knownScore) && tentativeScore >= knownScore)
                        continue;

                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeScore;
                    if (!open.Contains(neighbor))
                    {
                        open.Add(neighbor);
                    }
                }
            }

            return result;
        }

        private bool CanTraverseCell(Vector2Int cell, Vector2Int startCell)
        {
            if (!IsCellInBounds(cell)) return false;
            if (cell == startCell) return true;

            var node = _grid.GetNode(cell.x, cell.y);
            return node != null && node.Walkable;
        }

        private static Vector2Int GetLowestScoreCell(List<Vector2Int> open, Dictionary<Vector2Int, int> gScore, Vector2Int target)
        {
            var best = open[0];
            int bestScore = GetEstimatedTotalScore(best, gScore, target);

            for (int i = 1; i < open.Count; i++)
            {
                var candidate = open[i];
                int score = GetEstimatedTotalScore(candidate, gScore, target);
                if (score >= bestScore) continue;

                best = candidate;
                bestScore = score;
            }

            return best;
        }

        private static int GetEstimatedTotalScore(Vector2Int cell, Dictionary<Vector2Int, int> gScore, Vector2Int target)
        {
            return gScore[cell] + Mathf.Abs(target.x - cell.x) + Mathf.Abs(target.y - cell.y);
        }

        private static List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current, Vector2Int start)
        {
            var path = new List<Vector2Int> { current };
            while (cameFrom.TryGetValue(current, out var previous))
            {
                current = previous;
                if (current == start) break;
                path.Add(current);
            }

            path.Reverse();
            return path;
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
