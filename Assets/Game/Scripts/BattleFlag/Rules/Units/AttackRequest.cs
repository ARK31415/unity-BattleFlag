using System;

namespace BF.Game.Battle.Rules.Units
{
    /// <summary>
    /// 攻击行动请求。只表达攻击者、目标和计划消耗的 AP，不把 Runtime 或动画对象作为规则事实。
    /// </summary>
    public readonly struct AttackRequest
    {
        /// <summary>
        /// 创建攻击请求。
        /// </summary>
        /// <param name="attackerRuntimeId">攻击者运行时身份。</param>
        /// <param name="targetRuntimeId">目标运行时身份。</param>
        /// <param name="actionPointCost">本次攻击计划消耗的行动点。</param>
        public AttackRequest(
            string attackerRuntimeId,
            string targetRuntimeId,
            int actionPointCost)
        {
            if (string.IsNullOrWhiteSpace(attackerRuntimeId))
                throw new ArgumentException("攻击者 RuntimeId 不能为空。", nameof(attackerRuntimeId));
            if (string.IsNullOrWhiteSpace(targetRuntimeId))
                throw new ArgumentException("目标 RuntimeId 不能为空。", nameof(targetRuntimeId));
            if (actionPointCost <= 0)
                throw new ArgumentOutOfRangeException(nameof(actionPointCost), actionPointCost, "攻击消耗必须为正数。");

            AttackerRuntimeId = attackerRuntimeId;
            TargetRuntimeId = targetRuntimeId;
            ActionPointCost = actionPointCost;
        }

        /// <summary>攻击者运行时身份。</summary>
        public string AttackerRuntimeId { get; }

        /// <summary>目标运行时身份。</summary>
        public string TargetRuntimeId { get; }

        /// <summary>本次攻击计划消耗的行动点。</summary>
        public int ActionPointCost { get; }
    }
}
