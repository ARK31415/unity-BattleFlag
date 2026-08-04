using System;
using System.Collections;
using System.Collections.Generic;
using BF.Game.Battle.Domain.Events;
using BF.Game.Runtime.Battle;
using BF.Game.Runtime.Battle.Events;
using BF.Game.Runtime.Battle.Managers;
using BF.Game.Runtime.Battle.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

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
                Is.EqualTo(ToDomainFaction(_completedCallbackResult.WinnerFaction)));

            var finalAttackIndex = FindLastDefeatingAttackIndex();
            var defeatedIndex = FindNextEventIndex(SessionEventKind.UnitDefeated, finalAttackIndex);
            var completedIndex = FindNextEventIndex(SessionEventKind.BattleCompleted, defeatedIndex);

            Assert.That(finalAttackIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(defeatedIndex, Is.GreaterThan(finalAttackIndex));
            Assert.That(completedIndex, Is.GreaterThan(defeatedIndex));

            var context = _session.Context;
            yield return UnloadBattleScene();

            Assert.That(_session.State, Is.EqualTo(BFBattleSessionState.Disposed));
            Assert.That(context.Units, Is.Null);
        }

        [UnityTest]
        public IEnumerator BattleSceneReload_DoesNotReusePreviousSessionSubscriptions()
        {
            var firstSession = _session;
            var firstContext = firstSession.Context;

            Assert.That(_soBattleStartedCount, Is.EqualTo(1));
            Assert.That(firstSession.State, Is.EqualTo(BFBattleSessionState.Running));

            yield return UnloadBattleScene();

            Assert.That(firstSession.State, Is.EqualTo(BFBattleSessionState.Disposed));
            Assert.That(firstContext.Units, Is.Null);

            DisposeSessionSubscriptions();
            UnregisterSOObservers();
            ResetObservationState();

            yield return LoadBattleScene();
            yield return WaitForBattleSessionReady();

            var secondSession = _session;
            Assert.That(secondSession, Is.Not.SameAs(firstSession));
            Assert.That(secondSession.State, Is.EqualTo(BFBattleSessionState.Running));
            Assert.That(_soBattleStartedCount, Is.EqualTo(1));

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
