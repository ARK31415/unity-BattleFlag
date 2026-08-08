namespace BF.Game.Battle.Rules.Units
{
    /// <summary>
    /// 攻击行动结果。正常玩法失败通过 <see cref="Succeeded" /> 与 <see cref="FailureReason" /> 表达，
    /// 不通过异常或 default 结果推断。
    /// </summary>
    public sealed class AttackResult
    {
        private AttackResult(
            bool succeeded,
            string failureReason,
            string attackerRuntimeId,
            string targetRuntimeId,
            int actionPointCost,
            int damage,
            int targetRemainingHp,
            bool targetWasKilled)
        {
            Succeeded = succeeded;
            FailureReason = failureReason;
            AttackerRuntimeId = attackerRuntimeId;
            TargetRuntimeId = targetRuntimeId;
            ActionPointCost = actionPointCost;
            Damage = damage;
            TargetRemainingHp = targetRemainingHp;
            TargetWasKilled = targetWasKilled;
        }

        /// <summary>攻击是否成功提交。</summary>
        public bool Succeeded { get; }

        /// <summary>失败原因；成功时为空字符串。</summary>
        public string FailureReason { get; }

        /// <summary>攻击者运行时身份。</summary>
        public string AttackerRuntimeId { get; }

        /// <summary>目标运行时身份。</summary>
        public string TargetRuntimeId { get; }

        /// <summary>本次攻击消耗的行动点。</summary>
        public int ActionPointCost { get; }

        /// <summary>规则结算后的最终伤害值；失败时为零。</summary>
        public int Damage { get; }

        /// <summary>结算完成后目标剩余生命值；失败时无意义。</summary>
        public int TargetRemainingHp { get; }

        /// <summary>目标是否在本次结算中被击败。</summary>
        public bool TargetWasKilled { get; }

        /// <summary>创建成功结果。</summary>
        public static AttackResult Success(
            string attackerRuntimeId,
            string targetRuntimeId,
            int actionPointCost,
            int damage,
            int targetRemainingHp,
            bool targetWasKilled)
        {
            return new AttackResult(
                true,
                string.Empty,
                attackerRuntimeId,
                targetRuntimeId,
                actionPointCost,
                damage,
                targetRemainingHp,
                targetWasKilled);
        }

        /// <summary>创建失败结果。</summary>
        public static AttackResult Failure(
            string attackerRuntimeId,
            string targetRuntimeId,
            string failureReason)
        {
            return new AttackResult(
                false,
                failureReason,
                attackerRuntimeId,
                targetRuntimeId,
                0,
                0,
                0,
                false);
        }
    }
}
