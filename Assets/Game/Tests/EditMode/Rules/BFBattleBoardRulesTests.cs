using System.Collections.Generic;
using BF.Game.Battle.Domain;
using BF.Game.Battle.Domain.Events;
using BF.Game.Battle.Domain.Units;
using BF.Game.Battle.Rules.Battle;
using BF.Game.Battle.Rules.Units;
using NUnit.Framework;

namespace BF.Game.Tests.EditMode.Rules
{
    /// <summary>
    /// 验证最小棋盘规则服务只依赖纯规则数据，并从 Context 推导动态占用。
    /// A*、Unity 节点和 Transform 不参与这些测试。
    /// </summary>
    public sealed class BFBattleBoardRulesTests
    {
        [Test]
        public void SpawnValidationRejectsOutOfBoundsAndStaticBlockedCells()
        {
            var context = new BFBattleContext("board-rules-test");
            using var rules = CreateRules(context, new BFGridPosition(1, 1));

            Assert.That(
                rules.ValidateSpawnPosition(new BFGridPosition(-1, 0)).Succeeded,
                Is.False);
            Assert.That(
                rules.ValidateSpawnPosition(new BFGridPosition(1, 1)).Succeeded,
                Is.False);
        }

        [Test]
        public void SpawnValidationDerivesAliveOccupancyFromContext()
        {
            var context = new BFBattleContext("board-rules-occupancy-test");
            var unit = CreateUnit("runtime-blocker", new BFGridPosition(1, 1));
            Assert.That(context.TryRegisterUnit(unit), Is.True);
            using var rules = CreateRules(context);

            Assert.That(
                rules.ValidateSpawnPosition(new BFGridPosition(1, 1)).Succeeded,
                Is.False);

            var unitRules = new BFUnitStateRules(context);
            Assert.That(unitRules.TryApplyDamage(unit.RuntimeId, 20, out var wasKilled), Is.True);
            Assert.That(wasKilled, Is.True);

            Assert.That(
                rules.ValidateSpawnPosition(new BFGridPosition(1, 1)).Succeeded,
                Is.True);
        }

        [Test]
        public void CandidatePathUsesFourDirectionsAndOneCostPerCell()
        {
            var context = new BFBattleContext("board-rules-path-test");
            var unit = CreateUnit("runtime-mover", new BFGridPosition(0, 0));
            Assert.That(context.TryRegisterUnit(unit), Is.True);
            using var rules = CreateRules(context);

            var result = rules.ValidateCandidatePath(
                unit.RuntimeId,
                new List<BFGridPosition>
                {
                    new BFGridPosition(1, 0),
                    new BFGridPosition(1, 1),
                    new BFGridPosition(2, 1)
                });

            Assert.That(result.Succeeded, Is.True, result.FailureReason);
            Assert.That(result.ActionPointCost, Is.EqualTo(3));
        }

        [Test]
        public void CandidatePathRejectsDiagonalRepeatedAndOccupiedCells()
        {
            var context = new BFBattleContext("board-rules-invalid-path-test");
            var mover = CreateUnit("runtime-mover", new BFGridPosition(0, 0));
            var blocker = CreateUnit("runtime-blocker", new BFGridPosition(1, 0));
            Assert.That(context.TryRegisterUnit(mover), Is.True);
            Assert.That(context.TryRegisterUnit(blocker), Is.True);
            using var rules = CreateRules(context);

            Assert.That(
                rules.ValidateCandidatePath(
                    mover.RuntimeId,
                    new List<BFGridPosition> { new BFGridPosition(1, 1) }).Succeeded,
                Is.False);
            Assert.That(
                rules.ValidateCandidatePath(
                    mover.RuntimeId,
                    new List<BFGridPosition>
                    {
                        new BFGridPosition(1, 0),
                        new BFGridPosition(0, 0)
                    }).Succeeded,
                Is.False);
            Assert.That(
                rules.ValidateCandidatePath(
                    mover.RuntimeId,
                    new List<BFGridPosition>
                    {
                        new BFGridPosition(1, 0)
                    }).Succeeded,
                Is.False);
        }

        [Test]
        public void CandidatePathRejectsRepeatedCellIndependentlyOfOccupancy()
        {
            var context = new BFBattleContext("board-rules-repeated-path-test");
            var mover = CreateUnit("runtime-mover", new BFGridPosition(0, 0));
            Assert.That(context.TryRegisterUnit(mover), Is.True);
            using var rules = CreateRules(context);

            var result = rules.ValidateCandidatePath(
                mover.RuntimeId,
                new List<BFGridPosition>
                {
                    new BFGridPosition(1, 0),
                    new BFGridPosition(1, 1),
                    new BFGridPosition(1, 0)
                });

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Does.Contain("重复"));
        }

        [Test]
        public void CandidatePathRejectsTargetMismatchWithoutMutatingUnitState()
        {
            var context = new BFBattleContext("board-rules-target-test");
            var mover = CreateUnit("runtime-mover", new BFGridPosition(0, 0));
            Assert.That(context.TryRegisterUnit(mover), Is.True);
            using var rules = CreateRules(context);

            var result = rules.ValidateCandidatePath(
                mover.RuntimeId,
                new BFGridPosition(2, 2),
                new List<BFGridPosition> { new BFGridPosition(1, 0) });

            Assert.That(result.Succeeded, Is.False);
            Assert.That(mover.GridPosition, Is.EqualTo(new BFGridPosition(0, 0)));
            Assert.That(mover.Attributes.RemainingActionPoints, Is.EqualTo(5));
        }

        [Test]
        public void CandidatePathRejectsInvalidStartBeforeInspectingCandidate()
        {
            var outOfBoundsContext = new BFBattleContext("board-rules-invalid-start-bounds-test");
            var outOfBoundsUnit = CreateUnit("runtime-out-of-bounds", new BFGridPosition(-1, 0));
            Assert.That(outOfBoundsContext.TryRegisterUnit(outOfBoundsUnit), Is.True);
            using (var outOfBoundsRules = CreateRules(outOfBoundsContext))
            {
                Assert.That(
                    outOfBoundsRules.ValidateCandidatePath(
                        outOfBoundsUnit.RuntimeId,
                        new List<BFGridPosition> { new BFGridPosition(0, 0) }).Succeeded,
                    Is.False);
            }

            var blockedContext = new BFBattleContext("board-rules-invalid-start-blocked-test");
            var blockedUnit = CreateUnit("runtime-blocked-start", new BFGridPosition(1, 1));
            Assert.That(blockedContext.TryRegisterUnit(blockedUnit), Is.True);
            using (var blockedRules = CreateRules(blockedContext, new BFGridPosition(1, 1)))
            {
                Assert.That(
                    blockedRules.ValidateCandidatePath(
                        blockedUnit.RuntimeId,
                        new List<BFGridPosition> { new BFGridPosition(1, 2) }).Succeeded,
                    Is.False);
            }
        }

        [Test]
        public void CandidatePathRejectsMissingOrDeadUnit()
        {
            var context = new BFBattleContext("board-rules-dead-path-test");
            using var rules = CreateRules(context);

            Assert.That(
                rules.ValidateCandidatePath(
                    "missing",
                    new List<BFGridPosition> { new BFGridPosition(1, 0) }).Succeeded,
                Is.False);

            var deadUnit = CreateUnit("runtime-dead", new BFGridPosition(0, 0));
            Assert.That(context.TryRegisterUnit(deadUnit), Is.True);
            var unitRules = new BFUnitStateRules(context);
            Assert.That(unitRules.TryApplyDamage(deadUnit.RuntimeId, 20, out _), Is.True);
            Assert.That(
                rules.ValidateCandidatePath(
                    deadUnit.RuntimeId,
                    new List<BFGridPosition> { new BFGridPosition(1, 0) }).Succeeded,
                Is.False);
        }

        [Test]
        public void DisposeRejectsFurtherRuleQueries()
        {
            var context = new BFBattleContext("board-rules-dispose-test");
            var rules = CreateRules(context);

            rules.Dispose();

            Assert.Throws<System.ObjectDisposedException>(
                () => rules.ValidateSpawnPosition(new BFGridPosition(0, 0)));
        }

        private static BFBattleBoardRules CreateRules(
            BFBattleContext context,
            params BFGridPosition[] blockedCells)
        {
            return new BFBattleBoardRules(
                new BFBoardTopologySnapshot(4, 4, blockedCells),
                context);
        }

        private static BFUnitState CreateUnit(string runtimeId, BFGridPosition position)
        {
            return new BFUnitState(
                "profile-" + runtimeId,
                runtimeId,
                BFUnitFaction.Player,
                BFUnitRole.Warrior,
                BFUnitTier.Normal,
                new BFUnitAttributes(20, 5, 8),
                position);
        }
    }
}
