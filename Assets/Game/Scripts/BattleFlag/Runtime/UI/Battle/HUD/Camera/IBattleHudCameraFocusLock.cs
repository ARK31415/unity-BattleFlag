namespace BF.Game.Runtime.UI.Battle.HUD.Camera
{
    /// <summary>
    /// BattleHUD 子界面使用的相机聚焦锁定合同。
    /// HUD 只调用该接口建立和释放操作上下文，不依赖具体 Cinemachine 或摄像机实现。
    /// </summary>
    public interface IBattleHudCameraFocusLock
    {
        /// <summary>
        /// 通过表现侧可识别的 RuntimeId 聚焦单位。
        /// HUD 不直接接收或传递 UnitRuntime；RuntimeId 到 Unity 对象的解析由适配层实现。
        /// </summary>
        void FocusAndLock(string runtimeId);
        void ReleaseLock();
    }
}
