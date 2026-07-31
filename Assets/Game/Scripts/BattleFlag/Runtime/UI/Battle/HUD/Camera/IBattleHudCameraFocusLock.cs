using BF.Game.Runtime.Battle.Units;

namespace BF.Game.Runtime.UI.Battle.HUD.Camera
{
    /// <summary>
    /// BattleHUD 子界面使用的相机聚焦锁定合同。
    /// HUD 只调用该接口建立和释放操作上下文，不依赖具体 Cinemachine 或摄像机实现。
    /// </summary>
    public interface IBattleHudCameraFocusLock
    {
        void FocusAndLock(UnitRuntime unit);
        void ReleaseLock();
    }
}
