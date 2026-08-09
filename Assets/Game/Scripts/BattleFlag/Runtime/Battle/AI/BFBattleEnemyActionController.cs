using System.Collections;
using System.Collections.Generic;
using BF.Game.Battle.Domain;
using BF.Game.Runtime.Battle.Factory;
using BF.Game.Runtime.Battle.Flow;
using BF.Game.Runtime.Battle.Managers;
using BF.Game.Runtime.Battle.Query;
using BF.Game.Runtime.Battle.Units;
using UnityEngine;
using DomainBattleSession = BF.Game.Battle.Domain.BFBattleSession;

namespace BF.Game.Runtime.Battle.AI
{
    /// <summary>
    /// 敌方行动控制器。
    ///
    /// 第一版只提供战斗测试所需的简单策略，所有行动意图都经过统一行动协调器。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BFBattleEnemyActionController : MonoBehaviour
    {
        [SerializeField] private BFBattleActionCoordinator _actionCoordinator;
        [SerializeField] private BFBattleMovementCoordinator _movementCoordinator;
        [SerializeField] private BFBattleTurnManager _turnManager;
        [SerializeField] private BFBattleOutcomeCoordinator _outcomeCoordinator;
        [SerializeField] private BFBattleBoardManager _boardManager;

        private Coroutine _activeTurnCoroutine;
        private bool _isExecuting;
        private DomainBattleSession _battleSession;
        private BFBattleUnitQuery _unitQuery;
        private IBFBattleActionGateway _actionGateway;

        /// <summary>指示敌方回合协程是否正在运行。</summary>
        public bool IsExecuting => _isExecuting;

        /// <summary>绑定敌方行动所需的适配依赖。</summary>
        public void SetDependencies(
            BFBattleActionCoordinator actionCoordinator,
            BFBattleMovementCoordinator movementCoordinator,
            BFBattleTurnManager turnManager,
            BFBattleOutcomeCoordinator outcomeCoordinator,
            BFBattleBoardManager boardManager,
            BFBattleUnitQuery unitQuery,
            DomainBattleSession battleSession)
        {
            _actionCoordinator = actionCoordinator;
            _actionGateway = actionCoordinator;
            _movementCoordinator = movementCoordinator;
            _turnManager = turnManager;
            _outcomeCoordinator = outcomeCoordinator;
            _boardManager = boardManager;
            _unitQuery = unitQuery;
            _battleSession = battleSession;
        }

        /// <summary>开始一次敌方回合行动。</summary>
        public void BeginTurn()
        {
            if (_actionCoordinator == null || IsExecuting ||
                _battleSession == null || _battleSession.State != BFBattleSessionState.Running)
                return;
            if (_boardManager != null && _boardManager.IsSyncFaulted || _actionCoordinator.IsActionLocked)
                return;
            if (_turnManager != null && _turnManager.CurrentPhase != BattlePhase.EnemyTurn)
                return;

            // StartCoroutine 会先同步执行协程，直到遇到第一个 yield；
            // 必须在调用前标记执行状态，否则协程首个行动经过 CanAct 时会被自己的行动锁拒绝。
            _isExecuting = true;
            _activeTurnCoroutine = StartCoroutine(ExecuteTurnCoroutine());

            // 如果协程在首个 yield 前已经完成，FinishTurn 已将状态清理；
            // 避免 StartCoroutine 返回后留下一个已结束的 Coroutine 句柄。
            if (!_isExecuting)
                _activeTurnCoroutine = null;
        }

        /// <summary>停止当前敌方回合并清理行动锁。</summary>
        public void CancelTurn()
        {
            if (_activeTurnCoroutine != null)
            {
                StopCoroutine(_activeTurnCoroutine);
                _activeTurnCoroutine = null;
            }

            _isExecuting = false;

            // 敌方回合可能正处于攻击命中帧等待；取消 AI 协程时必须同时
            // 清理行动协调器持有的 pending attack 与未提交的表现状态。
            _actionCoordinator?.CleanupInterruptedActions();
            _actionCoordinator?.SetActionLocked(false);
        }

        private void OnDisable()
        {
            CancelTurn();
        }

        private IEnumerator ExecuteTurnCoroutine()
        {
            _actionCoordinator.SetActionLocked(true);

            var enemies = _unitQuery?.GetAliveRuntimesByFaction(UnitFaction.Enemy) ?? new List<UnitRuntime>();
            var players = _unitQuery?.GetAliveRuntimesByFaction(UnitFaction.Player) ?? new List<UnitRuntime>();
            if (enemies.Count == 0 || players.Count == 0)
            {
                _outcomeCoordinator?.Evaluate();
                FinishTurn();
                yield break;
            }

            foreach (var enemy in enemies)
            {
                if (_battleSession == null || _battleSession.State != BFBattleSessionState.Running ||
                    _boardManager != null && _boardManager.IsSyncFaulted ||
                    enemy == null || !enemy.RuleState.IsAlive)
                    continue;

                var nearest = FindNearestPlayer(enemy, players);
                if (nearest == null)
                    continue;

                if (_actionGateway.TryAttack(enemy.RuntimeId, nearest.RuntimeId))
                {
                    yield return _actionCoordinator.WaitForAttack(enemy);
                    if (_battleSession == null || _battleSession.State != BFBattleSessionState.Running)
                        break;
                    continue;
                }

                var reachable = _actionCoordinator.GetReachableCellsForUnit(enemy);
                if (reachable.Count > 0)
                {
                    var bestCell = FindBestReachableCell(
                        reachable,
                        new Vector2Int(nearest.RuleState.GridPosition.X, nearest.RuleState.GridPosition.Y));
                    if (_actionGateway.TryMove(enemy.RuntimeId, bestCell))
                        yield return _movementCoordinator.WaitForCompletion();
                }

                if (_boardManager != null && _boardManager.IsSyncFaulted)
                    break;

                if (_battleSession != null && _battleSession.State == BFBattleSessionState.Running &&
                    enemy.RuleState.IsAlive && nearest.RuleState.IsAlive &&
                    _actionGateway.TryAttack(enemy.RuntimeId, nearest.RuntimeId))
                {
                    yield return _actionCoordinator.WaitForAttack(enemy);
                }
            }

            if (_battleSession != null && _battleSession.State == BFBattleSessionState.Running)
                _outcomeCoordinator?.Evaluate();
            FinishTurn();
        }

        private void FinishTurn()
        {
            _activeTurnCoroutine = null;
            _isExecuting = false;
            _actionCoordinator?.SetActionLocked(false);

            if (_battleSession != null && _battleSession.State == BFBattleSessionState.Running &&
                (_outcomeCoordinator?.Result == null || !_outcomeCoordinator.Result.HasResult) &&
                (_boardManager == null || !_boardManager.IsSyncFaulted))
            {
                _turnManager?.EndTurn();
            }
        }

        private static UnitRuntime FindNearestPlayer(
            UnitRuntime enemy,
            IReadOnlyList<UnitRuntime> players)
        {
            UnitRuntime nearest = null;
            var minDistance = float.MaxValue;
            foreach (var player in players)
            {
                if (player == null || !player.RuleState.IsAlive)
                    continue;

                var distance = Vector2Int.Distance(
                    new Vector2Int(enemy.RuleState.GridPosition.X, enemy.RuleState.GridPosition.Y),
                    new Vector2Int(player.RuleState.GridPosition.X, player.RuleState.GridPosition.Y));
                if (distance >= minDistance)
                    continue;

                minDistance = distance;
                nearest = player;
            }

            return nearest;
        }

        private static Vector2Int FindBestReachableCell(
            IReadOnlyList<Vector2Int> reachable,
            Vector2Int target)
        {
            var best = reachable[0];
            var bestDistance = float.MaxValue;
            foreach (var cell in reachable)
            {
                var distance = Vector2Int.Distance(cell, target);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                best = cell;
            }

            return best;
        }
    }
}
