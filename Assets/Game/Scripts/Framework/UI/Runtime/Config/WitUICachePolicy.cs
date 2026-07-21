namespace Wit.Framework.UI
{
    /// <summary>
    /// 窗口关闭后的缓存策略。
    /// DestroyOnClose: 关闭时销毁 GameObject。
    /// CacheOnClose: 关闭时保持实例但不激活，下次打开复用。
    /// Permanent: 常驻实例，关闭时仅隐藏，不销毁且不离开缓存。
    /// </summary>
    public enum WitUICachePolicy
    {
        DestroyOnClose = 0,
        CacheOnClose = 10,
        Permanent = 20
    }
}
