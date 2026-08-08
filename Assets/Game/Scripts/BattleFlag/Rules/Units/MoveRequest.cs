using System;
using BF.Game.Battle.Domain.Units;

namespace BF.Game.Battle.Rules.Units
{
    /// <summary>
    /// 移动行动请求。只表达规则身份、目标规则位置和计划消耗的 AP，不接收 Runtime 对象。
    /// </summary>
    public readonly struct MoveRequest
    {
        /// <summary>
        /// 创建移动请求。
        /// </summary>
        /// <param name="runtimeId">移动单位的运行时身份。</param>
        /// <param name="targetGridPosition">目标规则网格位置。</param>
        /// <param name="actionPointCost">本次移动计划消耗的行动点。</param>
        public MoveRequest(string runtimeId, BFGridPosition targetGridPosition, int actionPointCost)
        {
            if (string.IsNullOrWhiteSpace(runtimeId))
                throw new ArgumentException("RuntimeId 不能为空。", nameof(runtimeId));
            if (actionPointCost <= 0)
                throw new ArgumentOutOfRangeException(nameof(actionPointCost), actionPointCost, "移动消耗必须为正数。");

            RuntimeId = runtimeId;
            TargetGridPosition = targetGridPosition;
            ActionPointCost = actionPointCost;
        }

        /// <summary>移动单位的运行时身份。</summary>
        public string RuntimeId { get; }

        /// <summary>目标规则网格位置。</summary>
        public BFGridPosition TargetGridPosition { get; }

        /// <summary>本次移动计划消耗的行动点。</summary>
        public int ActionPointCost { get; }
    }
}
