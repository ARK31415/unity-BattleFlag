using System;
using System.Collections;
using System.Collections.Generic;
using BF.Game.Battle.Domain.Events;
using BF.Game.Battle.Domain.Units;
using BF.Game.Battle.Rules.Units;
using BF.Game.Runtime.Battle;
using BF.Game.Runtime.Battle.AI;
using BF.Game.Runtime.Battle.Events;
using BF.Game.Runtime.Battle.Flow;
using BF.Game.Runtime.Battle.Input;
using BF.Game.Runtime.Battle.Managers;
using BF.Game.Runtime.Battle.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using BFBattleSession = BF.Game.Battle.Domain.BFBattleSession;
using BFBattleSessionState = BF.Game.Battle.Domain.BFBattleSessionState;
using BattleResult = BF.Game.Battle.Domain.BattleResult;

namespace BF.Game.Tests.PlayMode
{
    /// <summary>
    /// 使用默认战斗测试场景验收战斗事件总线与现有 SO 事件的真实集成链路。
    ///
    /// 测试只调用战斗管理器的生产入口；攻击伤害必须由攻击动画命中帧触发，
    /// 不在测试中直接发布事件、修改生命值或调用结算层内部回调。
    /// </summary>
    public sealed class BFBattleIntegrationTests
    {
        private const string PersistentSceneName = "BFPersistent";
        private const string BattleSceneName = "BFBattleTest";
        private const int SceneLoadTimeoutFrames = 300;
        private const int SessionReadyTimeoutFrames = 300;
        private const int ActionTimeoutFrames = 600;
        private const int BattleTimeoutFrames = 12000;
        private const int MaxDriverSteps = 240;

        private readonly List<IDisposable> _sessionSubscriptions = new();
        private readonly List<BFAttackResolvedEvent> _attackEvents = new();
        private readonly List<BFUnitDefeatedEvent> _defeatedEvents = new();
        private readonly List<BFBattleCompletedEvent> _completedEvents = new();
        private readonly List<SessionEventKind> _sessionEventOrder = new();

        private BFBattleRoot _battleRoot;
        private BFBattleSession _session;
        private Scene _battleScene;
        private string _sceneLoadError;

        private int _soBattleStartedCount;
        private int _soBattleCompletedCount;
        private int _soDamagedCount;
        private int _soKilledCount;

        private BFBattleEventSO _battleEventChannel;
        private BFTurnEventSO _turnEventChannel;
        private BFUnitEventSO _unitEventChannel;
        private UnityAction<BFBattleEventData> _battleEventListener;
        private UnityAction<BFTurnEventData> _turnEventListener;
        private UnityAction<BFUnitEventData> _unitEventListener;

        private BFBattleSessionState _completedCallbackState;
        private BattleResult _completedCallbackResult;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ResetObservationState();
            yield return LoadSceneSingle(PersistentSceneName);
            yield return LoadBattleScene();
            yield return WaitForBattleSessionReady();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DisposeSessionSubscriptions();
            UnregisterSOObservers();

            if (_battleScene.IsValid() && _battleScene.isLoaded)
            {
                var unloadOperation = SceneManager.UnloadSceneAsync(_battleScene);
                if (unloadOperation != null)
                {
                    while (!unloadOperation.isDone)
                        yield return null;
                }
            }

            _battleScene = default;
            yield return null;
        }

        [UnityTest]
        public IEnumerator DefaultBattle_CompletesThroughProductionEntryPoints()
        {
            yield return DriveBattleToCompletion();

            Assert.That(_soBattleStartedCount, Is.EqualTo(1));
            Assert.That(_soBattleCompletedCount, Is.EqualTo(1));
            Assert.That(_attackEvents.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(_defeatedEvents.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(_completedEvents.Count, Is.EqualTo(1));
            Assert.That(_soDamagedCount, Is.EqualTo(_attackEvents.Count));
            Assert.That(_soKilledCount, Is.EqualTo(_defeatedEvents.Count));
            Assert.That(
                _battleRoot.SelectionController.HasSelection,
                Is.False,
                "Completing a battle must clear the transient unit selection.");

            foreach (var unit in _battleRoot.UnitRegistry.Runtimes)
            {
                if (unit == null || unit.RuleState.IsAlive) continue;

                var deadCell = new Vector2Int(
                    unit.RuleState.GridPosition.X,
                    unit.RuleState.GridPosition.Y);
                Assert.That(_battleRoot.BoardManager.GetOccupant(deadCell), Is.Null);
                Assert.That(
                    _session.Context.TryGetUnit(unit.RuntimeId, out var retainedState),
                    Is.True);
                Assert.That(retainedState, Is.SameAs(unit.RuleState));
            }

            Assert.That(_session.State, Is.EqualTo(BFBattleSessionState.Completed));
            Assert.That(_completedCallbackState, Is.EqualTo(BFBattleSessionState.Running));
            Assert.That(_completedCallbackResult, Is.SameAs(_session.Context.Result));
            Assert.That(_completedCallbackResult, Is.Not.Null);
            Assert.That(_completedCallbackResult.HasResult, Is.True);
            Assert.That(
                _completedEvents[0].WinnerFaction,
                Is.EqualTo(_completedCallbackResult.WinnerFaction));

            // 胜负协调器必须具备幂等性：完成后的重复评估不能再次发布完成事实。
            _battleRoot.OutcomeCoordinator.Evaluate();
            Assert.That(_completedEvents.Count, Is.EqualTo(1));

            var finalAttackIndex = FindLastDefeatingAttackIndex();
            var defeatedIndex = FindNextEventIndex(SessionEventKind.UnitDefeated, finalAttackIndex);
            var completedIndex = FindNextEventIndex(SessionEventKind.BattleCompleted, defeatedIndex);

            Assert.That(finalAttackIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(defeatedIndex, Is.GreaterThan(finalAttackIndex));
            Assert.That(completedIndex, Is.GreaterThan(defeatedIndex));

            var context = _session.Context;
            yield return UnloadBattleScene();

            Assert.That(_session.State, Is.EqualTo(BFBattleSessionState.Disposed));
            Assert.Throws<ObjectDisposedException>(() => _ = context.Units);
        }

        [UnityTest]
        public IEnumerator DisablingUnitBeforeHit_DoesNotConsumeActionPointsOrDealDamage()
        {
            var player = GetAliveUnitsByFaction(UnitFaction.Player)[0];
            Assert.That(TrySelectUnit(player), Is.True);

            var target = ChooseAttackableTarget();
            if (target == null)
            {
                var destination = ChooseMoveDestination(player);
                if (destination.HasValue)
                {
                    Assert.That(_battleRoot.ActionCoordinator.TryMoveSelected(destination.Value), Is.True);
                    yield return WaitForActionUnlocked();
                }

                // 移动消耗 AP 后可能不足以攻击；结束回合让 AP 重置，下一回合直接攻击。
                _battleRoot.TurnManager.EndTurn();
                yield return WaitForPlayerTurnOrCompletion();
                Assert.That(TrySelectUnit(player), Is.True);
                target = ChooseAttackableTarget();
            }

            Assert.That(target, Is.Not.Null, "玩家单位无法进入攻击范围。");
            var apBefore = player.RuleState.Attributes.RemainingActionPoints;
            var targetHpBefore = target.RuleState.Attributes.CurrentHP;
            var attackEventCountBefore = _attackEvents.Count;

            Assert.That(_battleRoot.ActionCoordinator.TryAttackSelected(target), Is.True);

            // 命中前禁用攻击单位：攻击上下文被清理，AP 不消耗、不造成伤害、不发布攻击事实。
            player.gameObject.SetActive(false);
            yield return WaitUntil(
                () => !_battleRoot.ActionCoordinator.IsActionLocked || _session.State != BFBattleSessionState.Running,
                ActionTimeoutFrames,
                "禁用攻击单位后动作锁未释放。");

            Assert.That(player.RuleState.Attributes.RemainingActionPoints, Is.EqualTo(apBefore));
            Assert.That(player.RuleState.ActionState, Is.EqualTo(BFUnit_ActionState.Idle));
            Assert.That(target.RuleState.Attributes.CurrentHP, Is.EqualTo(targetHpBefore));
            Assert.That(_attackEvents.Count, Is.EqualTo(attackEventCountBefore));
        }

        [UnityTest]
        public IEnumerator DisablingSelectedUnit_ClearsSelectionThroughRuntimeLifecycle()
        {
            var player = GetAliveUnitsByFaction(UnitFaction.Player)[0];
            Assert.That(TrySelectUnit(player), Is.True);
            Assert.That(_battleRoot.SelectionController.HasSelection, Is.True);
            var runtimeId = player.RuntimeId;

            player.gameObject.SetActive(false);
            yield return null;

            Assert.That(_battleRoot.SelectionController.HasSelection, Is.False);
            Assert.That(_battleRoot.SelectionCoordinator.SelectedRuntimeId, Is.Null);
            Assert.That(
                _battleRoot.UnitRegistry.TryGetRuntime(runtimeId, out _),
                Is.False,
                "Disabled Runtime must be removed from the session registry.");
        }

        [UnityTest]
        public IEnumerator UnloadingBattleScene_ClosesBattleHud()
        {
            var battleHud = FindBattleHudComponent();
            Assert.That(battleHud, Is.Not.Null, "Battle HUD should be open before unloading the battle scene.");
            Assert.That(ReadBattleHudIsOpen(battleHud), Is.True);

            yield return UnloadBattleScene();

            battleHud = FindBattleHudComponent();
            Assert.That(battleHud, Is.Not.Null, "Cached battle HUD should remain inspectable after scene unload.");
            Assert.That(ReadBattleHudIsOpen(battleHud), Is.False);
        }

        [UnityTest]
        public IEnumerator GatewayFailure_DoesNotMutateRuleState()
        {
            var player = GetAliveUnitsByFaction(UnitFaction.Player)[0];
            Assert.That(TrySelectUnit(player), Is.True);

            var currentHp = player.RuleState.Attributes.CurrentHP;
            var remainingActionPoints = player.RuleState.Attributes.RemainingActionPoints;
            var gridPosition = player.RuleState.GridPosition;
            var actionState = player.RuleState.ActionState;

            Assert.That(
                _battleRoot.ActionCoordinator.TryAttack(player.RuntimeId, "missing-runtime-id"),
                Is.False);
            yield return null;

            Assert.That(player.RuleState.Attributes.CurrentHP, Is.EqualTo(currentHp));
            Assert.That(
                player.RuleState.Attributes.RemainingActionPoints,
                Is.EqualTo(remainingActionPoints));
            Assert.That(player.RuleState.GridPosition.X, Is.EqualTo(gridPosition.X));
            Assert.That(player.RuleState.GridPosition.Y, Is.EqualTo(gridPosition.Y));
            Assert.That(player.RuleState.ActionState, Is.EqualTo(actionState));
        }

        [UnityTest]
        public IEnumerator UnitQuerySnapshot_ReflectsRuleStateAfterSuccessfulAction()
        {
            var player = GetAliveUnitsByFaction(UnitFaction.Player)[0];
            Assert.That(TrySelectUnit(player), Is.True);
            Assert.That(_battleRoot.UnitQuery.TryGetSnapshot(player.RuntimeId, out var before), Is.True);

            Assert.That(_battleRoot.ActionCoordinator.TryWaitSelected(), Is.True);
            yield return null;

            Assert.That(_battleRoot.UnitQuery.TryGetSnapshot(player.RuntimeId, out var after), Is.True);
            Assert.That(after.BattleId, Is.EqualTo(_session.Context.BattleId));
            Assert.That(after.RuntimeId, Is.EqualTo(player.RuleState.RuntimeId));
            Assert.That(after.CurrentHP, Is.EqualTo(player.RuleState.Attributes.CurrentHP));
            Assert.That(
                after.RemainingActionPoints,
                Is.EqualTo(player.RuleState.Attributes.RemainingActionPoints));
            Assert.That(after.GridPosition.X, Is.EqualTo(player.RuleState.GridPosition.X));
            Assert.That(after.GridPosition.Y, Is.EqualTo(player.RuleState.GridPosition.Y));
            Assert.That(after.RemainingActionPoints, Is.Not.EqualTo(before.RemainingActionPoints));
        }

        [UnityTest]
        public IEnumerator DisablingAttackTargetBeforeHit_ClearsAttackerActionLifecycle()
        {
            var attacker = GetAliveUnitsByFaction(UnitFaction.Player)[0];
            Assert.That(TrySelectUnit(attacker), Is.True);

            var target = ChooseAttackableTarget();
            if (target == null)
            {
                var destination = ChooseMoveDestination(attacker);
                if (destination.HasValue)
                {
                    Assert.That(_battleRoot.ActionCoordinator.TryMoveSelected(destination.Value), Is.True);
                    yield return WaitForActionUnlocked();
                }

                _battleRoot.TurnManager.EndTurn();
                yield return WaitForPlayerTurnOrCompletion();
                Assert.That(TrySelectUnit(attacker), Is.True);
                target = ChooseAttackableTarget();
            }

            Assert.That(target, Is.Not.Null, "玩家单位无法进入攻击范围。");

            var apBefore = attacker.RuleState.Attributes.RemainingActionPoints;
            var targetHpBefore = target.RuleState.Attributes.CurrentHP;
            var attackEventCountBefore = _attackEvents.Count;

            Assert.That(_battleRoot.ActionCoordinator.TryAttackSelected(target), Is.True);

            // 目标在命中帧前被禁用时，必须清理攻击者的 pending attack、动作状态和行动锁。
            target.gameObject.SetActive(false);
            yield return WaitUntil(
                () => !_battleRoot.ActionCoordinator.IsActionLocked || _session.State != BFBattleSessionState.Running,
                ActionTimeoutFrames,
                "禁用攻击目标后动作锁未释放。");

            Assert.That(attacker.RuleState.Attributes.RemainingActionPoints, Is.EqualTo(apBefore));
            Assert.That(attacker.RuleState.ActionState, Is.EqualTo(BFUnit_ActionState.Idle));
            Assert.That(attacker.Combat.HasQueuedAttack, Is.False);
            Assert.That(target.RuleState.Attributes.CurrentHP, Is.EqualTo(targetHpBefore));
            Assert.That(_attackEvents.Count, Is.EqualTo(attackEventCountBefore));
        }

        [UnityTest]
        public IEnumerator WaitWithRemainingActionPoints_UsesUnifiedActionCoordinator()
        {
            var player = GetAliveUnitsByFaction(UnitFaction.Player)[0];
            Assert.That(player.RuleState.Attributes.RemainingActionPoints, Is.GreaterThan(0));
            Assert.That(TrySelectUnit(player), Is.True);

            Assert.That(_battleRoot.ActionCoordinator.TryWaitSelected(), Is.True);
            yield return null;

            Assert.That(player.RuleState.Attributes.RemainingActionPoints, Is.EqualTo(0));
            Assert.That(player.RuleState.ActionState, Is.EqualTo(BFUnit_ActionState.Idle));
            Assert.That(_battleRoot.ActionCoordinator.SelectedUnit, Is.Null);
            Assert.That(_battleRoot.ActionCoordinator.IsActionLocked, Is.False);
        }

        [UnityTest]
        public IEnumerator DefaultBattle_RejectsStaleCandidatePathWithoutRuleCommit()
        {
            var player = GetAliveUnitsByFaction(UnitFaction.Player)[0];
            var movementCoordinator = _battleRoot.MovementCoordinator;
            Assert.That(movementCoordinator, Is.Not.Null);
            Assert.That(TrySelectUnit(player), Is.True);

            var start = player.RuleState.GridPosition;
            var startCell = new Vector2Int(start.X, start.Y);
            var reachableCells = GetReachableCellsForSelected();
            Assert.That(reachableCells, Is.Not.Empty, "默认战斗场景没有可用于路径复验的移动目标。");
            var targetCell = reachableCells[0];
            var target = new BFGridPosition(targetCell.x, targetCell.y);
            var enemy = GetAliveUnitsByFaction(UnitFaction.Enemy)[0];
            var enemyStart = enemy.RuleState.GridPosition;
            var apBefore = player.RuleState.Attributes.RemainingActionPoints;
            var actionStateBefore = player.RuleState.ActionState;
            var movedEventCount = 0;
            var subscription = _session.Subscribe<BFUnitMovedEvent>(_ => movedEventCount++);
            var unitRules = new BFUnitStateRules(_session.Context);

            // 移动查询通过后，在表现期间改变规则占用；提交阶段必须重新验证候选路径并拒绝。
            LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex("候选路径被棋盘规则拒绝"));
            Assert.That(_battleRoot.ActionCoordinator.TryMoveSelected(targetCell), Is.True);
            Assert.That(unitRules.TrySetGridPosition(enemy.RuntimeId, target), Is.True);
            yield return WaitForActionUnlocked();

            subscription.Dispose();
            Assert.That(unitRules.TrySetGridPosition(enemy.RuntimeId, enemyStart), Is.True);

            Assert.That(player.RuleState.GridPosition, Is.EqualTo(start));
            Assert.That(player.RuleState.Attributes.RemainingActionPoints, Is.EqualTo(apBefore));
            Assert.That(player.RuleState.ActionState, Is.EqualTo(actionStateBefore));
            Assert.That(movedEventCount, Is.EqualTo(0));
            Assert.That(_battleRoot.BoardManager.GetOccupant(startCell), Is.EqualTo(player.RuntimeId));
            Assert.That(_battleRoot.BoardManager.GetOccupant(targetCell), Is.Null);
            Assert.That(
                _battleRoot.BoardManager.GetOccupant(new Vector2Int(enemyStart.X, enemyStart.Y)),
                Is.EqualTo(enemy.RuntimeId));
        }

        [UnityTest]
        public IEnumerator EndingPlayerTurn_ExecutesEnemyActionBeforeReturning()
        {
            var enemy = GetAliveUnitsByFaction(UnitFaction.Enemy)[0];
            var enemyActionController = _battleRoot.EnemyActionController;
            var startPosition = enemy.RuleState.GridPosition;
            var startActionPoints = enemy.RuleState.Attributes.RemainingActionPoints;

            _battleRoot.TurnManager.EndTurn();

            yield return WaitUntil(
                () => _battleRoot.TurnManager.CurrentPhase == BattlePhase.PlayerTurn &&
                      !_battleRoot.ActionCoordinator.IsActionLocked,
                ActionTimeoutFrames,
                "敌方回合没有返回玩家回合。");

            var moved = enemy.RuleState.GridPosition != startPosition;
            var spentActionPoints = enemy.RuleState.Attributes.RemainingActionPoints < startActionPoints;
            Assert.That(moved || spentActionPoints, Is.True,
                "敌方回合立即返回玩家回合，但敌方没有执行任何移动或行动点消耗。");
            Assert.That(enemyActionController.IsExecuting, Is.False,
                "敌方回合已经结束，但 AI 执行状态仍未清理。");
        }

        private UnitRuntime ChooseAttackableTarget()
        {
            var targets = _battleRoot.ActionCoordinator.GetAttackableTargets();
            return targets.Count > 0 ? ChooseTarget(targets) : null;
        }

        private List<UnitRuntime> GetAliveUnitsByFaction(UnitFaction faction)
        {
            var result = new List<UnitRuntime>();
            var domainFaction = ToDomainFaction(faction);
            foreach (var runtime in _battleRoot.UnitRegistry.Runtimes)
            {
                if (runtime == null || !runtime.gameObject.activeInHierarchy || !runtime.IsRuleBound ||
                    !runtime.RuleState.IsAlive || runtime.RuleState.Faction != domainFaction)
                    continue;

                result.Add(runtime);
            }

            return result;
        }

        private bool TrySelectUnit(UnitRuntime runtime)
        {
            return _battleRoot.SelectionCoordinator.TrySelect(runtime);
        }

        private List<Vector2Int> GetReachableCellsForSelected()
        {
            var selected = _battleRoot.ActionCoordinator.SelectedUnit;
            return selected == null
                ? new List<Vector2Int>()
                : _battleRoot.ActionCoordinator.GetReachableCellsForUnit(selected);
        }

        [UnityTest]
        public IEnumerator BattleSceneReload_DoesNotReusePreviousSessionSubscriptions()
        {
            var firstSession = _session;
            var firstContext = firstSession.Context;
            var firstBattleId = firstSession.Context.BattleId;
            var firstRuntime = new List<UnitRuntime>(_battleRoot.UnitRegistry.Runtimes)[0];
            var firstHandle = new BF.Game.Runtime.Battle.Factory.BFBattleUnitHandle(
                firstBattleId,
                firstRuntime.RuntimeId);

            Assert.That(_soBattleStartedCount, Is.EqualTo(1));
            Assert.That(firstSession.State, Is.EqualTo(BFBattleSessionState.Running));

            yield return UnloadBattleScene();

            Assert.That(firstSession.State, Is.EqualTo(BFBattleSessionState.Disposed));
            Assert.Throws<ObjectDisposedException>(() => _ = firstContext.Units);

            DisposeSessionSubscriptions();
            UnregisterSOObservers();
            ResetObservationState();

            yield return LoadBattleScene();
            yield return WaitForBattleSessionReady();

            var secondSession = _session;
            Assert.That(secondSession, Is.Not.SameAs(firstSession));
            Assert.That(secondSession.Context.BattleId, Is.Not.EqualTo(firstBattleId));
            Assert.That(secondSession.State, Is.EqualTo(BFBattleSessionState.Running));
            Assert.That(_soBattleStartedCount, Is.EqualTo(1));
            Assert.That(_battleRoot.UnitRegistry.TryGetRuntime(firstHandle, out _), Is.False);

            yield return UnloadBattleScene();

            Assert.That(secondSession.State, Is.EqualTo(BFBattleSessionState.Disposed));
        }

        private IEnumerator LoadSceneSingle(string sceneName)
        {
            var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.That(operation, Is.Not.Null, $"Unable to load scene {sceneName}.");

            var frame = 0;
            while (!operation.isDone && frame++ < SceneLoadTimeoutFrames)
                yield return null;

            Assert.That(operation.isDone, Is.True, $"Timed out loading scene {sceneName}.");
        }

        private IEnumerator LoadBattleScene()
        {
            _battleRoot = null;
            _session = null;
            _sceneLoadError = null;

            SceneManager.sceneLoaded += HandleSceneLoaded;
            var operation = SceneManager.LoadSceneAsync(BattleSceneName, LoadSceneMode.Additive);
            Assert.That(operation, Is.Not.Null, $"Unable to load scene {BattleSceneName}.");

            var frame = 0;
            while (_battleRoot == null && _sceneLoadError == null && frame++ < SceneLoadTimeoutFrames)
                yield return null;

            SceneManager.sceneLoaded -= HandleSceneLoaded;

            Assert.That(_sceneLoadError, Is.Null, _sceneLoadError);
            Assert.That(_battleRoot, Is.Not.Null, "BFBattleRoot was not found after loading BFBattleTest.");
            _battleScene = _battleRoot.gameObject.scene;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != BattleSceneName)
                return;

            _battleRoot = FindBattleRoot(scene);
            if (_battleRoot == null)
            {
                _sceneLoadError = "BFBattleRoot was not found in BFBattleTest.";
                return;
            }

            // SceneManager.sceneLoaded is raised after Awake and before Start. Pause Root so
            // the SO observer can capture the first BattleStarted notification.
            _battleRoot.enabled = false;
            RegisterSOObservers(_battleRoot);
            _battleRoot.enabled = true;
        }

        private IEnumerator WaitForBattleSessionReady()
        {
            var frame = 0;
            while ((_battleRoot == null || _battleRoot.BattleSession == null ||
                    _battleRoot.BattleSession.State != BFBattleSessionState.Running) &&
                   frame++ < SessionReadyTimeoutFrames)
            {
                yield return null;
            }

            Assert.That(_battleRoot, Is.Not.Null, "Battle root was not initialized.");
            Assert.That(_battleRoot.BattleSession, Is.Not.Null, "Battle session was not created.");
            Assert.That(_battleRoot.UnitRegistry, Is.Not.Null, "Unit registry was not resolved.");
            Assert.That(_battleRoot.TurnManager, Is.Not.Null, "Turn manager was not resolved.");
            Assert.That(_battleRoot.ResolutionManager, Is.Not.Null, "Resolution manager was not resolved.");
            Assert.That(_battleRoot.BattleSession.State, Is.EqualTo(BFBattleSessionState.Running));

            Assert.That(_battleRoot.SelectionController, Is.Not.Null);
            Assert.That(_battleRoot.ActionCoordinator, Is.Not.Null);
            Assert.That(_battleRoot.MovementCoordinator, Is.Not.Null);
            Assert.That(_battleRoot.EnemyActionController, Is.Not.Null);
            Assert.That(_battleRoot.OutcomeCoordinator, Is.Not.Null);
            Assert.That(_battleRoot.SelectionCoordinator, Is.Not.Null);

            _session = _battleRoot.BattleSession;
            RegisterSessionObservers(_session);
        }

        private IEnumerator DriveBattleToCompletion()
        {
            Assert.That(_battleRoot.TurnManager.CurrentPhase, Is.EqualTo(BattlePhase.PlayerTurn));

            var driverSteps = 0;
            var frame = 0;
            while (_session.State == BFBattleSessionState.Running && frame++ < BattleTimeoutFrames)
            {
                Assert.That(driverSteps++, Is.LessThan(MaxDriverSteps),
                    "Battle driver made no progress through production entry points.");

                var attackCountBeforeAction = _attackEvents.Count;
                if (TryStartPlayerAction(out var attackStarted))
                {
                    if (attackStarted)
                        yield return WaitForNextAttackResolved(attackCountBeforeAction);
                    else
                        yield return WaitForActionUnlocked();

                    continue;
                }

                _battleRoot.TurnManager.EndTurn();
                yield return WaitForPlayerTurnOrCompletion();
            }

            Assert.That(frame, Is.LessThan(BattleTimeoutFrames), "Default battle did not complete before timeout.");
            Assert.That(_session.State, Is.EqualTo(BFBattleSessionState.Completed));
        }

        private bool TryStartPlayerAction(out bool attackStarted)
        {
            attackStarted = false;
            var players = GetAliveUnitsByFaction(UnitFaction.Player);

            foreach (var player in players)
            {
                if (player == null || player.Stats.RemainingActionPoints <= 0)
                    continue;

                if (!TrySelectUnit(player))
                    continue;

                var targets = _battleRoot.ActionCoordinator.GetAttackableTargets();
                if (targets.Count > 0 && player.Stats.RemainingActionPoints >= player.Stats.AttackCost)
                {
                    Assert.That(
                        _battleRoot.ActionCoordinator.TryAttackSelected(ChooseTarget(targets)),
                        Is.True,
                        "TryAttack rejected a target returned by GetAttackableTargets.");
                    attackStarted = true;
                    return true;
                }

                var destination = ChooseMoveDestination(player);
                if (destination.HasValue && _battleRoot.ActionCoordinator.TryMoveSelected(destination.Value))
                    return true;

                _battleRoot.SelectionCoordinator.ClearSelection();
            }

            return false;
        }

        private Vector2Int? ChooseMoveDestination(UnitRuntime player)
        {
            var reachableCells = GetReachableCellsForSelected();
            if (reachableCells.Count == 0)
                return null;

            var enemies = GetAliveUnitsByFaction(UnitFaction.Enemy);
            if (enemies.Count == 0)
                return null;

            var bestCell = reachableCells[0];
            var bestDistance = int.MaxValue;
            foreach (var cell in reachableCells)
            {
                if (cell == player.Grid.GridPosition)
                    continue;

                var nearestEnemyDistance = int.MaxValue;
                foreach (var enemy in enemies)
                {
                    var distance = ManhattanDistance(cell, enemy.Grid.GridPosition);
                    if (distance < nearestEnemyDistance)
                        nearestEnemyDistance = distance;
                }

                if (nearestEnemyDistance < bestDistance)
                {
                    bestDistance = nearestEnemyDistance;
                    bestCell = cell;
                }
            }

            return bestCell == player.Grid.GridPosition ? null : bestCell;
        }

        private static UnitRuntime ChooseTarget(List<UnitRuntime> targets)
        {
            var target = targets[0];
            for (var index = 1; index < targets.Count; index++)
            {
                if (targets[index].Stats.CurrentHP < target.Stats.CurrentHP)
                    target = targets[index];
            }

            return target;
        }

        private IEnumerator WaitForNextAttackResolved(int previousAttackCount)
        {
            yield return WaitUntil(
                () => _attackEvents.Count > previousAttackCount || _session.State != BFBattleSessionState.Running,
                ActionTimeoutFrames,
                "TryAttack did not reach an AttackResolvedEvent through the animation hit frame.");

            if (_session.State == BFBattleSessionState.Running)
            {
                yield return WaitUntil(
                    () => !_battleRoot.ActionCoordinator.IsActionLocked,
                    ActionTimeoutFrames,
                    "AttackResolvedEvent was published but the action lock was not released.");
            }
        }

        private IEnumerator WaitForActionUnlocked()
        {
            yield return WaitUntil(
                () => !_battleRoot.ActionCoordinator.IsActionLocked,
                ActionTimeoutFrames,
                "TryMoveUnit did not finish its movement coroutine.");
        }

        private IEnumerator WaitForPlayerTurnOrCompletion()
        {
            yield return WaitUntil(
                () => _session.State != BFBattleSessionState.Running ||
                      (_battleRoot.TurnManager.CurrentPhase == BattlePhase.PlayerTurn &&
                       !_battleRoot.ActionCoordinator.IsActionLocked),
                ActionTimeoutFrames,
                "Enemy turn did not return to PlayerTurn or complete the battle.");
        }

        private static IEnumerator WaitUntil(Func<bool> condition, int timeoutFrames, string failureMessage)
        {
            for (var frame = 0; frame < timeoutFrames; frame++)
            {
                if (condition())
                    yield break;

                yield return null;
            }

            Assert.Fail(failureMessage);
        }

        private IEnumerator UnloadBattleScene()
        {
            DisposeSessionSubscriptions();
            UnregisterSOObservers();

            if (!_battleScene.IsValid() || !_battleScene.isLoaded)
                yield break;

            var operation = SceneManager.UnloadSceneAsync(_battleScene);
            Assert.That(operation, Is.Not.Null, "Unable to unload BFBattleTest.");
            while (!operation.isDone)
                yield return null;

            yield return null;
        }

        private void RegisterSOObservers(BFBattleRoot root)
        {
            _battleEventChannel = root.BattleEventChannel;
            _turnEventChannel = root.TurnEventChannel;
            _unitEventChannel = root.UnitEventChannel;

            _battleEventListener = HandleBattleSOEvent;
            _turnEventListener = HandleTurnSOEvent;
            _unitEventListener = HandleUnitSOEvent;

            _battleEventChannel?.Register(_battleEventListener);
            _turnEventChannel?.Register(_turnEventListener);
            _unitEventChannel?.Register(_unitEventListener);
        }

        private void UnregisterSOObservers()
        {
            _battleEventChannel?.Unregister(_battleEventListener);
            _turnEventChannel?.Unregister(_turnEventListener);
            _unitEventChannel?.Unregister(_unitEventListener);

            _battleEventListener = null;
            _turnEventListener = null;
            _unitEventListener = null;
            _battleEventChannel = null;
            _turnEventChannel = null;
            _unitEventChannel = null;
        }

        private void RegisterSessionObservers(BFBattleSession session)
        {
            _sessionSubscriptions.Add(session.Subscribe<BFBattlePhaseChangedEvent>(HandlePhaseChanged));
            _sessionSubscriptions.Add(session.Subscribe<BFAttackResolvedEvent>(HandleAttackResolved));
            _sessionSubscriptions.Add(session.Subscribe<BFUnitDefeatedEvent>(HandleUnitDefeated));
            _sessionSubscriptions.Add(session.Subscribe<BFBattleCompletedEvent>(HandleBattleCompleted));
        }

        private void DisposeSessionSubscriptions()
        {
            for (var index = _sessionSubscriptions.Count - 1; index >= 0; index--)
                _sessionSubscriptions[index].Dispose();

            _sessionSubscriptions.Clear();
        }

        private void HandleBattleSOEvent(BFBattleEventData eventData)
        {
            if (eventData.EventType == BFBattleEventType.BattleStarted)
                _soBattleStartedCount++;
            else if (eventData.EventType == BFBattleEventType.Victory ||
                     eventData.EventType == BFBattleEventType.Defeat)
                _soBattleCompletedCount++;
        }

        private void HandleTurnSOEvent(BFTurnEventData eventData)
        {
            // The SO observer intentionally records only that the legacy boundary remains active.
            // Domain order assertions use the strongly typed Session observer below.
        }

        private void HandleUnitSOEvent(BFUnitEventData eventData)
        {
            if (eventData.EventType == "Damaged")
                _soDamagedCount++;
            else if (eventData.EventType == "Killed")
                _soKilledCount++;
        }

        private void HandlePhaseChanged(BFBattlePhaseChangedEvent eventData)
        {
            _sessionEventOrder.Add(SessionEventKind.PhaseChanged);
        }

        private void HandleAttackResolved(BFAttackResolvedEvent eventData)
        {
            _attackEvents.Add(eventData);
            _sessionEventOrder.Add(SessionEventKind.AttackResolved);
        }

        private void HandleUnitDefeated(BFUnitDefeatedEvent eventData)
        {
            _defeatedEvents.Add(eventData);
            _sessionEventOrder.Add(SessionEventKind.UnitDefeated);
        }

        private void HandleBattleCompleted(BFBattleCompletedEvent eventData)
        {
            _completedEvents.Add(eventData);
            _sessionEventOrder.Add(SessionEventKind.BattleCompleted);
            _completedCallbackState = _session.State;
            _completedCallbackResult = _session.Context.Result;
        }

        private int FindLastDefeatingAttackIndex()
        {
            for (var attackIndex = _attackEvents.Count - 1; attackIndex >= 0; attackIndex--)
            {
                if (!_attackEvents[attackIndex].TargetWasDefeated)
                    continue;

                var seenAttacks = 0;
                for (var orderIndex = 0; orderIndex < _sessionEventOrder.Count; orderIndex++)
                {
                    if (_sessionEventOrder[orderIndex] != SessionEventKind.AttackResolved)
                        continue;

                    if (seenAttacks++ == attackIndex)
                        return orderIndex;
                }
            }

            return -1;
        }

        private int FindNextEventIndex(SessionEventKind eventKind, int startIndex)
        {
            for (var index = startIndex + 1; index < _sessionEventOrder.Count; index++)
            {
                if (_sessionEventOrder[index] == eventKind)
                    return index;
            }

            return -1;
        }

        private void ResetObservationState()
        {
            _attackEvents.Clear();
            _defeatedEvents.Clear();
            _completedEvents.Clear();
            _sessionEventOrder.Clear();
            _soBattleStartedCount = 0;
            _soBattleCompletedCount = 0;
            _soDamagedCount = 0;
            _soKilledCount = 0;
            _completedCallbackState = default;
            _completedCallbackResult = null;
        }

        private static BFBattleRoot FindBattleRoot(Scene scene)
        {
            foreach (var rootObject in scene.GetRootGameObjects())
            {
                var root = rootObject.GetComponentInChildren<BFBattleRoot>(true);
                if (root != null)
                    return root;
            }

            return null;
        }

        private static int ManhattanDistance(Vector2Int first, Vector2Int second)
        {
            return Mathf.Abs(first.x - second.x) + Mathf.Abs(first.y - second.y);
        }

        private static BFUnitFaction ToDomainFaction(UnitFaction faction)
        {
            return faction switch
            {
                UnitFaction.Player => BFUnitFaction.Player,
                UnitFaction.Enemy => BFUnitFaction.Enemy,
                _ => BFUnitFaction.None
            };
        }

        private enum SessionEventKind
        {
            PhaseChanged,
            AttackResolved,
            UnitDefeated,
            BattleCompleted
        }
        private static Component FindBattleHudComponent()
        {
            foreach (var component in UnityEngine.Object.FindObjectsByType<Component>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (component != null && component.GetType().Name == "BattleHudView")
                    return component;
            }

            return null;
        }

        private static bool ReadBattleHudIsOpen(Component battleHud)
        {
            var property = battleHud?.GetType().GetProperty("IsOpen");
            Assert.That(property, Is.Not.Null, "Battle HUD must expose the UI lifecycle IsOpen property.");
            return (bool)property.GetValue(battleHud);
        }
    }
}
