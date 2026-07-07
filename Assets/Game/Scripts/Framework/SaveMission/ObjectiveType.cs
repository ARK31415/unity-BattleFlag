namespace BF.Framework.SaveMission
{
    /// <summary>
    /// 任务目标类型枚举。
    /// </summary>
    public enum ObjectiveType
    {
        /// <summary>击杀指定目标。</summary>
        Kill,
        /// <summary>收集指定物品。</summary>
        Collect,
        /// <summary>到达指定位置。</summary>
        Reach,
        /// <summary>使用指定物品。</summary>
        UseItem,
        /// <summary>赢得战斗。</summary>
        WinBattle,
        /// <summary>完成关卡。</summary>
        CompleteLevel
    }
}
