using BF.Game.Battle.Domain;
using BF.Game.Battle.Domain.Units;

namespace BF.Game.Battle.Rules.Units
{
    /// <summary>
    /// 单位规则状态的受控修改入口。
    ///
    /// 该类型只依赖纯 C# Domain，不负责 Unity 表现、动画或事件适配。
    /// 调用方应先在适配层完成目标选择和外部输入转换，再由本类型执行规则状态修改。
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
        /// 尝试开始一次规则攻击，并原子地消耗攻击 AP、进入 Attack 状态。
        /// </summary>
        /// <param name="runtimeId">攻击者运行时身份。</param>
        /// <param name="actionPointCost">本次攻击消耗的 AP。</param>
        /// <returns>true 表示攻击规则命令已经成功开始。</returns>
        public bool TryStartAttack(string runtimeId, int actionPointCost)
        {
            if (actionPointCost <= 0 || !TryGetAliveUnit(runtimeId, out var unit)) return false;
            if (unit.Attributes.RemainingActionPoints < actionPointCost) return false;

            var previousActionPoints = unit.Attributes.RemainingActionPoints;
            var previousActionState = unit.ActionState;
            unit.Attributes.SetRemainingActionPoints(previousActionPoints - actionPointCost);

            if (!unit.TryChangeActionState(BFUnit_ActionState.Attack))
            {
                unit.Attributes.SetRemainingActionPoints(previousActionPoints);
                unit.TryChangeActionState(previousActionState);
                return false;
            }

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

        /// <summary>
        /// 尝试完成一次规则移动。
        ///
        /// 位置、AP 和行动状态必须作为同一个规则命令完成；任一不变量失败时，
        /// 入口会恢复原位置、原 AP 和原行动状态，不留下部分更新。
        /// </summary>
        /// <param name="runtimeId">单位运行时身份。</param>
        /// <param name="gridPosition">移动完成后的规则网格位置。</param>
        /// <param name="actionPointCost">本次移动消耗的 AP。</param>
        /// <returns>true 表示规则移动完成。</returns>
        public bool TryCompleteMove(
            string runtimeId,
            BFGridPosition gridPosition,
            int actionPointCost)
        {
            if (actionPointCost <= 0 || !TryGetAliveUnit(runtimeId, out var unit)) return false;
            if (unit.Attributes.RemainingActionPoints < actionPointCost) return false;

            var previousPosition = unit.GridPosition;
            var previousActionPoints = unit.Attributes.RemainingActionPoints;
            var previousActionState = unit.ActionState;

            unit.Attributes.SetRemainingActionPoints(previousActionPoints - actionPointCost);
            unit.SetGridPosition(gridPosition);
            if (!unit.TryChangeActionState(BFUnit_ActionState.Idle))
            {
                unit.Attributes.SetRemainingActionPoints(previousActionPoints);
                unit.SetGridPosition(previousPosition);
                unit.TryChangeActionState(previousActionState);
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
    }
}
