namespace Wit.Framework.UI
{
    /// <summary>
    /// 窗口打开操作的结果。
    /// Success 表示窗口已成功实例化并打开，View 指向已打开的窗口实例。
    /// Failure 表示打开失败，Error 包含失败原因。
    /// </summary>
    public sealed class WitUIOpenResult
    {
        public bool Succeeded { get; private set; }
        public WitUIView View { get; private set; }
        public string Error { get; private set; }

        public static WitUIOpenResult Success(WitUIView view)
        {
            return new WitUIOpenResult { Succeeded = true, View = view };
        }

        public static WitUIOpenResult Failure(string error)
        {
            return new WitUIOpenResult { Succeeded = false, Error = error };
        }
    }
}
