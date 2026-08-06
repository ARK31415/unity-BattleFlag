using BF.Game.Battle.Domain.Events;
using NUnit.Framework;

namespace BF.Game.Tests.EditMode.Domain
{
    /// <summary>
    /// 验证战斗 Session 对 Context、事件总线和生命周期权限的所有权管理。
    /// </summary>
    public sealed class BFBattleSessionTests
    {
        [Test]
        public void SessionOwnsContextAndStartsCreated()
        {
            var context = new BF.Game.Battle.Domain.BFBattleContext("battle-001");
            using var session = new BF.Game.Battle.Domain.BFBattleSession(context);

            Assert.That(session.Context, Is.SameAs(context));
            Assert.That(session.State, Is.EqualTo(BF.Game.Battle.Domain.BFBattleSessionState.Created));
        }

        [Test]
        public void PublishIsAllowedOnlyWhileRunning()
        {
            var context = new BF.Game.Battle.Domain.BFBattleContext("battle-001");
            using var session = new BF.Game.Battle.Domain.BFBattleSession(context);
            var callbackCount = 0;
            session.Subscribe<BFBattleStartedEvent>(_ => callbackCount++);

            Assert.Throws<System.InvalidOperationException>(() => session.Publish(new BFBattleStartedEvent("battle-001")));

            session.Start();
            session.Publish(new BFBattleStartedEvent("battle-001"));
            Assert.That(callbackCount, Is.EqualTo(1));

            session.Complete(BF.Game.Battle.Domain.BattleResult.Victory("battle-001", 1));
            Assert.Throws<System.InvalidOperationException>(() => session.Publish(new BFBattleStartedEvent("battle-001")));
            Assert.Throws<System.InvalidOperationException>(() => session.Subscribe<BFBattleStartedEvent>(_ => { }));
        }

        [Test]
        public void CompletedSessionAllowsUnsubscribeButNotNewSubscriptions()
        {
            var context = new BF.Game.Battle.Domain.BFBattleContext("battle-001");
            using var session = new BF.Game.Battle.Domain.BFBattleSession(context);
            System.Action<BFBattleStartedEvent> listener = _ => { };
            session.Subscribe(listener);
            session.Start();
            session.Complete(BF.Game.Battle.Domain.BattleResult.Victory("battle-001", 1));

            Assert.DoesNotThrow(() => session.Unsubscribe(listener));
        }

        [Test]
        public void DisposeIsIdempotentAndBlocksFurtherUse()
        {
            var context = new BF.Game.Battle.Domain.BFBattleContext("battle-001");
            var session = new BF.Game.Battle.Domain.BFBattleSession(context);

            session.Dispose();
            Assert.DoesNotThrow(session.Dispose);
            Assert.That(session.State, Is.EqualTo(BF.Game.Battle.Domain.BFBattleSessionState.Disposed));
            Assert.Throws<System.ObjectDisposedException>(() => _ = session.Context);
            Assert.Throws<System.ObjectDisposedException>(() => session.Start());
        }
    }
}
