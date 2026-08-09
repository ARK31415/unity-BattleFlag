namespace BF.Game.Eventing
{
    /// <summary>
    /// 总线内部事件通道的非泛型访问合同。
    /// </summary>
    internal interface IBFEventChannel
    {
        /// <summary>
        /// 指示通道当前是否没有监听者。
        /// </summary>
        bool IsEmpty { get; }

        /// <summary>
        /// 清除通道中的全部监听者和订阅节点。
        /// </summary>
        void Clear();
    }
}
