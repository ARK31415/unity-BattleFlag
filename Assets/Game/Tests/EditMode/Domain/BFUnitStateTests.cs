using BF.Game.Battle.Domain.Units;
using BF.Game.Battle.Domain.Events;
using NUnit.Framework;

namespace BF.Game.Tests.EditMode.Domain
{
    /// <summary>
    /// 验证单位规则状态的身份、初始行动状态和死亡终止状态。
    /// </summary>
    public sealed class BFUnitStateTests
    {
        [Test]
        public void NewUnitStateStartsIdleAndKeepsProfileAndRuntimeIdentitySeparate()
        {
            var attributes = new BFUnitAttributes(100, 5, 20);
            var state = new BFUnitState(
                "profile-warrior",
                "runtime-001",
                BFUnitFaction.Player,
                BFUnitRole.Warrior,
                BFUnitTier.Normal,
                attributes,
                new BFGridPosition(2, 3));

            Assert.That(state.ProfileId, Is.EqualTo("profile-warrior"));
            Assert.That(state.RuntimeId, Is.EqualTo("runtime-001"));
            Assert.That(state.Faction, Is.EqualTo(BFUnitFaction.Player));
            Assert.That(state.Role, Is.EqualTo(BFUnitRole.Warrior));
            Assert.That(state.Tier, Is.EqualTo(BFUnitTier.Normal));
            Assert.That(state.GridPosition, Is.EqualTo(new BFGridPosition(2, 3)));
            Assert.That(state.ActionState, Is.EqualTo(BFUnitActionState.Idle));
            Assert.That(state.IsAlive, Is.True);
        }

        [Test]
        public void DeadStateIsTerminalAndRequiresZeroCurrentHP()
        {
            var attributes = new BFUnitAttributes(100, 5, 20);
            var state = CreateState(attributes);

            Assert.That(state.TryChangeActionState(BFUnitActionState.Dead), Is.False);

            attributes.SetCurrentHP(0);
            Assert.That(state.TryChangeActionState(BFUnitActionState.Dead), Is.True);
            Assert.That(state.ActionState, Is.EqualTo(BFUnitActionState.Dead));
            Assert.That(state.TryChangeActionState(BFUnitActionState.Idle), Is.False);
        }

        [Test]
        public void EmptyIdentityIsRejected()
        {
            var attributes = new BFUnitAttributes(100, 5, 20);

            Assert.Throws<System.ArgumentException>(() => new BFUnitState(
                string.Empty,
                "runtime-001",
                BFUnitFaction.Player,
                BFUnitRole.Warrior,
                BFUnitTier.Normal,
                attributes,
                new BFGridPosition(0, 0)));
        }

        private static BFUnitState CreateState(BFUnitAttributes attributes)
        {
            return new BFUnitState(
                "profile-warrior",
                "runtime-001",
                BFUnitFaction.Player,
                BFUnitRole.Warrior,
                BFUnitTier.Normal,
                attributes,
                new BFGridPosition(0, 0));
        }
    }
}
