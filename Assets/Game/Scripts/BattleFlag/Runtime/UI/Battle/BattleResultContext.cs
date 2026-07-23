using System;
using BF.Game.Runtime.Battle;

namespace BF.Game.Runtime.UI.Battle
{
    /// <summary>
    /// 战斗结果弹窗打开参数。
    /// </summary>
    public sealed class BattleResultContext
    {
        public BattleResultContext(BattleResult result, Action closeRequested = null)
        {
            Result = result;
            CloseRequested = closeRequested;
        }

        public BattleResult Result { get; }
        public Action CloseRequested { get; }
        public bool IsVictory => Result != null && Result.IsPlayerVictory;
    }
}
