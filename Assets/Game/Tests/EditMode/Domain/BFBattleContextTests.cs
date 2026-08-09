using BF.Game.Battle.Domain.Units;
using BF.Game.Battle.Domain.Events;
using NUnit.Framework;

namespace BF.Game.Tests.EditMode.Domain
{
    /// <summary>
    /// 验证战斗上下文对纯规则单位状态的注册、查询和生命周期数据管理。
    /// </summary>
    public sealed class BFBattleContextTests
    {
        [Test]
        public void NewContextStartsWithEmptyBattleState()
        {
            var context = new BF.Game.Battle.Domain.BFBattleContext("battle-001");

            Assert.That(context.BattleId, Is.EqualTo("battle-001"));
            Assert.That(context.CurrentPhase, Is.EqualTo(BFBattlePhase.None));
            Assert.That(context.TurnNumber, Is.Zero);
            Assert.That(context.RoundNumber, Is.Zero);
            Assert.That(context.Result, Is.Null);
        }

        [Test]
        public void ContextRegistersQueriesAndRemovesByRuntimeId()
        {
            var context = new BF.Game.Battle.Domain.BFBattleContext("battle-001");
            var unit = CreateUnit("runtime-001");

            Assert.That(context.TryRegisterUnit(unit), Is.True);
            Assert.That(context.TryGetUnit("runtime-001", out var queried), Is.True);
            Assert.That(queried, Is.SameAs(unit));
            Assert.That(context.TryRemoveUnit("runtime-001"), Is.True);
            Assert.That(context.TryGetUnit("runtime-001", out _), Is.False);
        }

        [Test]
        public void DuplicateRuntimeIdIsRejected()
        {
            var context = new BF.Game.Battle.Domain.BFBattleContext("battle-001");

            Assert.That(context.TryRegisterUnit(CreateUnit("runtime-001")), Is.True);
            Assert.That(context.TryRegisterUnit(CreateUnit("runtime-001")), Is.False);
        }

        [Test]
        public void ContextStoresPhaseTurnRoundAndResultThroughControlledOperations()
        {
            var context = new BF.Game.Battle.Domain.BFBattleContext("battle-001");
            var result = BF.Game.Battle.Domain.BattleResult.Victory("battle-001", 3);

            context.SetCurrentPhase(BFBattlePhase.PlayerTurn);
            context.SetTurnNumber(2);
            context.SetRoundNumber(1);
            context.SetResult(result);

            Assert.That(context.CurrentPhase, Is.EqualTo(BFBattlePhase.PlayerTurn));
            Assert.That(context.TurnNumber, Is.EqualTo(2));
            Assert.That(context.RoundNumber, Is.EqualTo(1));
            Assert.That(context.Result, Is.SameAs(result));
        }

        private static BFUnitState CreateUnit(string runtimeId)
        {
            return new BFUnitState(
                "profile-warrior",
                runtimeId,
                BFUnitFaction.Player,
                BFUnitRole.Warrior,
                BFUnitTier.Normal,
                new BFUnitAttributes(100, 5, 20),
                new BFGridPosition(0, 0));
        }
    }
}
