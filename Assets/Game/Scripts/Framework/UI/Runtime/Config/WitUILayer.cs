namespace Wit.Framework.UI
{
    /// <summary>
    /// UI 层级定义，数值越大渲染越靠前。
    /// HUD: 常驻信息层，不进入返回栈。
    /// Screen: 页面级窗口，进入 ScreenStack。
    /// Popup: 弹窗级窗口，进入 PopupStack。
    /// Overlay: 全局阻塞层（Loading、转场遮罩等）。
    /// Toast: 轻提示层，不进入返回栈且不阻断交互。
    /// </summary>
    public enum WitUILayer
    {
        HUD = 0,
        Screen = 10,
        Popup = 20,
        Overlay = 30,
        Toast = 40
    }
}
