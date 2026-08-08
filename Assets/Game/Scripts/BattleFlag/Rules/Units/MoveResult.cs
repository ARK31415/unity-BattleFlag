using BF.Game.Battle.Domain.Units;

namespace BF.Game.Battle.Rules.Units
{
    /// <summary>
    /// 移动行动结果。正常玩法失败通过 <see cref="Succeeded" /> 与 <see cref="FailureReason" /> 表达，
    /// 不通过异常或 default 结果推断。
    /// </summary>
    public sealed class MoveResult
    {
        private MoveResult(
            bool succeeded,
            string failureReason,
            string runtimeId,
            BFGridPosition? fromGridPosition,
            BFGridPosition? toGridPosition,
            int actionPointCost,
            int remainingActionPoints)
        {
            Succeeded = succeeded;
            FailureReason = failureReason;
            RuntimeId = runtimeId;
            FromGridPosition = fromGridPosition;
            ToGridPosition = toGridPosition;
            ActionPointCost = actionPointCost;
            RemainingActionPoints = remainingActionPoints;
        }

        /// <summary>移动是否成功提交。</summary>
        public bool Succeeded { get; }

        /// <summary>失败原因；成功时为空字符串。</summary>
        public string FailureReason { get; }

        /// <summary>移动单位的运行时身份。</summary>
        public string RuntimeId { get; }

        /// <summary>提交前的规则位置；失败时无意义。</summary>
        public BFGridPosition? FromGridPosition { get; }

        /// <summary>提交后的规则位置；失败时无意义。</summary>
        public BFGridPosition? ToGridPosition { get; }

        /// <summary>本次移动消耗的行动点。</summary>
        public int ActionPointCost { get; }

        /// <summary>提交完成后单位剩余行动点。</summary>
        public int RemainingActionPoints { get; }

        /// <summary>创建成功结果。</summary>
        public static MoveResult Success(
            string runtimeId,
            BFGridPosition fromGridPosition,
            BFGridPosition toGridPosition,
            int actionPointCost,
            int remainingActionPoints)
        {
            return new MoveResult(
                true,
                string.Empty,
                runtimeId,
                fromGridPosition,
                toGridPosition,
                actionPointCost,
                remainingActionPoints);
        }

        /// <summary>创建失败结果。</summary>
        public static MoveResult Failure(string runtimeId, string failureReason)
        {
            return new MoveResult(false, failureReason, runtimeId, null, null, 0, 0);
        }
    }
}
