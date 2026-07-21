using System;

namespace BF.Game.Runtime.Input
{
    /// <summary>
    /// [已废弃] 旧输入上下文字符串 key 常量，仅供迁移过渡参考。
    /// 新输入消费代码应直接使用 BFInputManager.Actions 的强类型 Action 访问。
    /// </summary>
    [Obsolete("迁移到 BFInputManager.Actions 的直接强类型 Action 访问。")]
    public static class BFBattleFlagInputKeys
    {
        public const string BattleTestProfile = "battle.test";

        public const string BattleContext = "battle";

        public const string GlobalOverlay = "global";
        public const string BattleCameraOverlay = "battle_camera";
        public const string UIOverlay = "ui";

        public const string BattlePoint = "battle.point";
        public const string BattleSelect = "battle.select";
        public const string BattleCancel = "battle.cancel";
        public const string BattleEndTurn = "battle.end_turn";
        public const string BattleCameraMove = "battle_camera.move";
        public const string BattleCameraZoom = "battle_camera.zoom";
        public const string GlobalPause = "global.pause";
    }
}
