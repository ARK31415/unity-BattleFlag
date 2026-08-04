using System;
using System.Collections.Generic;

namespace BF.Game.Eventing
{
    /// <summary>
    /// 保存单一事件类型 <see cref="Action{T}" /> 监听集合的强类型通道。
    ///
    /// 一个通道只处理一种 <typeparamref name="TEvent" />。每条监听记录都有独立节点，
    /// 使重复订阅同一个回调时仍可以通过各自令牌分别移除。
    /// </summary>
    /// <typeparam name="TEvent">通道负责分发的事件数据类型。</typeparam>
    internal sealed class BFEventChannel<TEvent> : IBFEventChannel
    {
        private readonly List<BFEventSubscriptionEntry<TEvent>> _entries = new();
        private Action<TEvent> _listeners;

        /// <inheritdoc />
        public bool IsEmpty => _listeners == null;

        /// <summary>
        /// 添加一个监听者，并创建与本次订阅一一对应的清理令牌。
        /// </summary>
        /// <param name="listener">收到 <typeparamref name="TEvent" /> 后执行的回调。</param>
        /// <returns>仅代表本次订阅关系的清理令牌。</returns>
        public BFEventSubscription<TEvent> Subscribe(Action<TEvent> listener)
        {
            var entry = new BFEventSubscriptionEntry<TEvent>(listener);
            _entries.Add(entry);
            _listeners += entry.Handler;
            return new BFEventSubscription<TEvent>(this, entry);
        }

        /// <summary>
        /// 按回调引用移除最后一条匹配的监听记录。
        /// </summary>
        /// <param name="listener">要移除的监听回调。</param>
        public void Unsubscribe(Action<TEvent> listener)
        {
            for (var index = _entries.Count - 1; index >= 0; index--)
            {
                if (_entries[index].Listener != listener) continue;

                Remove(_entries[index]);
                return;
            }
        }

        /// <summary>
        /// 移除指定订阅节点。
        ///
        /// 该方法由订阅令牌调用，因此可以精确移除某一次订阅，而不会误删同一回调的其他订阅。
        /// </summary>
        /// <param name="entry">要移除的订阅节点。</param>
        internal void Remove(BFEventSubscriptionEntry<TEvent> entry)
        {
            if (entry == null || entry.IsRemoved) return;

            entry.IsRemoved = true;
            _listeners -= entry.Handler;
            _entries.Remove(entry);
        }

        /// <summary>
        /// 将事件同步分发给当前所有监听者。
        /// </summary>
        /// <param name="eventData">要分发的事件数据。</param>
        public void Publish(TEvent eventData)
        {
            _listeners?.Invoke(eventData);
        }

        /// <inheritdoc />
        public void Clear()
        {
            _listeners = null;
            foreach (var entry in _entries)
            {
                entry.IsRemoved = true;
            }

            _entries.Clear();
        }
    }

    /// <summary>
    /// 保存一次订阅所需的回调节点。
    ///
    /// 节点将原始监听者包装为独立的 <see cref="Action{T}" /> 实例，
    /// 从而让委托链中的重复回调可以通过不同订阅令牌分别管理。
    /// </summary>
    /// <typeparam name="TEvent">订阅节点处理的事件数据类型。</typeparam>
    internal sealed class BFEventSubscriptionEntry<TEvent>
    {
        /// <summary>
        /// 创建一个订阅节点。
        /// </summary>
        /// <param name="listener">原始监听回调。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="listener" /> 为空时抛出。</exception>
        public BFEventSubscriptionEntry(Action<TEvent> listener)
        {
            Listener = listener ?? throw new ArgumentNullException(nameof(listener));
            Handler = Invoke;
        }

        /// <summary>
        /// 原始监听回调，用于按回调移除订阅。
        /// </summary>
        public Action<TEvent> Listener { get; }

        /// <summary>
        /// 实际挂接到通道委托链的包装回调。
        /// </summary>
        public Action<TEvent> Handler { get; }

        /// <summary>
        /// 指示订阅节点是否已经从通道移除。
        /// </summary>
        public bool IsRemoved { get; set; }

        /// <summary>
        /// 将通道收到的事件转交给原始监听回调。
        /// </summary>
        /// <param name="eventData">通道当前正在分发的事件数据。</param>
        private void Invoke(TEvent eventData)
        {
            Listener(eventData);
        }
    }
}
