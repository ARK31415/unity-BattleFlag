using BF.Game.Battle.Domain;
using BF.Game.Battle.Domain.Events;
using BF.Game.Battle.Domain.Units;
using BF.Game.Battle.Rules.Units;
using NUnit.Framework;

namespace BF.Game.Tests.EditMode.Rules
{
    /// <summary>
    /// 验证单位规则入口只修改 Domain 状态，并维护 HP、AP、位置和死亡不变量。
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
            Assert.That(
                rules.TryCompleteMove(
                    state.RuntimeId,
                    new BFGridPosition(4, 7),
                    2),
                Is.True);

            Assert.That(state.GridPosition, Is.EqualTo(new BFGridPosition(4, 7)));
            Assert.That(state.Attributes.RemainingActionPoints, Is.EqualTo(3));
            Assert.That(state.ActionState, Is.EqualTo(BFUnit_ActionState.Idle));
        }

        [Test]
        public void CompleteMoveRejectsInsufficientActionPointsWithoutMutation()
        {
            var context = CreateContext(out var state);
            var rules = new BFUnitStateRules(context);

            Assert.That(
                rules.TryChangeActionState(state.RuntimeId, BFUnit_ActionState.Move),
                Is.True);

            Assert.That(
                rules.TryCompleteMove(
                    state.RuntimeId,
                    new BFGridPosition(4, 7),
                    6),
                Is.False);

            Assert.That(state.GridPosition, Is.EqualTo(new BFGridPosition(1, 2)));
            Assert.That(state.Attributes.RemainingActionPoints, Is.EqualTo(5));
            Assert.That(state.ActionState, Is.EqualTo(BFUnit_ActionState.Move));
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
                new BFUnitAttributes(20, 5, 8, currentHP),
                new BFGridPosition(1, 2));
            Assert.That(context.TryRegisterUnit(state), Is.True);
            return context;
        }
    }
}
