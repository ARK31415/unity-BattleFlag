using BF.Game.Battle.Domain;
using BF.Game.Battle.Domain.Events;
using BF.Game.Battle.Domain.Units;
using BF.Game.Battle.Rules.Units;
using NUnit.Framework;

namespace BF.Game.Tests.EditMode.Rules
{
    /// <summary>
    /// 验证单位规则入口只修改 Domain 状态，并维护 HP、AP、位置和死亡不变量；
    /// 行动入口使用强类型 Request / Result 表达成功与失败。
    /// </summary>
    public sealed class BFUnitStateRulesTests
    {
        [Test]
        public void ConsumeActionPointsUpdatesDomainState()
        {
            var context = CreateContext(out var state);
            var rules = new BFUnitStateRules(context);

            Assert.That(rules.TryConsumeActionPoints(state.RuntimeId, 2), Is.True);
            Assert.That(state.Attributes.RemainingActionPoints, Is.EqualTo(3));
        }

        [Test]
        public void ConsumeActionPointsRejectsInsufficientResourceWithoutMutation()
        {
            var context = CreateContext(out var state);
            var rules = new BFUnitStateRules(context);

            Assert.That(rules.TryConsumeActionPoints(state.RuntimeId, 6), Is.False);
            Assert.That(state.Attributes.RemainingActionPoints, Is.EqualTo(5));
        }

        [Test]
        public void DamageUpdatesHealthAndEntersTerminalDeadState()
        {
            var context = CreateContext(out var state, currentHP: 10);
            var rules = new BFUnitStateRules(context);

            Assert.That(rules.TryApplyDamage(state.RuntimeId, 10, out var wasKilled), Is.True);
            Assert.That(wasKilled, Is.True);
            Assert.That(state.Attributes.CurrentHP, Is.Zero);
            Assert.That(state.ActionState, Is.EqualTo(BFUnit_ActionState.Dead));
            Assert.That(rules.TryChangeActionState(state.RuntimeId, BFUnit_ActionState.Idle), Is.False);
        }

        [Test]
        public void DeadUnitCannotConsumeOrResetActionPoints()
        {
            var context = CreateContext(out var state, currentHP: 0);
            state.TryChangeActionState(BFUnit_ActionState.Dead);
            var rules = new BFUnitStateRules(context);

            Assert.That(rules.TryConsumeActionPoints(state.RuntimeId, 1), Is.False);
            Assert.That(rules.TryResetTurnResources(state.RuntimeId), Is.False);
            Assert.That(state.Attributes.RemainingActionPoints, Is.EqualTo(5));
        }

        [Test]
        public void SetGridPositionUpdatesOnlyDomainState()
        {
            var context = CreateContext(out var state);
            var rules = new BFUnitStateRules(context);

            Assert.That(
                rules.TrySetGridPosition(state.RuntimeId, new BFGridPosition(4, 7)),
                Is.True);
            Assert.That(state.GridPosition, Is.EqualTo(new BFGridPosition(4, 7)));
        }

        [Test]
        public void CompleteMoveUpdatesPositionAndActionPointsAsOneRuleCommand()
        {
            var context = CreateContext(out var state);
            var rules = new BFUnitStateRules(context);

            Assert.That(
                rules.TryChangeActionState(state.RuntimeId, BFUnit_ActionState.Move),
                Is.True);

            var result = rules.TryMove(new MoveRequest(state.RuntimeId, new BFGridPosition(4, 7), 2));

            Assert.That(result.Succeeded, Is.True, result.FailureReason);
            Assert.That(state.GridPosition, Is.EqualTo(new BFGridPosition(4, 7)));
            Assert.That(state.Attributes.RemainingActionPoints, Is.EqualTo(3));
            Assert.That(state.ActionState, Is.EqualTo(BFUnit_ActionState.Idle));
            Assert.That(result.FromGridPosition, Is.EqualTo(new BFGridPosition(1, 2)));
            Assert.That(result.ToGridPosition, Is.EqualTo(new BFGridPosition(4, 7)));
            Assert.That(result.ActionPointCost, Is.EqualTo(2));
            Assert.That(result.RemainingActionPoints, Is.EqualTo(3));
        }

        [Test]
        public void CompleteMoveRejectsInsufficientActionPointsWithoutMutation()
        {
            var context = CreateContext(out var state);
            var rules = new BFUnitStateRules(context);

            Assert.That(
                rules.TryChangeActionState(state.RuntimeId, BFUnit_ActionState.Move),
                Is.True);

            var result = rules.TryMove(new MoveRequest(state.RuntimeId, new BFGridPosition(4, 7), 6));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Is.Not.Empty);
            Assert.That(state.GridPosition, Is.EqualTo(new BFGridPosition(1, 2)));
            Assert.That(state.Attributes.RemainingActionPoints, Is.EqualTo(5));
            Assert.That(state.ActionState, Is.EqualTo(BFUnit_ActionState.Move));
        }

        [Test]
        public void StartAttackLocksStateWithoutConsumingActionPoints()
        {
            var context = CreateContext(out var state);
            var target = CreateEnemy(context, currentHP: 20);
            var rules = new BFUnitStateRules(context);

            var result = rules.TryStartAttack(
                new AttackRequest(state.RuntimeId, target.RuntimeId, 2));

            Assert.That(result.Succeeded, Is.True, result.FailureReason);
            Assert.That(state.Attributes.RemainingActionPoints, Is.EqualTo(5));
            Assert.That(state.ActionState, Is.EqualTo(BFUnit_ActionState.Attack));
        }

        [Test]
        public void StartAttackRejectsCostAboveRuleAttackCostWithoutMutation()
        {
            var context = CreateContext(out var state);
            var target = CreateEnemy(context, currentHP: 20);
            var rules = new BFUnitStateRules(context);

            var result = rules.TryStartAttack(
                new AttackRequest(state.RuntimeId, target.RuntimeId, 3));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Is.Not.Empty);
            Assert.That(state.Attributes.RemainingActionPoints, Is.EqualTo(5));
            Assert.That(state.ActionState, Is.EqualTo(BFUnit_ActionState.Idle));
        }

        [Test]
        public void StartAttackRejectsCostBelowRuleAttackCostWithoutMutation()
        {
            var context = CreateContext(out var state);
            var target = CreateEnemy(context, currentHP: 20);
            var rules = new BFUnitStateRules(context);

            var result = rules.TryStartAttack(
                new AttackRequest(state.RuntimeId, target.RuntimeId, 1));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Is.Not.Empty);
            Assert.That(state.Attributes.RemainingActionPoints, Is.EqualTo(5));
            Assert.That(state.ActionState, Is.EqualTo(BFUnit_ActionState.Idle));
        }

        [Test]
        public void StartAttackRejectsUnitThatIsAlreadyMovingWithoutMutation()
        {
            var context = CreateContext(out var state);
            var target = CreateEnemy(context, currentHP: 20);
            var rules = new BFUnitStateRules(context);

            Assert.That(
                rules.TryChangeActionState(state.RuntimeId, BFUnit_ActionState.Move),
                Is.True);

            var result = rules.TryStartAttack(
                new AttackRequest(state.RuntimeId, target.RuntimeId, 2));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Is.Not.Empty);
            Assert.That(state.Attributes.RemainingActionPoints, Is.EqualTo(5));
            Assert.That(state.ActionState, Is.EqualTo(BFUnit_ActionState.Move));
        }

        [Test]
        public void StartAttackRejectsInsufficientActionPointsWithoutMutation()
        {
            var context = CreateContext(out var state);
            var target = CreateEnemy(context, currentHP: 20);
            var rules = new BFUnitStateRules(context);

            Assert.That(rules.TryConsumeActionPoints(state.RuntimeId, 4), Is.True);
            var result = rules.TryStartAttack(
                new AttackRequest(state.RuntimeId, target.RuntimeId, 2));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Is.Not.Empty);
            Assert.That(state.Attributes.RemainingActionPoints, Is.EqualTo(1));
            Assert.That(state.ActionState, Is.EqualTo(BFUnit_ActionState.Idle));
        }

        [Test]
        public void StartAttackRejectsOutOfRangeTargetWithoutMutation()
        {
            var context = CreateContext(out var state);
            var target = CreateEnemy(context, currentHP: 20, gridPosition: new BFGridPosition(8, 8));
            var rules = new BFUnitStateRules(context);

            var result = rules.TryStartAttack(
                new AttackRequest(state.RuntimeId, target.RuntimeId, 2));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Does.Contain("攻击范围"));
            Assert.That(state.ActionState, Is.EqualTo(BFUnit_ActionState.Idle));
            Assert.That(state.Attributes.RemainingActionPoints, Is.EqualTo(5));
        }

        [Test]
        public void StartAttackRejectsSameFactionTargetWithoutMutation()
        {
            var context = CreateContext(out var state);
            var ally = CreateEnemy(context, currentHP: 20, faction: BFUnitFaction.Player);
            var rules = new BFUnitStateRules(context);

            var result = rules.TryStartAttack(
                new AttackRequest(state.RuntimeId, ally.RuntimeId, 2));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Does.Contain("同阵营"));
            Assert.That(state.ActionState, Is.EqualTo(BFUnit_ActionState.Idle));
            Assert.That(state.Attributes.RemainingActionPoints, Is.EqualTo(5));
        }

        [Test]
        public void ResolveAttackCommitsActionPointsAndDamageAsOneRuleCommand()
        {
            var context = CreateContext(out var attacker);
            var target = CreateEnemy(context, currentHP: 20);
            var rules = new BFUnitStateRules(context);

            var startResult = rules.TryStartAttack(
                new AttackRequest(attacker.RuntimeId, target.RuntimeId, 2));
            Assert.That(startResult.Succeeded, Is.True, startResult.FailureReason);
            var result = rules.TryResolveAttack(
                new AttackRequest(attacker.RuntimeId, target.RuntimeId, 2));

            Assert.That(result.Succeeded, Is.True, result.FailureReason);
            Assert.That(result.Damage, Is.EqualTo(8));
            Assert.That(result.TargetWasKilled, Is.False);
            Assert.That(attacker.Attributes.RemainingActionPoints, Is.EqualTo(3));
            Assert.That(target.Attributes.CurrentHP, Is.EqualTo(12));
            Assert.That(attacker.ActionState, Is.EqualTo(BFUnit_ActionState.Attack));
        }

        [Test]
        public void ResolveAttackCommitsDeathStateAtomically()
        {
            var context = CreateContext(out var attacker);
            var target = CreateEnemy(context, currentHP: 8);
            var rules = new BFUnitStateRules(context);

            var startResult = rules.TryStartAttack(
                new AttackRequest(attacker.RuntimeId, target.RuntimeId, 2));
            Assert.That(startResult.Succeeded, Is.True, startResult.FailureReason);
            var result = rules.TryResolveAttack(
                new AttackRequest(attacker.RuntimeId, target.RuntimeId, 2));

            Assert.That(result.Succeeded, Is.True, result.FailureReason);
            Assert.That(result.TargetWasKilled, Is.True);
            Assert.That(target.Attributes.CurrentHP, Is.Zero);
            Assert.That(target.ActionState, Is.EqualTo(BFUnit_ActionState.Dead));
            Assert.That(attacker.Attributes.RemainingActionPoints, Is.EqualTo(3));
        }

        [Test]
        public void ResolveAttackRejectsWhenAttackerIsNotInAttackStateWithoutMutation()
        {
            var context = CreateContext(out var attacker);
            var target = CreateEnemy(context, currentHP: 20);
            var rules = new BFUnitStateRules(context);

            var result = rules.TryResolveAttack(
                new AttackRequest(attacker.RuntimeId, target.RuntimeId, 2));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Is.Not.Empty);
            Assert.That(attacker.Attributes.RemainingActionPoints, Is.EqualTo(5));
            Assert.That(target.Attributes.CurrentHP, Is.EqualTo(20));
        }

        [Test]
        public void ResolveAttackRejectsWhenTargetMovesOutOfRangeWithoutMutation()
        {
            var context = CreateContext(out var attacker);
            var target = CreateEnemy(context, currentHP: 20, gridPosition: new BFGridPosition(2, 0));
            var rules = new BFUnitStateRules(context);

            var startResult = rules.TryStartAttack(
                new AttackRequest(attacker.RuntimeId, target.RuntimeId, 2));
            Assert.That(startResult.Succeeded, Is.True, startResult.FailureReason);
            Assert.That(
                rules.TrySetGridPosition(target.RuntimeId, new BFGridPosition(8, 8)),
                Is.True);

            var result = rules.TryResolveAttack(
                new AttackRequest(attacker.RuntimeId, target.RuntimeId, 2));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Does.Contain("攻击范围"));
            Assert.That(attacker.Attributes.RemainingActionPoints, Is.EqualTo(5));
            Assert.That(target.Attributes.CurrentHP, Is.EqualTo(20));
        }

        [Test]
        public void ResolveAttackRejectsDeadTargetWithoutMutation()
        {
            var context = CreateContext(out var attacker);
            var target = CreateEnemy(context, currentHP: 0);
            var rules = new BFUnitStateRules(context);

            var startResult = rules.TryStartAttack(
                new AttackRequest(attacker.RuntimeId, target.RuntimeId, 2));
            Assert.That(startResult.Succeeded, Is.False, startResult.FailureReason);
            Assert.That(attacker.Attributes.RemainingActionPoints, Is.EqualTo(5));
        }

        [Test]
        public void WaitSucceedsAndSettlesActionPointsToZero()
        {
            var context = CreateContext(out var state);
            var rules = new BFUnitStateRules(context);

            var result = rules.TryWait(new WaitRequest(state.RuntimeId));

            Assert.That(result.Succeeded, Is.True, result.FailureReason);
            Assert.That(result.RemainingActionPointsAfter, Is.EqualTo(0));
            Assert.That(state.Attributes.RemainingActionPoints, Is.EqualTo(0));
        }

        [Test]
        public void WaitFailsWhenActionPointsAreZero()
        {
            var context = CreateContext(out var state);
            var rules = new BFUnitStateRules(context);

            Assert.That(rules.TryConsumeActionPoints(state.RuntimeId, 5), Is.True);
            var result = rules.TryWait(new WaitRequest(state.RuntimeId));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Is.Not.Empty);
            Assert.That(state.Attributes.RemainingActionPoints, Is.EqualTo(0));
        }

        private static BFUnitState CreateEnemy(
            BFBattleContext context,
            int currentHP,
            BFGridPosition? gridPosition = null,
            BFUnitFaction faction = BFUnitFaction.Enemy)
        {
            var enemy = new BFUnitState(
                "profile-enemy",
                "runtime-enemy",
                faction,
                BFUnitRole.Warrior,
                BFUnitTier.Normal,
                1,
                new BFUnitAttributes(20, 5, 8, currentHP),
                gridPosition ?? new BFGridPosition(3, 3));
            Assert.That(context.TryRegisterUnit(enemy), Is.True);
            return enemy;
        }

        private static BFBattleContext CreateContext(
            out BFUnitState state,
            int currentHP = 20)
        {
            var context = new BFBattleContext("battle-rules-test");
            state = new BFUnitState(
                "profile-test",
                "runtime-test",
                BFUnitFaction.Player,
                BFUnitRole.Warrior,
                BFUnitTier.Normal,
                new BFUnitAttributes(20, 5, 8, currentHP, baseAttackRange: 3, baseAttackCost: 2),
                new BFGridPosition(1, 2));
            Assert.That(context.TryRegisterUnit(state), Is.True);
            return context;
        }
    }
}
