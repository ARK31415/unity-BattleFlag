namespace BF.Game.Battle.Rules.Units
{
    /// <summary>
    /// 等待行动结果。AP 为 0 时 <see cref="Succeeded" /> 为 false，不返回成功。
    /// </summary>
    public sealed class WaitResult
    {
        private WaitResult(bool succeeded, string failureReason, string runtimeId, int remainingActionPointsAfter)
        {
            Succeeded = succeeded;
            FailureReason = failureReason;
            RuntimeId = runtimeId;
            RemainingActionPointsAfter = remainingActionPointsAfter;
        }

        /// <summary>等待是否成功提交。</summary>
        public bool Succeeded { get; }

        /// <summary>失败原因；成功时为空字符串。</summary>
        public string FailureReason { get; }

        /// <summary>执行等待单位的运行时身份。</summary>
        public string RuntimeId { get; }

        /// <summary>提交完成后单位剩余行动点；成功后为 0。</summary>
        public int RemainingActionPointsAfter { get; }

        /// <summary>创建成功结果（剩余 AP 结算为 0）。</summary>
        public static WaitResult Success(string runtimeId)
        {
            return new WaitResult(true, string.Empty, runtimeId, 0);
        }

        /// <summary>创建失败结果。</summary>
        public static WaitResult Failure(string runtimeId, string failureReason)
        {
            return new WaitResult(false, failureReason, runtimeId, 0);
        }
    }
}
