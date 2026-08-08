using BF.Game.Battle.Domain;
using BF.Game.Battle.Domain.Units;

namespace BF.Game.Battle.Rules.Units
{
    /// <summary>
    /// 单位规则状态的受控修改入口。
    ///
    /// 该类型只依赖纯 C# Domain，不负责 Unity 表现、动画或事件适配。
    /// 行动入口统一使用强类型 Request / Result 表达成功与失败；
    /// 其余资源操作保留 bool 入口，不作为行动提交合同。
    /// </summary>
    public sealed class BFUnitStateRules
    {
        private readonly BFBattleContext _context;

        /// <summary>创建绑定到指定战斗上下文的单位规则入口。</summary>
        /// <param name="context">本场战斗的纯规则上下文。</param>
        public BFUnitStateRules(BFBattleContext context)
        {
            _context = context ?? throw new System.ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// 尝试提交一次移动。
        ///
        /// 位置、AP 和行动状态作为同一个规则命令完成；任一不变量失败时恢复原位置、
        /// 原 AP 和原行动状态，不留下部分更新。
        /// </summary>
        /// <param name="request">移动请求。</param>
        /// <returns>包含规则提交结果的移动结果。</returns>
        public MoveResult TryMove(MoveRequest request)
        {
            if (!TryGetAliveUnit(request.RuntimeId, out var unit))
                return MoveResult.Failure(request.RuntimeId, "单位不存在或已死亡。");
            if (unit.Attributes.RemainingActionPoints < request.ActionPointCost)
                return MoveResult.Failure(request.RuntimeId, "剩余行动点不足。");

            var previousPosition = unit.GridPosition;
            var previousActionPoints = unit.Attributes.RemainingActionPoints;
            var previousActionState = unit.ActionState;

            unit.Attributes.SetRemainingActionPoints(previousActionPoints - request.ActionPointCost);
            unit.SetGridPosition(request.TargetGridPosition);
            if (!unit.TryChangeActionState(BFUnit_ActionState.Idle))
            {
                unit.Attributes.SetRemainingActionPoints(previousActionPoints);
                unit.SetGridPosition(previousPosition);
                unit.TryChangeActionState(previousActionState);
                return MoveResult.Failure(request.RuntimeId, "行动状态切换失败，已回滚。");
            }

            return MoveResult.Success(
                request.RuntimeId,
                previousPosition,
                request.TargetGridPosition,
                request.ActionPointCost,
                unit.Attributes.RemainingActionPoints);
        }

        /// <summary>
        /// 尝试开始一次规则攻击。
        ///
        /// 攻击请求由规则层校验攻击者、目标、阵营、攻击范围、攻击资源和当前行动状态；
        /// 攻击开始阶段只锁定攻击行动状态，不消耗 AP，AP 消耗在 <see cref="TryResolveAttack" /> 中提交。
        /// </summary>
        /// <param name="request">攻击请求。</param>
        /// <returns>包含规则校验结果与锁定状态的攻击结果。</returns>
        public AttackResult TryStartAttack(AttackRequest request)
        {
            if (!TryGetAliveUnit(request.AttackerRuntimeId, out var attacker))
                return AttackResult.Failure(
                    request.AttackerRuntimeId, request.TargetRuntimeId, "攻击者不存在或已死亡。");
            if (!_context.TryGetUnit(request.TargetRuntimeId, out var target) || !target.IsAlive)
                return AttackResult.Failure(
                    request.AttackerRuntimeId, request.TargetRuntimeId, "目标不存在或已死亡。");
            if (attacker.Faction == target.Faction)
                return AttackResult.Failure(
                    request.AttackerRuntimeId, request.TargetRuntimeId, "不能攻击同阵营单位。");
            if (attacker.ActionState != BFUnit_ActionState.Idle)
                return AttackResult.Failure(
                    request.AttackerRuntimeId, request.TargetRuntimeId, "攻击者必须处于待机状态。");
            if (request.ActionPointCost != attacker.Attributes.EffectiveAttackCost)
                return AttackResult.Failure(
                    request.AttackerRuntimeId, request.TargetRuntimeId, "攻击消耗必须与规则攻击成本一致。");
            if (attacker.Attributes.RemainingActionPoints < request.ActionPointCost)
                return AttackResult.Failure(
                    request.AttackerRuntimeId, request.TargetRuntimeId, "剩余行动点不足。");
            if (ManhattanDistance(attacker.GridPosition, target.GridPosition) >
                attacker.Attributes.EffectiveAttackRange)
            {
                return AttackResult.Failure(
                    request.AttackerRuntimeId, request.TargetRuntimeId, "目标超出攻击范围。");
            }

            if (!attacker.TryChangeActionState(BFUnit_ActionState.Attack))
                return AttackResult.Failure(
                    request.AttackerRuntimeId, request.TargetRuntimeId, "行动状态切换失败。");

            return AttackResult.Success(
                request.AttackerRuntimeId,
                request.TargetRuntimeId,
                request.ActionPointCost,
                0,
                target.Attributes.CurrentHP,
                false);
        }

        /// <summary>
        /// 尝试提交一次命中后的攻击结算。
        ///
        /// 攻击者必须处于规则 Attack 状态；攻击者 AP 消耗、目标伤害和死亡状态作为同一个
        /// 规则命令提交，伤害值由攻击者规则攻击力决定。任一不变量失败时恢复攻击者 AP 和
        /// 目标 HP，不留下部分修改。
        /// </summary>
        /// <param name="request">攻击请求。</param>
        /// <returns>包含结算结果的攻击结果。</returns>
        public AttackResult TryResolveAttack(AttackRequest request)
        {
            if (!TryGetAliveUnit(request.AttackerRuntimeId, out var attacker))
                return AttackResult.Failure(
                    request.AttackerRuntimeId, request.TargetRuntimeId, "攻击者不存在或已死亡。");
            if (!TryGetAliveUnit(request.TargetRuntimeId, out var target))
                return AttackResult.Failure(
                    request.AttackerRuntimeId, request.TargetRuntimeId, "目标不存在或已死亡。");
            if (attacker.ActionState != BFUnit_ActionState.Attack)
                return AttackResult.Failure(
                    request.AttackerRuntimeId, request.TargetRuntimeId, "攻击者不在攻击状态。");
            if (attacker.Faction == target.Faction)
                return AttackResult.Failure(
                    request.AttackerRuntimeId, request.TargetRuntimeId, "不能攻击同阵营单位。");
            if (request.ActionPointCost != attacker.Attributes.EffectiveAttackCost)
                return AttackResult.Failure(
                    request.AttackerRuntimeId, request.TargetRuntimeId, "攻击消耗必须与规则攻击成本一致。");
            if (attacker.Attributes.RemainingActionPoints < request.ActionPointCost)
                return AttackResult.Failure(
                    request.AttackerRuntimeId, request.TargetRuntimeId, "剩余行动点不足。");
            if (ManhattanDistance(attacker.GridPosition, target.GridPosition) >
                attacker.Attributes.EffectiveAttackRange)
            {
                return AttackResult.Failure(
                    request.AttackerRuntimeId, request.TargetRuntimeId, "目标超出攻击范围。");
            }

            var damage = System.Math.Max(0, attacker.Attributes.EffectiveAttackPower);
            if (damage <= 0)
                return AttackResult.Failure(
                    request.AttackerRuntimeId, request.TargetRuntimeId, "攻击力为 0，无法造成伤害。");

            var previousAttackPoints = attacker.Attributes.RemainingActionPoints;
            var previousTargetHealth = target.Attributes.CurrentHP;

            attacker.Attributes.SetRemainingActionPoints(previousAttackPoints - request.ActionPointCost);
            target.Attributes.SetCurrentHP(System.Math.Max(0, previousTargetHealth - damage));
            var wasKilled = !target.IsAlive;

            if (wasKilled && !target.TryChangeActionState(BFUnit_ActionState.Dead))
            {
                attacker.Attributes.SetRemainingActionPoints(previousAttackPoints);
                target.Attributes.SetCurrentHP(previousTargetHealth);
                return AttackResult.Failure(
                    request.AttackerRuntimeId, request.TargetRuntimeId, "死亡状态切换失败，已回滚。");
            }

            return AttackResult.Success(
                request.AttackerRuntimeId,
                request.TargetRuntimeId,
                request.ActionPointCost,
                damage,
                target.Attributes.CurrentHP,
                wasKilled);
        }

        /// <summary>
        /// 尝试提交一次等待。
        ///
        /// 只有剩余 AP 大于 0 时允许等待；成功后将剩余 AP 结算为 0 并结束当前单位行动。
        /// 剩余 AP 为 0 时返回失败，不能返回成功。
        /// </summary>
        /// <param name="request">等待请求。</param>
        /// <returns>等待结果。</returns>
        public WaitResult TryWait(WaitRequest request)
        {
            if (!TryGetAliveUnit(request.RuntimeId, out var unit))
                return WaitResult.Failure(request.RuntimeId, "单位不存在或已死亡。");
            if (unit.Attributes.RemainingActionPoints <= 0)
                return WaitResult.Failure(request.RuntimeId, "剩余行动点为 0，不能等待。");

            unit.Attributes.SetRemainingActionPoints(0);
            return WaitResult.Success(request.RuntimeId);
        }

        /// <summary>
        /// 尝试消耗单位当前回合的行动点。
        ///
        /// 只有存活单位可以消耗行动点，消耗失败时不会修改原值。
        /// </summary>
        /// <param name="runtimeId">当前战斗内的单位运行时身份。</param>
        /// <param name="amount">要消耗的行动点数量。</param>
        /// <returns>true 表示消耗成功。</returns>
        public bool TryConsumeActionPoints(string runtimeId, int amount)
        {
            if (amount <= 0 || !TryGetAliveUnit(runtimeId, out var unit)) return false;
            if (unit.Attributes.RemainingActionPoints < amount) return false;

            unit.Attributes.SetRemainingActionPoints(
                unit.Attributes.RemainingActionPoints - amount);
            return true;
        }

        /// <summary>
        /// 尝试重置单位本回合行动点。
        ///
        /// 死亡单位不会重新获得行动点。
        /// </summary>
        /// <param name="runtimeId">当前战斗内的单位运行时身份。</param>
        /// <returns>true 表示重置成功。</returns>
        public bool TryResetTurnResources(string runtimeId)
        {
            if (!TryGetAliveUnit(runtimeId, out var unit)) return false;

            unit.Attributes.SetRemainingActionPoints(unit.Attributes.EffectiveMaxActionPoints);
            return true;
        }

        /// <summary>
        /// 尝试应用一次直接伤害。
        ///
        /// 伤害使生命值归零时，同步进入不可逆的规则 Dead 状态。
        /// </summary>
        /// <param name="runtimeId">受伤单位的运行时身份。</param>
        /// <param name="damage">正数伤害值。</param>
        /// <param name="wasKilled">输出本次伤害是否造成死亡。</param>
        /// <returns>true 表示本次实际应用了正数伤害。</returns>
        public bool TryApplyDamage(string runtimeId, int damage, out bool wasKilled)
        {
            wasKilled = false;
            if (damage <= 0 || !TryGetAliveUnit(runtimeId, out var unit)) return false;

            var previousHealth = unit.Attributes.CurrentHP;
            int nextHealth = System.Math.Max(0, unit.Attributes.CurrentHP - damage);
            unit.Attributes.SetCurrentHP(nextHealth);
            wasKilled = !unit.IsAlive;

            if (wasKilled && !unit.TryChangeActionState(BFUnit_ActionState.Dead))
            {
                // 保证规则入口具有原子性：状态切换失败时回滚此前的 HP 写入。
                unit.Attributes.SetCurrentHP(previousHealth);
                wasKilled = false;
                return false;
            }

            return true;
        }

        /// <summary>尝试更新单位的规则网格位置。</summary>
        /// <param name="runtimeId">单位运行时身份。</param>
        /// <param name="gridPosition">已经由调用方完成合法性检查的目标规则位置。</param>
        /// <returns>true 表示位置更新成功。</returns>
        public bool TrySetGridPosition(string runtimeId, BFGridPosition gridPosition)
        {
            if (!TryGetAliveUnit(runtimeId, out var unit)) return false;

            unit.SetGridPosition(gridPosition);
            return true;
        }

        /// <summary>尝试切换单位规则行动状态。</summary>
        /// <param name="runtimeId">单位运行时身份。</param>
        /// <param name="nextState">目标规则行动状态。</param>
        /// <returns>true 表示状态切换成功。</returns>
        public bool TryChangeActionState(string runtimeId, BFUnit_ActionState nextState)
        {
            return _context.TryGetUnit(runtimeId, out var unit)
                   && unit.TryChangeActionState(nextState);
        }

        private bool TryGetAliveUnit(string runtimeId, out BFUnitState unit)
        {
            if (!_context.TryGetUnit(runtimeId, out unit) || !unit.IsAlive)
            {
                unit = null;
                return false;
            }

            return true;
        }

        private static int ManhattanDistance(BFGridPosition first, BFGridPosition second)
        {
            return System.Math.Abs(first.X - second.X) + System.Math.Abs(first.Y - second.Y);
        }
    }
}
