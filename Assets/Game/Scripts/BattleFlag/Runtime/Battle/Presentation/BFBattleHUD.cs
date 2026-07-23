using BF.Game.Runtime.UI.Battle;

namespace BF.Game.Runtime.Battle.Presentation
{
    /// <summary>
    /// 旧场景兼容壳。正式战斗 HUD 逻辑已迁移到 BattleHudView，后续场景应直接挂载 BattleHudView。
    /// </summary>
    public class BFBattleHUD : BattleHudView { }
}
