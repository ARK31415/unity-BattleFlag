using BF.Game.Runtime.Battle.Managers;
using NUnit.Framework;
using UnityEngine;

namespace BF.Game.Tests.EditMode.Battle
{
    /// <summary>
    /// 验证回合管理器没有 BattleSession 时不再启动正式战斗兼容流程。
    /// </summary>
    public sealed class BFBattleTurnManagerTests
    {
        [TearDown]
        public void TearDown()
        {
            foreach (var manager in Object.FindObjectsByType<BFBattleTurnManager>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(manager.gameObject);
            }
        }

        [Test]
        public void StartBattleWithoutSessionDoesNotEnterPlayerTurn()
        {
            var manager = new GameObject("TurnManager").AddComponent<BFBattleTurnManager>();

            manager.StartBattle();

            Assert.That(manager.HasBattleSession, Is.False);
            Assert.That(manager.CurrentPhase, Is.EqualTo(BattlePhase.None));
            Assert.That(manager.TurnNumber, Is.EqualTo(0));
            Assert.That(manager.RoundNumber, Is.EqualTo(0));
        }
    }
}
