using System.Collections;
using System.Collections.Generic;
using BF.Game.Runtime.Battle.Flow;
using BF.Game.Runtime.Battle.Managers;
using BF.Game.Runtime.Battle.Units;
using UnityEngine;

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
        [SerializeField] private BFBattleUnitManager _unitManager;
        [SerializeField] private BFBattleActionCoordinator _actionCoordinator;
        [SerializeField] private BFBattleMovementCoordinator _movementCoordinator;
        [SerializeField] private BFBattleTurnManager _turnManager;

        private Coroutine _activeTurnCoroutine;
        private bool _isExecuting;

        /// <summary>指示敌方回合协程是否正在运行。</summary>
        public bool IsExecuting => _isExecuting;

        /// <summary>绑定敌方行动所需的适配依赖。</summary>
        public void SetDependencies(
            BFBattleUnitManager unitManager,
            BFBattleActionCoordinator actionCoordinator,
            BFBattleMovementCoordinator movementCoordinator,
            BFBattleTurnManager turnManager)
        {
            _unitManager = unitManager;
            _actionCoordinator = actionCoordinator;
            _movementCoordinator = movementCoordinator;
            _turnManager = turnManager;
        }

        /// <summary>开始一次敌方回合行动。</summary>
        public void BeginTurn()
        {
            if (_unitManager == null || _actionCoordinator == null || IsExecuting ||
                !_unitManager.IsBattleRunning)
                return;
            if (_unitManager.IsBoardSyncFaulted || _unitManager.IsActionLocked)
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
            _unitManager?.SetActionLockedForCoordinator(false);
        }

        private void OnDisable()
        {
            CancelTurn();
        }

        private IEnumerator ExecuteTurnCoroutine()
        {
            _unitManager.SetActionLockedForCoordinator(true);

            var enemies = _unitManager.GetAliveUnitsByFaction(UnitFaction.Enemy);
            var players = _unitManager.GetAliveUnitsByFaction(UnitFaction.Player);
            if (enemies.Count == 0 || players.Count == 0)
            {
                _unitManager.CheckBattleEndCondition();
                FinishTurn();
                yield break;
            }

            foreach (var enemy in enemies)
            {
                if (!_unitManager.IsBattleRunning || _unitManager.IsBoardSyncFaulted ||
                    enemy == null || !enemy.RuleState.IsAlive)
                    continue;

                var nearest = FindNearestPlayer(enemy, players);
                if (nearest == null)
                    continue;

                if (_actionCoordinator.TryAttack(enemy, nearest))
                {
                    yield return _actionCoordinator.WaitForAttack(enemy);
                    if (!_unitManager.IsBattleRunning)
                        break;
                    continue;
                }

                var reachable = _unitManager.GetReachableCellsForUnit(enemy);
                if (reachable.Count > 0)
                {
                    var bestCell = FindBestReachableCell(
                        reachable,
                        new Vector2Int(nearest.RuleState.GridPosition.X, nearest.RuleState.GridPosition.Y));
                    if (_actionCoordinator.TryMove(enemy, bestCell))
                        yield return _movementCoordinator.WaitForCompletion();
                }

                if (_unitManager.IsBoardSyncFaulted)
                    break;

                if (_unitManager.IsBattleRunning && enemy.RuleState.IsAlive && nearest.RuleState.IsAlive &&
                    _actionCoordinator.TryAttack(enemy, nearest))
                {
                    yield return _actionCoordinator.WaitForAttack(enemy);
                }
            }

            if (_unitManager.IsBattleRunning)
                _unitManager.CheckBattleEndCondition();
            FinishTurn();
        }

        private void FinishTurn()
        {
            _activeTurnCoroutine = null;
            _isExecuting = false;
            _unitManager.SetActionLockedForCoordinator(false);

            if (_unitManager.IsBattleRunning && !_unitManager.IsBoardSyncFaulted &&
                (_unitManager.Result == null || !_unitManager.Result.HasResult))
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
