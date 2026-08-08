using BF.Game.Battle.Domain;
using BF.Game.Battle.Domain.Events;
using BF.Game.Battle.Domain.Units;
using BF.Game.Battle.Rules.Units;
using BF.Game.Runtime.Battle.Factory;
using BF.Game.Runtime.Battle.Managers;
using BF.Game.Runtime.Battle.Units;
using NUnit.Framework;
using Pathfinding;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;
using DomainUnitFaction = BF.Game.Battle.Domain.Events.BFUnitFaction;
using DomainUnitRole = BF.Game.Battle.Domain.Units.BFUnitRole;

namespace BF.Game.Tests.EditMode.Battle
{
    /// <summary>
    /// 验证 3.2 行动提交边界：RegisterUnit 拒绝未绑定、Wait 完整行为、
    /// 命中前/命中后禁用差异、棋盘同步失败处理和领域事件唯一发布。
    /// </summary>
    public sealed class BFBattleUnitManagerActionTests
    {
        private readonly List<GameObject> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (var index = 0; index < _createdObjects.Count; index++)
            {
                if (_createdObjects[index] != null)
                    UnityEngine.Object.DestroyImmediate(_createdObjects[index]);
            }

            _createdObjects.Clear();

            foreach (var manager in UnityEngine.Object.FindObjectsByType<BFBattleUnitManager>(FindObjectsSortMode.None))
            {
                UnityEngine.Object.DestroyImmediate(manager.gameObject);
            }

            foreach (var astar in UnityEngine.Object.FindObjectsByType<AstarPath>(FindObjectsSortMode.None))
            {
                UnityEngine.Object.DestroyImmediate(astar.gameObject);
            }

            foreach (var turnManager in UnityEngine.Object.FindObjectsByType<BFBattleTurnManager>(FindObjectsSortMode.None))
            {
                UnityEngine.Object.DestroyImmediate(turnManager.gameObject);
            }
        }

        [Test]
        public void RegisterUnit_RejectsUnboundRuntime()
        {
            var manager = CreateManager();
            var unbound = CreateUnit("Unbound Unit");

            manager.RegisterUnit(unbound);

            Assert.That(manager.AllUnits, Has.No.Member(unbound));
        }

        [Test]
        public void RegisterUnit_RejectsBoundRuntimeWithoutBattleSession()
        {
            var manager = CreateManager();
            var unit = CreateUnit("Bound Without Session");
            var state = new BFUnitState(
                "profile-unit",
                "runtime-unit",
                DomainUnitFaction.Player,
                DomainUnitRole.Warrior,
                BFUnitTier.Normal,
                new BFUnitAttributes(20, 5, 8),
                new BFGridPosition(0, 0));
            unit.BindRuleState(
                state,
                null,
                "Bound Without Session",
                new BFBattleUnitHandle("no-session", state.RuntimeId));

            manager.RegisterUnit(unit);

            Assert.That(manager.AllUnits, Has.No.Member(unit));
        }

        [Test]
        public void CheckBattleEndCondition_RejectsMissingBattleSession()
        {
            var manager = CreateManager();
            LogAssert.Expect(
                UnityEngine.LogType.Warning,
                "[BFBattleUnitManager] Cannot evaluate battle end without a BattleSession.");

            manager.CheckBattleEndCondition();

            Assert.That(manager.Result, Is.Null);
        }

        [Test]
        public void Wait_ConsumesAllActionPointsAndPublishesDomainEventOnce()
        {
            var battle = CreateBattle(out var unit, out var state, out var session, out var manager);
            Assert.That(manager.TrySelectUnit(unit), Is.True);

            var waitedCount = 0;
            var subscription = session.Subscribe<BFUnitWaitedEvent>(_ => waitedCount++);

            Assert.That(manager.TryWaitSelectedUnit(), Is.True);
            Assert.That(state.Attributes.RemainingActionPoints, Is.EqualTo(0));
            Assert.That(unit.Stats.RemainingActionPoints, Is.EqualTo(0));
            Assert.That(waitedCount, Is.EqualTo(1));

            subscription.Dispose();
        }

        [Test]
        public void Wait_ReturnsFalseWhenActionPointsAreZero()
        {
            var battle = CreateBattle(out var unit, out var state, out var session, out var manager);
            var rules = new BFUnitStateRules(battle.Context);
            Assert.That(rules.TryConsumeActionPoints(state.RuntimeId, 5), Is.True);
            unit.RefreshRuleStateProjection();
            Assert.That(manager.TrySelectUnit(unit), Is.True);

            var waitedCount = 0;
            var subscription = session.Subscribe<BFUnitWaitedEvent>(_ => waitedCount++);

            Assert.That(manager.TryWaitSelectedUnit(), Is.False);
            Assert.That(state.Attributes.RemainingActionPoints, Is.EqualTo(0));
            Assert.That(waitedCount, Is.EqualTo(0));

            subscription.Dispose();
        }

        [Test]
        public void CleanupInterruptedActions_PreHitDoesNotConsumeActionPointsOrDamage()
        {
            var battle = CreateBattle(out var attacker, out var attackerState, out var session, out var manager);
            var targetState = CreateTargetState(battle.Context, "runtime-target");
            var target = CreateUnit("Target");
            target.BindRuleState(
                targetState,
                null,
                "Target",
                new BFBattleUnitHandle("action-test", targetState.RuntimeId));
            _createdObjects.Add(target.gameObject);

            var rules = new BFUnitStateRules(battle.Context);
            var startResult = rules.TryStartAttack(
                new AttackRequest(attackerState.RuntimeId, targetState.RuntimeId, 2));
            Assert.That(startResult.Succeeded, Is.True, startResult.FailureReason);
            attacker.Combat.BeginQueuedAttack(target);
            manager.RegisterUnit(attacker);

            manager.CleanupInterruptedActions();

            Assert.That(attacker.Combat.HasQueuedAttack, Is.False);
            Assert.That(attackerState.ActionState, Is.EqualTo(BFUnit_ActionState.Idle));
            Assert.That(attackerState.Attributes.RemainingActionPoints, Is.EqualTo(5));
            Assert.That(targetState.Attributes.CurrentHP, Is.EqualTo(20));
        }

        [Test]
        public void CleanupInterruptedActions_PostHitKeepsCommittedResultWithoutReSettlement()
        {
            var battle = CreateBattle(out var attacker, out var attackerState, out var session, out var manager);
            var targetState = CreateTargetState(battle.Context, "runtime-target");
            var target = CreateUnit("Target");
            target.BindRuleState(
                targetState,
                null,
                "Target",
                new BFBattleUnitHandle("action-test", targetState.RuntimeId));
            _createdObjects.Add(target.gameObject);

            var rules = new BFUnitStateRules(battle.Context);
            var startResult = rules.TryStartAttack(
                new AttackRequest(attackerState.RuntimeId, targetState.RuntimeId, 2));
            Assert.That(startResult.Succeeded, Is.True, startResult.FailureReason);
            var resolveResult = rules.TryResolveAttack(
                new AttackRequest(attackerState.RuntimeId, targetState.RuntimeId, 2));
            Assert.That(resolveResult.Succeeded, Is.True, resolveResult.FailureReason);
            attacker.Combat.BeginQueuedAttack(target);
            manager.RegisterUnit(attacker);

            manager.CleanupInterruptedActions();

            // 已提交的 AP 与伤害不回滚；规则行动状态恢复为可行动状态。
            Assert.That(attackerState.Attributes.RemainingActionPoints, Is.EqualTo(3));
            Assert.That(targetState.Attributes.CurrentHP, Is.EqualTo(12));
            Assert.That(attackerState.ActionState, Is.EqualTo(BFUnit_ActionState.Idle));
            Assert.That(attacker.Combat.HasQueuedAttack, Is.False);
        }

        [Test]
        public void CompleteMove_BoardSyncFailure_KeepsRuleCommitAndReportsNoPresentationSuccess()
        {
            var battle = CreateBattle(out var unit, out var state, out var session, out var manager);
            var blockerState = CreateTargetState(battle.Context, "runtime-blocker", new BFGridPosition(1, 1));
            var blocker = CreateUnit("Blocker");
            blocker.BindRuleState(
                blockerState,
                null,
                "Blocker",
                new BFBattleUnitHandle("action-test", blockerState.RuntimeId));
            _createdObjects.Add(blocker.gameObject);

            var board = CreateScannedBoard(3, 3);
            manager.SetBoardForTest(board);
            Assert.That(board.TryOccupyCell(new Vector2Int(0, 0), state.RuntimeId), Is.True);
            Assert.That(board.TryOccupyCell(new Vector2Int(1, 1), blockerState.RuntimeId), Is.True);
            manager.RegisterUnit(unit);
            unit.MovementHandler = board;

            var movedCount = 0;
            var subscription = session.Subscribe<BFUnitMovedEvent>(_ => movedCount++);
            var moveCompletedCount = 0;
            manager.OnUnitMoveCompleted += _ => moveCompletedCount++;

            UnityEngine.TestTools.LogAssert.Expect(
                UnityEngine.LogType.Error,
                new System.Text.RegularExpressions.Regex("棋盘占用同步失败"));

            var committed = manager.CompleteMove(
                unit,
                new Vector2Int(0, 0),
                new Vector2Int(1, 1),
                1,
                refreshPlayerLegalActions: false,
                clearSelectionWhenActed: false,
                out var boardSyncFailed);

            Assert.That(committed, Is.True);
            Assert.That(boardSyncFailed, Is.True);
            Assert.That(state.GridPosition, Is.EqualTo(new BFGridPosition(1, 1)));
            Assert.That(state.Attributes.RemainingActionPoints, Is.EqualTo(4));
            Assert.That(movedCount, Is.EqualTo(1));
            Assert.That(moveCompletedCount, Is.EqualTo(0));
            Assert.That(manager.TrySelectUnit(unit), Is.False);
            Assert.That(manager.IsBoardSyncFaulted, Is.True);
            Assert.That(manager.TryRecoverBoardSync(), Is.False);
            Assert.That(board.ReleaseCell(new Vector2Int(0, 0), state.RuntimeId), Is.True);
            Assert.That(board.ReleaseCell(new Vector2Int(1, 1), blockerState.RuntimeId), Is.True);
            Assert.That(board.TryOccupyCell(new Vector2Int(1, 1), state.RuntimeId), Is.True);
            Assert.That(manager.TryRecoverBoardSync(), Is.True);
            Assert.That(manager.IsBoardSyncFaulted, Is.False);

            subscription.Dispose();
        }

        [Test]
        public void CompleteMove_PublishesMovedDomainEventOnceOnSuccess()
        {
            var battle = CreateBattle(out var unit, out var state, out var session, out var manager);
            var board = CreateScannedBoard(3, 3);
            manager.SetBoardForTest(board);
            Assert.That(board.TryOccupyCell(new Vector2Int(0, 0), state.RuntimeId), Is.True);
            manager.RegisterUnit(unit);
            unit.MovementHandler = board;

            var movedCount = 0;
            var subscription = session.Subscribe<BFUnitMovedEvent>(_ => movedCount++);
            var moveCompletedCount = 0;
            manager.OnUnitMoveCompleted += _ => moveCompletedCount++;

            var committed = manager.CompleteMove(
                unit,
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                1,
                refreshPlayerLegalActions: false,
                clearSelectionWhenActed: false,
                out var boardSyncFailed);

            Assert.That(committed, Is.True);
            Assert.That(boardSyncFailed, Is.False);
            Assert.That(state.GridPosition, Is.EqualTo(new BFGridPosition(1, 0)));
            Assert.That(state.Attributes.RemainingActionPoints, Is.EqualTo(4));
            Assert.That(movedCount, Is.EqualTo(1));
            Assert.That(moveCompletedCount, Is.EqualTo(1));

            subscription.Dispose();
        }

        [Test]
        public void UnitRuntime_CleanupDisabledRuntime_ClearsCombatAndPresentationWithoutTouchingRuleState()
        {
            var battle = CreateBattle(out var unit, out var state, out var session, out var manager);
            var targetState = CreateTargetState(battle.Context, "runtime-target");
            var target = CreateUnit("Target");
            target.BindRuleState(
                targetState,
                null,
                "Target",
                new BFBattleUnitHandle("action-test", targetState.RuntimeId));
            _createdObjects.Add(target.gameObject);

            var rules = new BFUnitStateRules(battle.Context);
            var startResult = rules.TryStartAttack(
                new AttackRequest(state.RuntimeId, targetState.RuntimeId, 2));
            Assert.That(startResult.Succeeded, Is.True, startResult.FailureReason);
            Assert.That(unit.Combat.BeginQueuedAttack(target), Is.True);
            unit.StateMachine.ChangeState(unit.StateMachine.AttackState);

            unit.CleanupDisabledRuntime();

            Assert.That(unit.Combat.HasQueuedAttack, Is.False);
            Assert.That(unit.StateMachine.CurrentState, Is.TypeOf<BFUnit_PresentationIdleState>());
            // Runtime 自身清理不修改规则状态；规则状态由适配层通过规则入口恢复。
            Assert.That(state.ActionState, Is.EqualTo(BFUnit_ActionState.Attack));
        }

        private static BFBattleUnitManager CreateManager()
        {
            return new GameObject("UnitManager").AddComponent<BFBattleUnitManager>();
        }

        private static UnitRuntime CreateUnit(string name)
        {
            var gameObject = new GameObject(name);
            return gameObject.AddComponent<UnitRuntime>();
        }

        private static BattleFixture CreateBattle(
            out UnitRuntime unit,
            out BFUnitState state,
            out BFBattleSession session,
            out BFBattleUnitManager manager)
        {
            var context = new BFBattleContext("action-test");
            state = new BFUnitState(
                "profile-attacker",
                "runtime-attacker",
                DomainUnitFaction.Player,
                DomainUnitRole.Warrior,
                BFUnitTier.Normal,
                1,
                new BFUnitAttributes(20, 5, 8, baseAttackRange: 2, baseAttackCost: 2),
                new BFGridPosition(0, 0));
            Assert.That(context.TryRegisterUnit(state), Is.True);

            session = new BFBattleSession(context);
            session.Start();
            manager = CreateManager();
            var turnManager = new GameObject("ActionTest.TurnManager").AddComponent<BFBattleTurnManager>();
            SetPrivateField(manager, "_turnManager", turnManager);
            SetPrivateField(turnManager, "_unitManager", manager);
            turnManager.SetBattleSession(session);

            unit = CreateUnit("Attacker");
            unit.BindRuleState(
                state,
                null,
                "Attacker",
                new BFBattleUnitHandle("action-test", state.RuntimeId));
            manager.SetBattleSession(session);
            manager.RegisterUnit(unit);
            turnManager.StartBattle();
            return new BattleFixture(context, session);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private test dependency field: {fieldName}");
            field.SetValue(target, value);
        }

        private static BFUnitState CreateTargetState(
            BFBattleContext context,
            string runtimeId,
            BFGridPosition? gridPosition = null)
        {
            var targetState = new BFUnitState(
                "profile-target",
                runtimeId,
                DomainUnitFaction.Enemy,
                DomainUnitRole.Warrior,
                BFUnitTier.Normal,
                1,
                new BFUnitAttributes(20, 5, 8),
                gridPosition ?? new BFGridPosition(2, 0));
            Assert.That(context.TryRegisterUnit(targetState), Is.True);
            return targetState;
        }

        private static BFBattleBoardManager CreateScannedBoard(int width, int height)
        {
            var boardObject = new GameObject("Board");
            var astar = boardObject.AddComponent<AstarPath>();
            var grid = astar.data.AddGraph(typeof(GridGraph)) as GridGraph;
            Assert.That(grid, Is.Not.Null);

            grid.SetDimensions(width, height, 1f);
            grid.center = new Vector3(width * 0.5f - 0.5f, height * 0.5f - 0.5f, 0f);
            grid.is2D = true;
            grid.collision.use2D = true;
            grid.collision.heightCheck = false;
            grid.neighbours = NumNeighbours.Four;
            astar.Scan();

            var manager = boardObject.AddComponent<BFBattleBoardManager>();
            var awake = typeof(BFBattleBoardManager).GetMethod(
                "Awake",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(awake, Is.Not.Null);
            awake.Invoke(manager, null);
            return manager;
        }

        private readonly struct BattleFixture
        {
            public BattleFixture(BFBattleContext context, BFBattleSession session)
            {
                Context = context;
                Session = session;
            }

            public BFBattleContext Context { get; }
            public BFBattleSession Session { get; }
        }
    }
}
