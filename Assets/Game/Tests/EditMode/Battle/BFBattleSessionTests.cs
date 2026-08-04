using BF.Game.Battle.Domain.Events;
using BF.Game.Runtime.Battle;
using NUnit.Framework;

namespace BF.Game.Tests.EditMode.Battle
{
    public sealed class BFBattleSessionTests
    {
        [Test]
        public void Created_AllowsSubscriptionButRejectsPublish()
        {
            using var session = new BFBattleSession(new BFBattleContext());
            session.Subscribe<BFBattleStartedEvent>(_ => { });

            Assert.Throws<System.InvalidOperationException>(() =>
                session.Publish(new BFBattleStartedEvent("battle-1")));
        }

        [Test]
        public void Running_AllowsPublishAndReceivesEvent()
        {
            using var session = new BFBattleSession(new BFBattleContext());
            var received = false;
            session.Subscribe<BFBattleStartedEvent>(eventData => received = eventData.BattleId == "battle-1");

            session.Start();
            session.Publish(new BFBattleStartedEvent("battle-1"));

            Assert.That(received, Is.True);
        }

        [Test]
        public void Completed_AllowsUnsubscribeButRejectsSubscribeAndPublish()
        {
            using var session = new BFBattleSession(new BFBattleContext());
            System.Action<BFBattleStartedEvent> listener = _ => { };
            session.Subscribe(listener);
            session.Start();
            session.Complete();

            Assert.DoesNotThrow(() => session.Unsubscribe(listener));
            Assert.Throws<System.InvalidOperationException>(() => session.Subscribe<BFBattleStartedEvent>(_ => { }));
            Assert.Throws<System.InvalidOperationException>(() =>
                session.Publish(new BFBattleStartedEvent("battle-1")));
        }

        [Test]
        public void Dispose_IsIdempotentAndRejectsFurtherUse()
        {
            var context = new BFBattleContext();
            context.Units.Add(null);
            var session = new BFBattleSession(context);
            session.Dispose();
            session.Dispose();

            Assert.That(session.State, Is.EqualTo(BFBattleSessionState.Disposed));
            Assert.That(context.Units, Is.Null);
            Assert.Throws<System.ObjectDisposedException>(() => _ = session.Context);
            Assert.Throws<System.ObjectDisposedException>(() => session.Publish(1));
            Assert.Throws<System.ObjectDisposedException>(() => session.Subscribe<int>(_ => { }));
        }
    }
}
