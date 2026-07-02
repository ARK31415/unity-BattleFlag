namespace BF.Game.Runtime.Battle
{
    /// <summary>
    /// 战斗上下文数据，承载一次战斗的标识与配置。
    /// </summary>
    public class BFBattleContext
    {
        /// <summary>
        /// 战斗唯一标识，用于存档与日志追踪。
        /// </summary>
        public string BattleId { get; set; } = "TestBattle";
    }
}
