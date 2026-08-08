using System;
using System.Collections;
using System.Collections.Generic;
using BF.Game.Battle.Domain.Events;
using BF.Game.Battle.Domain.Units;
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

            Assert.That(_session.State, Is.EqualTo(BFBattleSessionState.Completed));
            Assert.That(_completedCallbackState, Is.EqualTo(BFBattleSessionState.Running));
            Assert.That(_completedCallbackResult, Is.SameAs(_session.Context.Result));
            Assert.That(_completedCallbackResult, Is.Not.Null);
            Assert.That(_completedCallbackResult.HasResult, Is.True);
            Assert.That(
                _completedEvents[0].WinnerFaction,
                Is.EqualTo(_completedCallbackResult.WinnerFaction));

            // 胜负协调器必须具备幂等性：完成后的重复评估不能再次发布完成事实。
            _battleRoot.UnitManager.CheckBattleEndCondition();
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
            var player = _battleRoot.UnitManager.GetAliveUnitsByFaction(UnitFaction.Player)[0];
            Assert.That(_battleRoot.UnitManager.TrySelectUnit(player), Is.True);

            var target = ChooseAttackableTarget();
            if (target == null)
            {
                var destination = ChooseMoveDestination(player);
                if (destination.HasValue)
                {
                    Assert.That(_battleRoot.UnitManager.TryMoveUnit(destination.Value), Is.True);
                    yield return WaitForActionUnlocked();
                }

                // 移动消耗 AP 后可能不足以攻击；结束回合让 AP 重置，下一回合直接攻击。
                _battleRoot.TurnManager.EndTurn();
                yield return WaitForPlayerTurnOrCompletion();
                Assert.That(_battleRoot.UnitManager.TrySelectUnit(player), Is.True);
                target = ChooseAttackableTarget();
            }

            Assert.That(target, Is.Not.Null, "玩家单位无法进入攻击范围。");
            var apBefore = player.RuleState.Attributes.RemainingActionPoints;
            var targetHpBefore = target.RuleState.Attributes.CurrentHP;
            var attackEventCountBefore = _attackEvents.Count;

            Assert.That(_battleRoot.UnitManager.TryAttack(target), Is.True);

            // 命中前禁用攻击单位：攻击上下文被清理，AP 不消耗、不造成伤害、不发布攻击事实。
            player.gameObject.SetActive(false);
            yield return WaitUntil(
                () => !_battleRoot.UnitManager.IsActionLocked || _session.State != BFBattleSessionState.Running,
                ActionTimeoutFrames,
                "禁用攻击单位后动作锁未释放。");

            Assert.That(player.RuleState.Attributes.RemainingActionPoints, Is.EqualTo(apBefore));
            Assert.That(player.RuleState.ActionState, Is.EqualTo(BFUnit_ActionState.Idle));
            Assert.That(target.RuleState.Attributes.CurrentHP, Is.EqualTo(targetHpBefore));
            Assert.That(_attackEvents.Count, Is.EqualTo(attackEventCountBefore));
        }

        [UnityTest]
        public IEnumerator DisablingAttackTargetBeforeHit_ClearsAttackerActionLifecycle()
        {
            var attacker = _battleRoot.UnitManager.GetAliveUnitsByFaction(UnitFaction.Player)[0];
            Assert.That(_battleRoot.UnitManager.TrySelectUnit(attacker), Is.True);

            var target = ChooseAttackableTarget();
            if (target == null)
            {
                var destination = ChooseMoveDestination(attacker);
                if (destination.HasValue)
                {
                    Assert.That(_battleRoot.UnitManager.TryMoveUnit(destination.Value), Is.True);
                    yield return WaitForActionUnlocked();
                }

                _battleRoot.TurnManager.EndTurn();
                yield return WaitForPlayerTurnOrCompletion();
                Assert.That(_battleRoot.UnitManager.TrySelectUnit(attacker), Is.True);
                target = ChooseAttackableTarget();
            }

            Assert.That(target, Is.Not.Null, "玩家单位无法进入攻击范围。");

            var apBefore = attacker.RuleState.Attributes.RemainingActionPoints;
            var targetHpBefore = target.RuleState.Attributes.CurrentHP;
            var attackEventCountBefore = _attackEvents.Count;

            Assert.That(_battleRoot.UnitManager.TryAttack(target), Is.True);

            // 目标在命中帧前被禁用时，必须清理攻击者的 pending attack、动作状态和行动锁。
            target.gameObject.SetActive(false);
            yield return WaitUntil(
                () => !_battleRoot.UnitManager.IsActionLocked || _session.State != BFBattleSessionState.Running,
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
            var player = _battleRoot.UnitManager.GetAliveUnitsByFaction(UnitFaction.Player)[0];
            Assert.That(player.RuleState.Attributes.RemainingActionPoints, Is.GreaterThan(0));
            Assert.That(_battleRoot.UnitManager.TrySelectUnit(player), Is.True);

            Assert.That(_battleRoot.UnitManager.TryWaitSelectedUnit(), Is.True);
            yield return null;

            Assert.That(player.RuleState.Attributes.RemainingActionPoints, Is.EqualTo(0));
            Assert.That(player.RuleState.ActionState, Is.EqualTo(BFUnit_ActionState.Idle));
            Assert.That(_battleRoot.UnitManager.SelectedUnit, Is.Null);
            Assert.That(_battleRoot.UnitManager.IsActionLocked, Is.False);
        }

        [UnityTest]
        public IEnumerator EndingPlayerTurn_ExecutesEnemyActionBeforeReturning()
        {
            var enemy = _battleRoot.UnitManager.GetAliveUnitsByFaction(UnitFaction.Enemy)[0];
            var enemyActionController = _battleRoot.UnitManager.GetComponent<BFBattleEnemyActionController>();
            var startPosition = enemy.RuleState.GridPosition;
            var startActionPoints = enemy.RuleState.Attributes.RemainingActionPoints;

            _battleRoot.TurnManager.EndTurn();

            yield return WaitUntil(
                () => _battleRoot.TurnManager.CurrentPhase == BattlePhase.PlayerTurn &&
                      !_battleRoot.UnitManager.IsActionLocked,
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
            var targets = _battleRoot.UnitManager.GetAttackableTargets();
            return targets.Count > 0 ? ChooseTarget(targets) : null;
        }

        [UnityTest]
        public IEnumerator BattleSceneReload_DoesNotReusePreviousSessionSubscriptions()
        {
            var firstSession = _session;
            var firstContext = firstSession.Context;
            var firstBattleId = firstSession.Context.BattleId;
            var firstRuntime = _battleRoot.UnitManager.AllUnits[0];
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
            Assert.That(_battleRoot.UnitManager, Is.Not.Null, "Unit manager was not resolved.");
            Assert.That(_battleRoot.TurnManager, Is.Not.Null, "Turn manager was not resolved.");
            Assert.That(_battleRoot.ResolutionManager, Is.Not.Null, "Resolution manager was not resolved.");
            Assert.That(_battleRoot.BattleSession.State, Is.EqualTo(BFBattleSessionState.Running));

            Assert.That(_battleRoot.UnitManager.GetComponent<BFBattleSelectionController>(), Is.Not.Null);
            Assert.That(_battleRoot.UnitManager.GetComponent<BFBattleActionCoordinator>(), Is.Not.Null);
            Assert.That(_battleRoot.UnitManager.GetComponent<BFBattleMovementCoordinator>(), Is.Not.Null);
            Assert.That(_battleRoot.UnitManager.GetComponent<BFBattleEnemyActionController>(), Is.Not.Null);
            Assert.That(_battleRoot.UnitManager.GetComponent<BFBattleOutcomeCoordinator>(), Is.Not.Null);
            Assert.That(_battleRoot.UnitManager.ActionCoordinator, Is.SameAs(
                _battleRoot.UnitManager.GetComponent<BFBattleActionCoordinator>()));

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
            var players = _battleRoot.UnitManager.GetAliveUnitsByFaction(UnitFaction.Player);

            foreach (var player in players)
            {
                if (player == null || player.Stats.RemainingActionPoints <= 0)
                    continue;

                if (!_battleRoot.UnitManager.TrySelectUnit(player))
                    continue;

                var targets = _battleRoot.UnitManager.GetAttackableTargets();
                if (targets.Count > 0 && player.Stats.RemainingActionPoints >= player.Stats.AttackCost)
                {
                    Assert.That(
                        _battleRoot.UnitManager.TryAttack(ChooseTarget(targets)),
                        Is.True,
                        "TryAttack rejected a target returned by GetAttackableTargets.");
                    attackStarted = true;
                    return true;
                }

                var destination = ChooseMoveDestination(player);
                if (destination.HasValue && _battleRoot.UnitManager.TryMoveUnit(destination.Value))
                    return true;

                _battleRoot.UnitManager.DeselectUnit();
            }

            return false;
        }

        private Vector2Int? ChooseMoveDestination(UnitRuntime player)
        {
            var reachableCells = _battleRoot.UnitManager.GetReachableCellsForSelected();
            if (reachableCells.Count == 0)
                return null;

            var enemies = _battleRoot.UnitManager.GetAliveUnitsByFaction(UnitFaction.Enemy);
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
                    () => !_battleRoot.UnitManager.IsActionLocked,
                    ActionTimeoutFrames,
                    "AttackResolvedEvent was published but the action lock was not released.");
            }
        }

        private IEnumerator WaitForActionUnlocked()
        {
            yield return WaitUntil(
                () => !_battleRoot.UnitManager.IsActionLocked,
                ActionTimeoutFrames,
                "TryMoveUnit did not finish its movement coroutine.");
        }

        private IEnumerator WaitForPlayerTurnOrCompletion()
        {
            yield return WaitUntil(
                () => _session.State != BFBattleSessionState.Running ||
                      (_battleRoot.TurnManager.CurrentPhase == BattlePhase.PlayerTurn &&
                       !_battleRoot.UnitManager.IsActionLocked),
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
    }
}
