using BF.Framework.Core.Scene;

namespace BF.Game.Runtime.Battle
{
    /// <summary>
    /// 战斗场景入口，派生自 BFSceneEntry。
    /// 负责战斗场景的初始化流程，完成后通知场景就绪。
    /// </summary>
    public class BFBattleSceneEntry : BFSceneEntry
    {
        /// <summary>
        /// 是否已完成初始化。
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// 执行场景初始化流程。
        /// </summary>
        public void InitializeScene()
        {
            IsInitialized = true;
            NotifyReady();
        }

        /// <summary>
        /// 通知上层场景已就绪。
        /// </summary>
        public override void NotifyReady()
        {
            // 父类 BFSceneEntry 的 NotifyReady 是抽象方法，
            // 后续由 BFSceneService 订阅具体通知逻辑。
        }
    }
}
