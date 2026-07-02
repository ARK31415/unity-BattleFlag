using NUnit.Framework;
using BF.Game.Runtime.Battle;

namespace BF.Game.Tests.EditMode.Battle
{
    /// <summary>
    /// BFBattleSceneEntry 的 EditMode 单元测试。
    /// 验证战斗场景入口的初始化契约。
    /// </summary>
    public class BFBattleSceneEntryTests
    {
        [Test]
        public void InitializeScene_MarksEntryInitialized()
        {
            var entry = new BFBattleSceneEntry();
            entry.InitializeScene();
            Assert.That(entry.IsInitialized, Is.True);
        }
    }
}
