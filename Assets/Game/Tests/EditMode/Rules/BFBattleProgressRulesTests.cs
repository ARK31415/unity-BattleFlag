using BF.Game.Battle.Domain;
using BF.Game.Battle.Domain.Events;
using BF.Game.Battle.Rules.Battle;
using NUnit.Framework;

namespace BF.Game.Tests.EditMode.Rules
{
    /// <summary>
    /// 验证战斗进度规则先更新 Context，再发布领域事实事件。
    /// </summary>
    public sealed class BFBattleProgressRulesTests
    {
        [Test]
        public void StartBattlePublishesAfterSessionEntersRunning()
        {
            using var session = new BFBattleSession(new BFBattleContext("battle-progress-test"));
            bool observedRunning = false;
            session.Subscribe<BFBattleStartedEvent>(_ =>
            {
                observedRunning = session.State == BFBattleSessionState.Running;
            });

            new BFBattleProgressRules(session).StartBattle();

            Assert.That(observedRunning, Is.True);
        }

        [Test]
        public void UpdateProgressPublishesAfterContextIsUpdated()
        {
            using var session = new BFBattleSession(new BFBattleContext("battle-progress-test"));
            new BFBattleProgressRules(session).StartBattle();
            bool observedUpdatedContext = false;
            session.Subscribe<BFBattlePhaseChangedEvent>(eventData =>
            {
                observedUpdatedContext = session.Context.CurrentPhase == eventData.CurrentPhase
                                          && session.Context.TurnNumber == eventData.TurnNumber
                                          && session.Context.RoundNumber == eventData.RoundNumber;
            });

            bool updated = new BFBattleProgressRules(session).TryUpdateProgress(
                BFBattlePhase.PlayerTurn,
                1,
                0);

            Assert.That(updated, Is.True);
            Assert.That(observedUpdatedContext, Is.True);
        }

        [Test]
        public void CompleteBattleRejectsResultFromAnotherBattle()
        {
            using var session = new BFBattleSession(new BFBattleContext("battle-progress-test"));
            var rules = new BFBattleProgressRules(session);
            rules.StartBattle();

            Assert.Throws<System.ArgumentException>(() => rules.CompleteBattle(
                BattleResult.Victory("another-battle", 1)));
            Assert.That(session.State, Is.EqualTo(BFBattleSessionState.Running));
            Assert.That(session.Context.Result, Is.Null);
        }

        [Test]
        public void CompleteBattlePublishesBeforeSessionBecomesCompleted()
        {
            using var session = new BFBattleSession(new BFBattleContext("battle-progress-test"));
            var rules = new BFBattleProgressRules(session);
            rules.StartBattle();

            bool observedRunning = false;
            bool observedResult = false;
            session.Subscribe<BFBattleCompletedEvent>(_ =>
            {
                observedRunning = session.State == BFBattleSessionState.Running;
                observedResult = session.Context.Result != null;
            });

            rules.CompleteBattle(BattleResult.Victory("battle-progress-test", 2));

            Assert.That(observedRunning, Is.True);
            Assert.That(observedResult, Is.True);
            Assert.That(session.State, Is.EqualTo(BFBattleSessionState.Completed));
        }
    }
}
