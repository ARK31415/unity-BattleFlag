using System;

namespace BF.Game.Eventing
{
    /// <summary>
    /// 作用域事件总线的公开合同。
    ///
    /// 总线按事件的运行时类型分发消息。具体总线实例由所属会话持有，
    /// 因此该接口本身不提供全局静态访问，也不负责决定会话何时结束。
    /// </summary>
    public interface IBFEventBus : IDisposable
    {
        /// <summary>
        /// 订阅一种事件，并返回只对应本次订阅的清理令牌。
        ///
        /// 同一个回调可以重复订阅；每次调用都会产生独立令牌，令牌之间互不影响。
        /// </summary>
        /// <typeparam name="TEvent">要订阅的事件数据类型。</typeparam>
        /// <param name="listener">收到事件后同步执行的回调。</param>
        /// <returns>仅代表本次订阅关系的清理令牌。</returns>
        /// <exception cref="ArgumentNullException">当 <paramref name="listener" /> 为空时抛出。</exception>
        IDisposable Subscribe<TEvent>(Action<TEvent> listener);

        /// <summary>
        /// 通过原始回调显式移除一种事件的一次匹配订阅。
        ///
        /// 当同一个回调存在多次订阅时，本方法只移除其中一次；需要精确移除时应使用订阅令牌。
        /// </summary>
        /// <typeparam name="TEvent">要取消订阅的事件数据类型。</typeparam>
        /// <param name="listener">之前传入 <see cref="Subscribe{TEvent}" /> 的回调。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="listener" /> 为空时抛出。</exception>
        void Unsubscribe<TEvent>(Action<TEvent> listener);

        /// <summary>
        /// 同步发布一种事件。
        ///
        /// 发布过程在当前调用栈内执行所有监听者；总线不排队、不切换线程，也不捕获监听者异常。
        /// </summary>
        /// <typeparam name="TEvent">要发布的事件数据类型。</typeparam>
        /// <param name="eventData">要传递给监听者的事件数据。</param>
        void Publish<TEvent>(TEvent eventData);
    }
}
