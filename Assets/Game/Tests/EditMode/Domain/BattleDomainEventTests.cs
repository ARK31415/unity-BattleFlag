using BF.Game.Battle.Domain.Events;
using NUnit.Framework;

namespace BF.Game.Tests.EditMode.Domain
{
    public sealed class BattleDomainEventTests
    {
        [Test]
        public void AttackResolvedEvent_ExposesImmutableResolutionData()
        {
            var eventData = new BFAttackResolvedEvent(
                "battle-1",
                "attacker-1",
                "target-1",
                7,
                13,
                true,
                2);

            Assert.That(eventData.BattleId, Is.EqualTo("battle-1"));
            Assert.That(eventData.AttackerId, Is.EqualTo("attacker-1"));
            Assert.That(eventData.TargetId, Is.EqualTo("target-1"));
            Assert.That(eventData.FinalDamage, Is.EqualTo(7));
            Assert.That(eventData.TargetRemainingHp, Is.EqualTo(13));
            Assert.That(eventData.TargetWasDefeated, Is.True);
            Assert.That(eventData.TurnNumber, Is.EqualTo(2));
        }

        [Test]
        public void UnitDefeatedEvent_UsesDefeatedUnitAsPrimaryIdentity()
        {
            var eventData = new BFUnitDefeatedEvent(
                "battle-1",
                "enemy-1",
                BFUnitFaction.Enemy,
                "player-1",
                3);

            Assert.That(eventData.UnitId, Is.EqualTo("enemy-1"));
            Assert.That(eventData.Faction, Is.EqualTo(BFUnitFaction.Enemy));
            Assert.That(eventData.DefeatedByUnitId, Is.EqualTo("player-1"));
            Assert.That(eventData.TurnNumber, Is.EqualTo(3));
        }

        [Test]
        public void EventConstructors_NormalizeNullIdentifiers()
        {
            var started = new BFBattleStartedEvent(null);
            var phaseChanged = new BFBattlePhaseChangedEvent(null, BFBattlePhase.None, BFBattlePhase.Init, 0, 0);
            var attack = new BFAttackResolvedEvent(null, null, null, 0, 0, false, 0);
            var defeated = new BFUnitDefeatedEvent(null, null, BFUnitFaction.None, null, 0);
            var completed = new BFBattleCompletedEvent(null, BFUnitFaction.None, 0);

            Assert.That(started.BattleId, Is.Empty);
            Assert.That(phaseChanged.BattleId, Is.Empty);
            Assert.That(attack.BattleId, Is.Empty);
            Assert.That(attack.AttackerId, Is.Empty);
            Assert.That(attack.TargetId, Is.Empty);
            Assert.That(defeated.BattleId, Is.Empty);
            Assert.That(defeated.UnitId, Is.Empty);
            Assert.That(defeated.DefeatedByUnitId, Is.Empty);
            Assert.That(completed.BattleId, Is.Empty);
        }
    }
}
