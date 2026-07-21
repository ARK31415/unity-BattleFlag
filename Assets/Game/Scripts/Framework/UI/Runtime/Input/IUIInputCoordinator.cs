namespace Wit.Framework.UI
{
    /// <summary>
    /// UI 框架与输入系统之间的适配边界。
    /// 当 UI 状态变化时，框架通过该接口通知输入系统是否应阻断 gameplay 输入。
    /// </summary>
    public interface IUIInputCoordinator
    {
        /// <summary>
        /// 当 UI 状态发生变化时调用。
        /// </summary>
        /// <param name="hasBlockingUI">当前是否有模态或全局阻塞 UI 正在显示。</param>
        /// <param name="hasAnyUI">当前是否有任何 UI 窗口处于打开状态。</param>
        void OnUIStateChanged(bool hasBlockingUI, bool hasAnyUI);
    }
}
