using System;
using System.Collections.Generic;

namespace BF.Game.Eventing
{
    /// <summary>
    /// 不依赖 Unity 的实例级、按事件类型分发的强类型事件总线。
    ///
    /// 总线本身不是泛型类，而是通过泛型方法和泛型通道保证事件类型安全：
    /// <c>Subscribe&lt;TEvent&gt;</c>、<c>Publish&lt;TEvent&gt;</c> 分别访问对应的事件通道。
    /// 每个战斗会话独立持有一个实例，不使用静态全局状态。
    /// </summary>
    public sealed class BFScopedEventBus : IBFEventBus
    {
        private readonly Dictionary<Type, IBFEventChannel> _channels = new();
        private bool _isDisposed;

        /// <inheritdoc />
        public IDisposable Subscribe<TEvent>(Action<TEvent> listener)
        {
            EnsureNotDisposed();
            if (listener == null) throw new ArgumentNullException(nameof(listener));

            var channel = GetOrCreateChannel<TEvent>();
            return channel.Subscribe(listener);
        }

        /// <inheritdoc />
        public void Unsubscribe<TEvent>(Action<TEvent> listener)
        {
            EnsureNotDisposed();
            if (listener == null) throw new ArgumentNullException(nameof(listener));

            if (!_channels.TryGetValue(typeof(TEvent), out var rawChannel)) return;

            var channel = (BFEventChannel<TEvent>)rawChannel;
            channel.Unsubscribe(listener);
            RemoveEmptyChannel(typeof(TEvent), channel);
        }

        /// <inheritdoc />
        public void Publish<TEvent>(TEvent eventData)
        {
            EnsureNotDisposed();
            if (!_channels.TryGetValue(typeof(TEvent), out var rawChannel)) return;

            ((BFEventChannel<TEvent>)rawChannel).Publish(eventData);
        }

        /// <inheritdoc />
        /// <summary>
        /// 清理所有事件通道和监听关系，使总线进入不可再使用的状态。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            foreach (var channel in _channels.Values)
            {
                channel.Clear();
            }

            _channels.Clear();
        }

        /// <summary>
        /// 获取指定事件类型的现有通道；不存在时创建一个新通道。
        /// </summary>
        /// <typeparam name="TEvent">要访问的事件数据类型。</typeparam>
        /// <returns>对应 <typeparamref name="TEvent" /> 的强类型事件通道。</returns>
        private BFEventChannel<TEvent> GetOrCreateChannel<TEvent>()
        {
            var eventType = typeof(TEvent);
            if (_channels.TryGetValue(eventType, out var rawChannel))
                return (BFEventChannel<TEvent>)rawChannel;

            var channel = new BFEventChannel<TEvent>();
            _channels.Add(eventType, channel);
            return channel;
        }

        /// <summary>
        /// 在通道没有监听者时从总线移除通道。
        /// </summary>
        /// <typeparam name="TEvent">通道负责的事件数据类型。</typeparam>
        /// <param name="eventType">要移除的事件类型键。</param>
        /// <param name="channel">要检查的事件通道。</param>
        private void RemoveEmptyChannel<TEvent>(Type eventType, BFEventChannel<TEvent> channel)
        {
            if (channel.IsEmpty)
                _channels.Remove(eventType);
        }

        /// <summary>
        /// 确认总线仍处于可使用状态。
        /// </summary>
        /// <exception cref="ObjectDisposedException">当总线已经释放时抛出。</exception>
        private void EnsureNotDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(BFScopedEventBus));
        }
    }
}
