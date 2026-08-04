using System;

namespace BF.Game.Eventing
{
    /// <summary>
    /// 表示一次具体事件订阅关系的清理令牌。
    ///
    /// 令牌保存订阅节点而不是仅保存原始回调，因此可以精确清理重复订阅中的某一次。
    /// </summary>
    /// <typeparam name="TEvent">令牌所管理的事件数据类型。</typeparam>
    internal sealed class BFEventSubscription<TEvent> : IDisposable
    {
        private BFEventChannel<TEvent> _channel;
        private BFEventSubscriptionEntry<TEvent> _entry;
        private bool _isDisposed;

        /// <summary>
        /// 创建一个与指定通道和订阅节点绑定的令牌。
        /// </summary>
        /// <param name="channel">保存订阅节点的事件通道。</param>
        /// <param name="entry">本次订阅对应的唯一节点。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="channel" /> 或 <paramref name="entry" /> 为空时抛出。</exception>
        public BFEventSubscription(
            BFEventChannel<TEvent> channel,
            BFEventSubscriptionEntry<TEvent> entry)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _entry = entry ?? throw new ArgumentNullException(nameof(entry));
        }

        /// <summary>
        /// 清理本次订阅；重复调用不会重复移除或抛出异常。
        ///
        /// 即使所属通道已经被清空，令牌仍可安全释放。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            _channel.Remove(_entry);
            _channel = null;
            _entry = null;
        }
    }
}
