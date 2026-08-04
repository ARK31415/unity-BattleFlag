using System;
using BF.Game.Eventing;

namespace BF.Game.Runtime.Battle
{
    /// <summary>
    /// 一次战斗的运行时会话。
    ///
    /// Session 拥有上下文、作用域事件总线和订阅生命周期，不使用静态单例，
    /// 也不让 GC 决定战斗何时结束。
    /// </summary>
    public sealed class BFBattleSession : IDisposable
    {
        private readonly BFBattleContext _context;
        private readonly BFEventSubscriptionGroup _subscriptions = new();
        private bool _isDisposed;

        /// <summary>
        /// 创建一个处于 <see cref="BFBattleSessionState.Created" /> 状态的战斗会话。
        /// </summary>
        /// <param name="context">本场战斗的规则状态数据。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="context" /> 为空时抛出。</exception>
        public BFBattleSession(BFBattleContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            EventBus = new BFScopedEventBus();
            State = BFBattleSessionState.Created;
        }

        /// <summary>
        /// 本场战斗的规则状态数据。
        ///
        /// 会话进入 <see cref="BFBattleSessionState.Disposed" /> 后不再允许访问。
        /// </summary>
        public BFBattleContext Context
        {
            get
            {
                EnsureNotDisposed();
                return _context;
            }
        }

        /// <summary>
        /// 本场战斗独立的实例级、按事件类型分发的强类型事件总线。
        /// 订阅和发布应优先通过当前 Session 的方法进行，以获得生命周期校验。
        /// </summary>
        internal BFScopedEventBus EventBus { get; }

        /// <summary>
        /// 当前会话生命周期状态。
        /// </summary>
        public BFBattleSessionState State { get; private set; }

        /// <summary>
        /// 订阅一种战斗领域事件，并将订阅令牌纳入会话生命周期管理。
        /// </summary>
        /// <typeparam name="TEvent">要订阅的事件数据类型。</typeparam>
        /// <param name="listener">收到事件后同步执行的回调。</param>
        /// <returns>仅代表本次订阅关系的清理令牌。</returns>
        /// <exception cref="ArgumentNullException">当 <paramref name="listener" /> 为空时抛出。</exception>
        /// <exception cref="InvalidOperationException">当会话已经完成时抛出。</exception>
        /// <exception cref="ObjectDisposedException">当会话已经释放时抛出。</exception>
        public IDisposable Subscribe<TEvent>(Action<TEvent> listener)
        {
            EnsureCanSubscribe();
            var token = EventBus.Subscribe(listener);
            _subscriptions.Add(token);
            return token;
        }

        /// <summary>
        /// 通过原始回调移除一种事件的一次匹配订阅。
        /// </summary>
        /// <typeparam name="TEvent">要取消订阅的事件数据类型。</typeparam>
        /// <param name="listener">之前注册的事件回调。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="listener" /> 为空时抛出。</exception>
        /// <exception cref="ObjectDisposedException">当会话已经释放时抛出。</exception>
        public void Unsubscribe<TEvent>(Action<TEvent> listener)
        {
            EnsureCanUnsubscribe();
            EventBus.Unsubscribe(listener);
        }

        /// <summary>
        /// 发布一个战斗领域事件。
        /// </summary>
        /// <typeparam name="TEvent">要发布的事件数据类型。</typeparam>
        /// <param name="eventData">要同步分发的事件数据。</param>
        /// <exception cref="InvalidOperationException">当会话不处于 Running 状态时抛出。</exception>
        /// <exception cref="ObjectDisposedException">当会话已经释放时抛出。</exception>
        public void Publish<TEvent>(TEvent eventData)
        {
            EnsureCanPublish();
            EventBus.Publish(eventData);
        }

        /// <summary>
        /// 从 Created 进入 Running。只有 Running 状态允许发布规则事实。
        /// </summary>
        /// <exception cref="InvalidOperationException">当会话不处于 Created 状态时抛出。</exception>
        /// <exception cref="ObjectDisposedException">当会话已经释放时抛出。</exception>
        public void Start()
        {
            EnsureNotDisposed();
            if (State != BFBattleSessionState.Created)
                throw new InvalidOperationException($"Cannot start a battle session in state {State}.");

            State = BFBattleSessionState.Running;
        }

        /// <summary>
        /// 从 Running 进入 Completed。Completed 保留结果读取能力，但禁止新订阅和发布。
        /// </summary>
        /// <exception cref="InvalidOperationException">当会话不处于 Running 状态时抛出。</exception>
        /// <exception cref="ObjectDisposedException">当会话已经释放时抛出。</exception>
        public void Complete()
        {
            EnsureNotDisposed();
            if (State != BFBattleSessionState.Running)
                throw new InvalidOperationException($"Cannot complete a battle session in state {State}.");

            State = BFBattleSessionState.Completed;
        }

        /// <summary>
        /// 释放会话持有的订阅、事件通道和运行时对象引用。
        ///
        /// 释放操作幂等；释放后再次访问上下文或发布/订阅事件会抛出异常。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            _subscriptions.Dispose();
            EventBus.Dispose();
            _context.ReleaseRuntimeReferences();
            State = BFBattleSessionState.Disposed;
        }

        /// <summary>
        /// 验证当前状态是否允许新增订阅。
        /// </summary>
        private void EnsureCanSubscribe()
        {
            EnsureNotDisposed();
            if (State == BFBattleSessionState.Completed)
                throw new InvalidOperationException("Cannot subscribe after a battle session is completed.");
        }

        /// <summary>
        /// 验证当前会话仍可执行取消订阅。
        /// </summary>
        private void EnsureCanUnsubscribe()
        {
            EnsureNotDisposed();
        }

        /// <summary>
        /// 验证当前状态是否允许发布领域事件。
        /// </summary>
        private void EnsureCanPublish()
        {
            EnsureNotDisposed();
            if (State != BFBattleSessionState.Running)
                throw new InvalidOperationException($"Cannot publish a battle event in state {State}.");
        }

        /// <summary>
        /// 验证会话尚未释放。
        /// </summary>
        /// <exception cref="ObjectDisposedException">当会话已经释放时抛出。</exception>
        private void EnsureNotDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(BFBattleSession));
        }
    }
}
