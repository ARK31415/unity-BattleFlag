using System;
using System.Collections.Generic;
using BF.Game.Battle.Domain;
using BF.Game.Battle.Domain.Units;

namespace BF.Game.Battle.Rules.Battle
{
    /// <summary>
    /// 当前战斗会话的最小棋盘规则服务。
    ///
    /// 该服务只依赖纯 C# 规则状态：静态拓扑来自战斗开始时导入的快照，
    /// 动态单位占用来自 Context 中存活单位的 GridPosition。A* 路径只是候选输入，
    /// 最终边界、阻挡、占用、连续性和移动成本必须由本服务重新验证。
    /// </summary>
    public sealed class BFBattleBoardRules : IDisposable
    {
        private static readonly BFGridPosition[] NeighborOffsets =
        {
            new BFGridPosition(1, 0),
            new BFGridPosition(-1, 0),
            new BFGridPosition(0, 1),
            new BFGridPosition(0, -1)
        };

        private readonly BFBoardTopologySnapshot _topology;
        private readonly BFBattleContext _context;
        private bool _isDisposed;

        /// <summary>创建绑定到指定战斗上下文和静态拓扑的棋盘规则服务。</summary>
        public BFBattleBoardRules(
            BFBoardTopologySnapshot topology,
            BFBattleContext context)
        {
            _topology = topology ?? throw new ArgumentNullException(nameof(topology));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>当前棋盘静态拓扑快照。</summary>
        public BFBoardTopologySnapshot Topology
        {
            get
            {
                EnsureNotDisposed();
                return _topology;
            }
        }

        /// <summary>判断规则坐标是否在棋盘边界内。</summary>
        public bool IsInBounds(BFGridPosition cell)
        {
            EnsureNotDisposed();
            return _topology.IsInBounds(cell);
        }

        /// <summary>判断规则坐标是否被静态地形阻挡。</summary>
        public bool IsStaticallyBlocked(BFGridPosition cell)
        {
            EnsureNotDisposed();
            return _topology.IsBlocked(cell);
        }

        /// <summary>判断棋盘规则服务是否绑定到指定战斗上下文实例。</summary>
        public bool IsBoundTo(BFBattleContext context)
        {
            EnsureNotDisposed();
            return ReferenceEquals(_context, context);
        }

        /// <summary>
        /// 验证单位出生位置。
        ///
        /// 该查询只读取当前 Context，因此在正式注册单位前可用于 Factory 预检；
        /// 已死亡单位不占用规则棋盘。
        /// </summary>
        public BFBoardPositionValidationResult ValidateSpawnPosition(BFGridPosition cell)
        {
            EnsureNotDisposed();
            if (!_topology.IsInBounds(cell))
                return BFBoardPositionValidationResult.Failure("出生位置超出棋盘边界。");
            if (_topology.IsBlocked(cell))
                return BFBoardPositionValidationResult.Failure("出生位置被静态阻挡。");
            if (TryGetAliveOccupant(cell, out var occupantId))
                return BFBoardPositionValidationResult.Failure(
                    $"出生位置已被存活单位 {occupantId} 占用。");

            return BFBoardPositionValidationResult.Success();
        }

        /// <summary>
        /// 验证 A* 提供的候选路径。
        ///
        /// 路径不包含起点、必须包含目标点；第一版只允许上下左右四方向，
        /// 每个路径格成本为 1，重复格、静态阻挡格、动态占用格和越界格都会失败。
        /// </summary>
        /// <param name="runtimeId">移动单位的 RuntimeId。</param>
        /// <param name="candidatePath">不包含起点、包含终点的候选路径。</param>
        public BFBoardPathValidationResult ValidateCandidatePath(
            string runtimeId,
            IReadOnlyList<BFGridPosition> candidatePath)
        {
            EnsureNotDisposed();
            if (string.IsNullOrWhiteSpace(runtimeId))
                return BFBoardPathValidationResult.Failure("移动单位 RuntimeId 不能为空。");
            if (!_context.TryGetUnit(runtimeId, out var unit) || !unit.IsAlive)
                return BFBoardPathValidationResult.Failure("移动单位不存在或已死亡。");
            if (candidatePath == null || candidatePath.Count == 0)
                return BFBoardPathValidationResult.Failure("候选路径不能为空。");

            var current = unit.GridPosition;
            if (!_topology.IsInBounds(current))
                return BFBoardPathValidationResult.Failure("候选路径起点超出棋盘边界。");
            if (_topology.IsBlocked(current))
                return BFBoardPathValidationResult.Failure("候选路径起点被静态阻挡。");
            if (TryGetAliveOccupant(current, out var startOccupantId) &&
                !string.Equals(startOccupantId, runtimeId, StringComparison.Ordinal))
            {
                return BFBoardPathValidationResult.Failure(
                    $"候选路径起点已被单位 {startOccupantId} 占用。");
            }

            var visited = new HashSet<BFGridPosition> { current };
            for (var index = 0; index < candidatePath.Count; index++)
            {
                var next = candidatePath[index];
                if (!_topology.IsInBounds(next))
                    return BFBoardPathValidationResult.Failure("候选路径包含越界格。");
                if (_topology.IsBlocked(next))
                    return BFBoardPathValidationResult.Failure("候选路径包含静态阻挡格。");
                if (!visited.Add(next))
                    return BFBoardPathValidationResult.Failure("候选路径包含重复格。");
                if (!IsAdjacent(current, next))
                    return BFBoardPathValidationResult.Failure("候选路径不是连续的四方向路径。");
                if (TryGetAliveOccupant(next, out var occupantId) &&
                    !string.Equals(occupantId, runtimeId, StringComparison.Ordinal))
                {
                    return BFBoardPathValidationResult.Failure(
                        $"候选路径包含被单位 {occupantId} 占用的格子。");
                }

                current = next;
            }

            return BFBoardPathValidationResult.Success(candidatePath.Count);
        }

        /// <summary>
        /// 验证候选路径并确认其终点与规则目标一致。
        /// </summary>
        /// <param name="runtimeId">移动单位的 RuntimeId。</param>
        /// <param name="target">规则层声明的目标格。</param>
        /// <param name="candidatePath">不包含起点、包含终点的候选路径。</param>
        public BFBoardPathValidationResult ValidateCandidatePath(
            string runtimeId,
            BFGridPosition target,
            IReadOnlyList<BFGridPosition> candidatePath)
        {
            var result = ValidateCandidatePath(runtimeId, candidatePath);
            if (!result.Succeeded) return result;
            if (candidatePath[candidatePath.Count - 1] != target)
                return BFBoardPathValidationResult.Failure("候选路径终点与规则目标不一致。");

            return result;
        }

        /// <summary>判断两个规则格是否为上下左右相邻格。</summary>
        private static bool IsAdjacent(BFGridPosition first, BFGridPosition second)
        {
            for (var index = 0; index < NeighborOffsets.Length; index++)
            {
                if (first.X + NeighborOffsets[index].X == second.X &&
                    first.Y + NeighborOffsets[index].Y == second.Y)
                    return true;
            }

            return false;
        }

        /// <summary>从 Context 推导存活单位占用，不维护第二份动态占用表。</summary>
        private bool TryGetAliveOccupant(BFGridPosition cell, out string runtimeId)
        {
            foreach (var entry in _context.Units)
            {
                var unit = entry.Value;
                if (unit != null && unit.IsAlive && unit.GridPosition == cell)
                {
                    runtimeId = unit.RuntimeId;
                    return true;
                }
            }

            runtimeId = null;
            return false;
        }

        /// <summary>释放棋盘规则服务，不影响本场 Context 中已保存的单位事实。</summary>
        public void Dispose()
        {
            _isDisposed = true;
        }

        private void EnsureNotDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(BFBattleBoardRules));
        }
    }

    /// <summary>出生位置规则校验结果。</summary>
    public sealed class BFBoardPositionValidationResult
    {
        private BFBoardPositionValidationResult(bool succeeded, string failureReason)
        {
            Succeeded = succeeded;
            FailureReason = failureReason;
        }

        /// <summary>校验是否成功。</summary>
        public bool Succeeded { get; }

        /// <summary>失败原因；成功时为空字符串。</summary>
        public string FailureReason { get; }

        /// <summary>创建成功结果。</summary>
        public static BFBoardPositionValidationResult Success()
        {
            return new BFBoardPositionValidationResult(true, string.Empty);
        }

        /// <summary>创建失败结果。</summary>
        public static BFBoardPositionValidationResult Failure(string reason)
        {
            return new BFBoardPositionValidationResult(false, reason);
        }
    }

    /// <summary>候选路径规则校验结果。</summary>
    public sealed class BFBoardPathValidationResult
    {
        private BFBoardPathValidationResult(
            bool succeeded,
            string failureReason,
            int actionPointCost)
        {
            Succeeded = succeeded;
            FailureReason = failureReason;
            ActionPointCost = actionPointCost;
        }

        /// <summary>校验是否成功。</summary>
        public bool Succeeded { get; }

        /// <summary>失败原因；成功时为空字符串。</summary>
        public string FailureReason { get; }

        /// <summary>候选路径的规则移动成本。</summary>
        public int ActionPointCost { get; }

        /// <summary>创建成功结果。</summary>
        public static BFBoardPathValidationResult Success(int actionPointCost)
        {
            return new BFBoardPathValidationResult(true, string.Empty, actionPointCost);
        }

        /// <summary>创建失败结果。</summary>
        public static BFBoardPathValidationResult Failure(string reason)
        {
            return new BFBoardPathValidationResult(false, reason, 0);
        }
    }
}
