using System;
using BF.Game.Battle.Domain.Events;
using BF.Game.Eventing;

namespace BF.Game.Battle.Domain
{
    /// <summary>
    /// 一次战斗的规则运行时会话。
    ///
    /// Session 统一持有纯规则 Context、Session 级事件总线和订阅生命周期，
    /// 不使用全局静态状态，也不负责 UI、动画、音频或 Unity 场景查找。
    /// </summary>
    public sealed class BFBattleSession : IDisposable
    {
        private readonly BFBattleContext _context;
        private readonly BFEventSubscriptionGroup _subscriptions = new();
        private int _nextRuntimeNumber = 1;
        private bool _isDisposed;

        /// <summary>创建处于 Created 状态的战斗 Session。</summary>
        /// <param name="context">本场战斗的纯规则上下文。</param>
        public BFBattleSession(BFBattleContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            EventBus = new BFScopedEventBus();
            State = BFBattleSessionState.Created;
        }

        /// <summary>当前 Session 持有的纯规则 Context。</summary>
        public BFBattleContext Context
        {
            get
            {
                EnsureNotDisposed();
                return _context;
            }
        }

        /// <summary>当前 Session 生命周期状态。</summary>
        public BFBattleSessionState State { get; private set; }

        /// <summary>
        /// 为当前 Session 生成不复用的运行时单位身份。
        /// 计数器属于会话实例，因此不会被不同战斗或不同工厂共享。
        /// </summary>
        public string CreateRuntimeId()
        {
            EnsureNotDisposed();
            if (State == BFBattleSessionState.Completed)
                throw new InvalidOperationException("Cannot create a RuntimeId after a battle session is completed.");

            return $"{Context.BattleId}_unit_{_nextRuntimeNumber++:D4}";
        }

        /// <summary>当前 Session 独立持有的事件总线。</summary>
        internal BFScopedEventBus EventBus { get; }

        /// <summary>
        /// 订阅 Session 内的一种领域事件，并将订阅令牌纳入 Session 生命周期管理。
        /// </summary>
        /// <typeparam name="TEvent">领域事件数据类型。</typeparam>
        /// <param name="listener">事件回调。</param>
        /// <returns>本次订阅的清理令牌。</returns>
        public IDisposable Subscribe<TEvent>(Action<TEvent> listener)
        {
            EnsureCanSubscribe();
            var token = EventBus.Subscribe(listener);
            _subscriptions.Add(token);
            return token;
        }

        /// <summary>通过原始回调显式移除一次订阅。</summary>
        /// <typeparam name="TEvent">领域事件数据类型。</typeparam>
        /// <param name="listener">之前注册的回调。</param>
        public void Unsubscribe<TEvent>(Action<TEvent> listener)
        {
            EnsureCanUnsubscribe();
            EventBus.Unsubscribe(listener);
        }

        /// <summary>同步发布当前 Session 内的领域事件。</summary>
        /// <typeparam name="TEvent">领域事件数据类型。</typeparam>
        /// <param name="eventData">要发布的领域事件数据。</param>
        public void Publish<TEvent>(TEvent eventData)
        {
            EnsureCanPublish();
            EventBus.Publish(eventData);
        }

        /// <summary>
        /// 在完成事件发布前写入已计算的战斗结果。
        /// 会话仍保持 Running，使完成事件回调能够读取最终规则结果。
        /// </summary>
        public void SetResult(BattleResult result)
        {
            EnsureNotDisposed();
            if (State != BFBattleSessionState.Running)
                throw new InvalidOperationException($"Cannot set a battle result in state {State}.");
            _context.SetResult(result ?? throw new ArgumentNullException(nameof(result)));
        }

        /// <summary>
        /// 在规则状态更新完成后同步推进阶段、回合和轮次。
        /// </summary>
        public void UpdateProgress(BFBattlePhase phase, int turnNumber, int roundNumber)
        {
            EnsureNotDisposed();
            _context.SetCurrentPhase(phase);
            _context.SetTurnNumber(turnNumber);
            _context.SetRoundNumber(roundNumber);
        }

        /// <summary>将 Session 从 Created 推进到 Running。</summary>
        public void Start()
        {
            EnsureNotDisposed();
            if (State != BFBattleSessionState.Created)
                throw new InvalidOperationException($"Cannot start a battle session in state {State}.");

            State = BFBattleSessionState.Running;
        }

        /// <summary>
        /// 写入最终战斗结果并将 Session 从 Running 推进到 Completed。
        /// </summary>
        /// <param name="result">已经由规则流程计算完成的战斗结果。</param>
        public void Complete(BattleResult result)
        {
            EnsureNotDisposed();
            if (State != BFBattleSessionState.Running)
                throw new InvalidOperationException($"Cannot complete a battle session in state {State}.");
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (!result.HasResult)
                throw new ArgumentException("战斗结果必须已经完成计算。", nameof(result));

            _context.SetResult(result);
            State = BFBattleSessionState.Completed;
        }

        /// <summary>
        /// 使用已通过 <see cref="SetResult" /> 写入上下文的结果完成会话。
        /// </summary>
        public void Complete()
        {
            EnsureNotDisposed();
            if (State != BFBattleSessionState.Running)
                throw new InvalidOperationException($"Cannot complete a battle session in state {State}.");
            if (_context.Result == null || !_context.Result.HasResult)
                throw new InvalidOperationException("Battle result must be set before completing the session.");

            State = BFBattleSessionState.Completed;
        }

        /// <summary>
        /// 释放订阅、事件总线和规则上下文。
        /// 重复释放是安全的，释放后 Session 不再接受任何业务操作。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            _subscriptions.Dispose();
            EventBus.Dispose();
            _context.Dispose();
            State = BFBattleSessionState.Disposed;
        }

        private void EnsureCanSubscribe()
        {
            EnsureNotDisposed();
            if (State == BFBattleSessionState.Completed)
                throw new InvalidOperationException("Cannot subscribe after a battle session is completed.");
        }

        private void EnsureCanUnsubscribe()
        {
            EnsureNotDisposed();
        }

        private void EnsureCanPublish()
        {
            EnsureNotDisposed();
            if (State != BFBattleSessionState.Running)
                throw new InvalidOperationException($"Cannot publish a battle event in state {State}.");
        }

        private void EnsureNotDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(BFBattleSession));
        }
    }
}
