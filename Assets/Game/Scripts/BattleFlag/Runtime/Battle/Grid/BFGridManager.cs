using System.Collections.Generic;
using BF.Game.Runtime.Battle.Units;
using Pathfinding;
using UnityEngine;

namespace BF.Game.Runtime.Battle.Grid
{
    public class BFGridManager : MonoBehaviour, IMovementHandler
    {
        [SerializeField] private GameObject _cellPrefab;
        private static readonly Color LightCell = new(0.85f, 0.85f, 0.85f);
        private static readonly Color DarkCell = Color.white;

        private GridGraph _grid;
        private Seeker _seeker;
        private readonly List<GameObject> _cellVisuals = new();

        public GridGraph Grid => _grid;
        public int Width => _grid != null ? _grid.Width : 10;
        public int Height => _grid != null ? _grid.Depth : 8;

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
                Debug.Log($"[BFGridManager] Created A*: {_grid.Width}x{_grid.Depth}");
            }
            else
            {
                bool rescan = ConfigGrid();
                if (rescan)
                {
                    astar.Scan();
                    Debug.Log($"[BFGridManager] Reconfigured and rescanned A*: {_grid.Width}x{_grid.Depth}");
                }
                else
                {
                    Debug.Log($"[BFGridManager] Reusing A*: {_grid.Width}x{_grid.Depth}");
                }
            }

            _seeker = GetComponent<Seeker>();
            if (_seeker == null) _seeker = gameObject.AddComponent<Seeker>();
        }

        private bool ConfigGrid()
        {
            bool changed = false;
            if (_grid.Width != 10 || _grid.Depth != 8)
            {
                _grid.SetDimensions(10, 8, 1f);
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

        private void Start()
        {
            GenerateVisuals();

            var units = FindObjectsByType<UnitRuntime>(FindObjectsSortMode.None);
            foreach (var unit in units)
            {
                Vector2Int cell = WorldToCell(unit.transform.position);
                unit.GridPosition = cell;
                unit.transform.position = (Vector3)CellToWorld(cell);
                unit.MovementHandler = this;
                OccupyCell(cell, unit.UnitId);
            }

            Debug.Log($"[BFGridManager] Ready: {units.Length} units, A* {Width}x{Height}");
        }

        public Vector2 CellToWorld(Vector2Int cell)
        {
            if (_grid == null || cell.x < 0 || cell.x >= Width || cell.y < 0 || cell.y >= Height) return Vector2.zero;

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

        public bool IsCellInBounds(Vector2Int cell) => cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;

        public bool IsCellOccupied(Vector2Int cell)
        {
            if (_grid == null || !IsCellInBounds(cell)) return false;

            var node = _grid.GetNode(cell.x, cell.y);
            return node != null && !node.Walkable;
        }

        public string GetOccupant(Vector2Int cell) => IsCellOccupied(cell) ? "occupied" : null;

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
                    if (sr) sr.color = (x + y) % 2 == 1 ? LightCell : DarkCell;

                    _cellVisuals.Add(go);
                }
            }

            Debug.Log($"[BFGridManager] Visualized {Width}x{Height}");
        }

        private int GetVisualIndex(Vector2Int cell) => cell.x * Height + cell.y;

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

        public void ResetCellColors()
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    int index = GetVisualIndex(new Vector2Int(x, y));
                    if (index >= _cellVisuals.Count) continue;

                    var sr = _cellVisuals[index].GetComponent<SpriteRenderer>();
                    if (sr) sr.color = (x + y) % 2 == 1 ? LightCell : DarkCell;
                }
            }
        }
    }
}
