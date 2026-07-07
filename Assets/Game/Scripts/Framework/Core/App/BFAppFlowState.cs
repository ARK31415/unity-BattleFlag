namespace BF.Framework.Core.App
{
    public enum BFAppFlowState
    {
        None,
        Boot,
        MainMenu,
        LevelSelect,
        LoadingBattle,
        InBattle,
        /// <summary>
        /// P2 系统验证关。
        /// </summary>
        SystemValidation,
        /// <summary>
        /// P2 战斗结算。
        /// </summary>
        BattleSettlement
    }
}
