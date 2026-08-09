using System;
using System.Collections.Generic;
using BF.Game.Eventing;
using NUnit.Framework;

namespace BF.Game.Tests.EditMode.Eventing
{
    /// <summary>
    /// 验证战斗事件总线基础设施的公开行为合同。
    /// </summary>
    public sealed class BFScopedEventBusTests
    {
        [Test]
        public void Publish_InvokesSubscribersInSubscriptionOrder()
        {
            using var bus = new BFScopedEventBus();
            var calls = new List<int>();

            bus.Subscribe<int>(value => calls.Add(value));
            bus.Subscribe<int>(value => calls.Add(value + 1));

            bus.Publish(10);

            CollectionAssert.AreEqual(new[] { 10, 11 }, calls);
        }

        [Test]
        public void SubscriptionToken_DisposesOnlyItsOwnSubscription()
        {
            using var bus = new BFScopedEventBus();
            var firstCalls = 0;
            var secondCalls = 0;
            var first = bus.Subscribe<int>(_ => firstCalls++);
            bus.Subscribe<int>(_ => secondCalls++);

            first.Dispose();
            bus.Publish(1);

            Assert.That(firstCalls, Is.EqualTo(0));
            Assert.That(secondCalls, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateCallbackSubscriptions_HaveIndependentTokens()
        {
            using var bus = new BFScopedEventBus();
            var calls = 0;
            Action<int> callback = _ => calls++;
            var first = bus.Subscribe(callback);
            var second = bus.Subscribe(callback);

            first.Dispose();
            bus.Publish(1);
            Assert.That(calls, Is.EqualTo(1));

            second.Dispose();
            bus.Publish(1);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void Unsubscribe_RemovesOneMatchingSubscription()
        {
            using var bus = new BFScopedEventBus();
            var calls = 0;
            Action<int> callback = _ => calls++;
            bus.Subscribe(callback);

            bus.Unsubscribe(callback);
            bus.Publish(1);

            Assert.That(calls, Is.EqualTo(0));
        }

        [Test]
        public void SeparateBuses_DoNotReceiveEachOthersEvents()
        {
            using var firstBus = new BFScopedEventBus();
            using var secondBus = new BFScopedEventBus();
            var firstCalls = 0;
            var secondCalls = 0;
            firstBus.Subscribe<int>(_ => firstCalls++);
            secondBus.Subscribe<int>(_ => secondCalls++);

            firstBus.Publish(1);

            Assert.That(firstCalls, Is.EqualTo(1));
            Assert.That(secondCalls, Is.EqualTo(0));
        }

        [Test]
        public void Publish_PropagatesListenerExceptions()
        {
            using var bus = new BFScopedEventBus();
            var expected = new InvalidOperationException("listener failure");
            bus.Subscribe<int>(_ => throw expected);

            var actual = Assert.Throws<InvalidOperationException>(() => bus.Publish(1));

            Assert.That(actual, Is.SameAs(expected));
        }

        [Test]
        public void Dispose_IsIdempotentAndRejectsFurtherUse()
        {
            var bus = new BFScopedEventBus();
            bus.Dispose();
            bus.Dispose();

            Assert.Throws<ObjectDisposedException>(() => bus.Subscribe<int>(_ => { }));
            Assert.Throws<ObjectDisposedException>(() => bus.Unsubscribe<int>(_ => { }));
            Assert.Throws<ObjectDisposedException>(() => bus.Publish(1));
        }

        [Test]
        public void SubscriptionGroup_DisposesAllSubscriptions()
        {
            using var bus = new BFScopedEventBus();
            using var group = new BFEventSubscriptionGroup();
            var calls = 0;
            group.Add(bus.Subscribe<int>(_ => calls++));
            group.Add(bus.Subscribe<int>(_ => calls++));

            group.Dispose();
            bus.Publish(1);

            Assert.That(calls, Is.EqualTo(0));
        }
    }
}
