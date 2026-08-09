using System;
using System.Collections.Generic;
using BF.Game.Battle.Domain.Units;

namespace BF.Game.Battle.Domain
{
    /// <summary>
    /// 战斗开始时从适配层导入的棋盘静态拓扑快照。
    ///
    /// 该类型只保存规则需要的宽度、高度和静态阻挡格，不保存 Unity、A* 节点、
    /// Transform 或动态单位占用。动态占用由 BFBattleContext 中的存活单位状态推导。
    /// </summary>
    public sealed class BFBoardTopologySnapshot
    {
        private readonly HashSet<BFGridPosition> _blockedCellSet;
        private readonly BFGridPosition[] _blockedCells;
        private readonly IReadOnlyCollection<BFGridPosition> _blockedCellsView;

        /// <summary>
        /// 创建一个棋盘静态拓扑快照。
        /// </summary>
        /// <param name="width">棋盘横向格数。</param>
        /// <param name="height">棋盘纵向格数。</param>
        /// <param name="blockedCells">静态阻挡格集合。</param>
        public BFBoardTopologySnapshot(
            int width,
            int height,
            IEnumerable<BFGridPosition> blockedCells)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), width, "棋盘宽度必须大于 0。");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), height, "棋盘高度必须大于 0。");

            Width = width;
            Height = height;
            _blockedCellSet = new HashSet<BFGridPosition>();

            if (blockedCells != null)
            {
                foreach (var cell in blockedCells)
                {
                    if (!IsInBounds(cell))
                        throw new ArgumentOutOfRangeException(
                            nameof(blockedCells),
                            cell,
                            "静态阻挡格必须位于棋盘边界内。");

                    _blockedCellSet.Add(cell);
                }
            }

            _blockedCells = new BFGridPosition[_blockedCellSet.Count];
            _blockedCellSet.CopyTo(_blockedCells);
            _blockedCellsView = Array.AsReadOnly(_blockedCells);
        }

        /// <summary>棋盘横向格数。</summary>
        public int Width { get; }

        /// <summary>棋盘纵向格数。</summary>
        public int Height { get; }

        /// <summary>静态阻挡格的只读副本。</summary>
        public IReadOnlyCollection<BFGridPosition> BlockedCells => _blockedCellsView;

        /// <summary>判断规则坐标是否在棋盘边界内。</summary>
        public bool IsInBounds(BFGridPosition cell)
        {
            return cell.X >= 0 && cell.X < Width && cell.Y >= 0 && cell.Y < Height;
        }

        /// <summary>判断规则坐标是否为静态阻挡格。</summary>
        public bool IsBlocked(BFGridPosition cell)
        {
            return _blockedCellSet.Contains(cell);
        }
    }
}
