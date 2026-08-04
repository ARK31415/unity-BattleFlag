using BF.Game.Battle.Domain.Events;
using BF.Game.Runtime.Battle;
using BF.Game.Runtime.Battle.Events;
using NUnit.Framework;
using UnityEngine;

namespace BF.Game.Tests.EditMode.Battle.Events
{
    public sealed class BFBattleEventToSOAdapterTests
    {
        [Test]
        public void AttackResolved_UsesLegacySOFieldCompatibilityMapping()
        {
            var unitChannel = ScriptableObject.CreateInstance<BFUnitEventSO>();
            try
            {
                var received = default(BFUnitEventData);
                unitChannel.Register(data => received = data);
                using var session = new BFBattleSession(new BFBattleContext { BattleId = "battle-1" });
                using var adapter = new BFBattleEventToSOAdapter(session, null, null, unitChannel);

                session.Start();
                session.Publish(new BFAttackResolvedEvent("battle-1", "attacker-1", "target-1", 6, 14, false, 1));

                Assert.That(received.EventType, Is.EqualTo("Damaged"));
                Assert.That(received.UnitId, Is.EqualTo("target-1"));
                Assert.That(received.TargetId, Is.EqualTo("attacker-1"));
                Assert.That(received.Value, Is.EqualTo(6));
            }
            finally
            {
                Object.DestroyImmediate(unitChannel);
            }
        }

        [Test]
        public void PhaseChanged_MapsOnlySupportedTurnPhases()
        {
            var turnChannel = ScriptableObject.CreateInstance<BFTurnEventSO>();
            try
            {
                var receivedCount = 0;
                var received = default(BFTurnEventData);
                turnChannel.Register(data =>
                {
                    receivedCount++;
                    received = data;
                });
                using var session = new BFBattleSession(new BFBattleContext { BattleId = "battle-1" });
                using var adapter = new BFBattleEventToSOAdapter(session, null, turnChannel, null);

                session.Start();
                session.Publish(new BFBattlePhaseChangedEvent("battle-1", BFBattlePhase.Init, BFBattlePhase.Init, 0, 0));
                session.Publish(new BFBattlePhaseChangedEvent("battle-1", BFBattlePhase.Init, BFBattlePhase.PlayerTurn, 1, 0));

                Assert.That(receivedCount, Is.EqualTo(1));
                Assert.That(received.Phase, Is.EqualTo(BFTurnPhase.PlayerTurnStarted));
                Assert.That(received.TurnNumber, Is.EqualTo(1));
                Assert.That(received.RoundNumber, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(turnChannel);
            }
        }

        [Test]
        public void Dispose_StopsForwardingWithoutDisposingSession()
        {
            var battleChannel = ScriptableObject.CreateInstance<BFBattleEventSO>();
            try
            {
                var receivedCount = 0;
                battleChannel.Register(_ => receivedCount++);
                using var session = new BFBattleSession(new BFBattleContext { BattleId = "battle-1" });
                var adapter = new BFBattleEventToSOAdapter(session, battleChannel, null, null);
                session.Start();
                adapter.Dispose();

                session.Publish(new BFBattleStartedEvent("battle-1"));

                Assert.That(receivedCount, Is.EqualTo(0));
                Assert.That(session.State, Is.EqualTo(BFBattleSessionState.Running));
            }
            finally
            {
                Object.DestroyImmediate(battleChannel);
            }
        }
    }
}
