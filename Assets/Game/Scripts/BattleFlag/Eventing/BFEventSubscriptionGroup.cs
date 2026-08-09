using System;
using System.Collections.Generic;

namespace BF.Game.Eventing
{
    /// <summary>
    /// 批量管理一组事件订阅令牌。
    ///
    /// 适合由战斗会话、适配器或表现对象持有，以便在所属生命周期结束时集中解除订阅。
    /// </summary>
    public sealed class BFEventSubscriptionGroup : IDisposable
    {
        private readonly List<IDisposable> _subscriptions = new();
        private bool _isDisposed;

        /// <summary>
        /// 添加一个订阅令牌；如果集合已经释放，则立即释放传入令牌。
        /// </summary>
        /// <param name="subscription">要纳入批量管理的订阅令牌。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="subscription" /> 为空时抛出。</exception>
        public void Add(IDisposable subscription)
        {
            if (subscription == null) throw new ArgumentNullException(nameof(subscription));

            if (_isDisposed)
            {
                subscription.Dispose();
                return;
            }

            _subscriptions.Add(subscription);
        }

        /// <summary>
        /// 按逆序释放集合中的全部订阅令牌；重复调用不会产生额外效果。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            for (var i = _subscriptions.Count - 1; i >= 0; i--)
            {
                _subscriptions[i].Dispose();
            }

            _subscriptions.Clear();
        }
    }
}
