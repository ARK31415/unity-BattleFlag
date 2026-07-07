using System;
using System.Collections.Generic;
using BF.Game.Runtime.Battle.Commands;
using BF.Game.Runtime.Battle.Events;
using BF.Game.Runtime.Battle.Units;
using UnityEngine;

namespace BF.Game.Runtime.Battle.Flow
{
    public enum BattlePhase { None, Init, PlayerTurn, EnemyTurn, Resolution }

    public class BattleStateController : MonoBehaviour
    {
        public BattlePhase CurrentPhase { get; private set; } = BattlePhase.None;
        public BFBattleContext Context { get; private set; }
        public BattleResult Result { get; private set; }
        public event Action<BattlePhase, BattlePhase> OnPhaseChanged;
        public event Action<BattleResult> OnBattleEnded;

        [SerializeField] private BFTurnEventSO _turnEventChannel;
        [SerializeField] private BFBattleEventSO _battleEventChannel;

        private void Start()
        {
            var units = FindObjectsByType<UnitRuntime>(FindObjectsSortMode.None);
            if (units.Length == 0) return;

            var context = new BFBattleContext
            {
                BattleId = "AutoBattle",
                Units = new List<UnitRuntime>(units)
            };
            StartBattle(context);
        }

        public void StartBattle(BFBattleContext context)
        {
            Context = context;
            Result = null;
            Debug.Log($"[BattleStateController] Start battle: {context.BattleId}");
            TransitionTo(BattlePhase.Init);
            TransitionTo(BattlePhase.PlayerTurn);
        }

        public void EndTurn()
        {
            if (CurrentPhase == BattlePhase.PlayerTurn) TransitionTo(BattlePhase.EnemyTurn);
            else if (CurrentPhase == BattlePhase.EnemyTurn) TransitionTo(BattlePhase.PlayerTurn);
        }

        public void CheckBattleEndCondition()
        {
            if (Context == null || (Result != null && Result.HasResult)) return;

            bool playerAlive = Context.GetAliveUnitsByFaction(UnitFaction.Player).Count > 0;
            bool enemyAlive = Context.GetAliveUnitsByFaction(UnitFaction.Enemy).Count > 0;
            if (!playerAlive)
            {
                Result = BattleResult.Defeat(Context.BattleId, Context.TurnNumber);
                TransitionTo(BattlePhase.Resolution);
            }
            else if (!enemyAlive)
            {
                Result = BattleResult.Victory(Context.BattleId, Context.TurnNumber);
                TransitionTo(BattlePhase.Resolution);
            }
        }

        private void TransitionTo(BattlePhase newPhase)
        {
            if (CurrentPhase == newPhase && newPhase != BattlePhase.Init) return;

            var oldPhase = CurrentPhase;
            CurrentPhase = newPhase;
            Debug.Log($"[BattleStateController] Phase: {oldPhase} -> {newPhase}");
            OnPhaseChanged?.Invoke(oldPhase, newPhase);

            switch (newPhase)
            {
                case BattlePhase.PlayerTurn:
                    Context.TurnNumber++;
                    if (oldPhase == BattlePhase.EnemyTurn) Context.RoundNumber++;
                    ResetAllUnitsForNewTurn();
                    RaiseTurnEvent(BFTurnPhase.PlayerTurnStarted);
                    break;
                case BattlePhase.EnemyTurn:
                    RaiseTurnEvent(BFTurnPhase.EnemyTurnStarted);
                    ExecuteEnemyTurn();
                    break;
                case BattlePhase.Resolution:
                    RaiseBattleEndEvent();
                    OnBattleEnded?.Invoke(Result);
                    break;
            }
        }

        private void ExecuteEnemyTurn()
        {
            var enemies = Context.GetAliveUnitsByFaction(UnitFaction.Enemy);
            var players = Context.GetAliveUnitsByFaction(UnitFaction.Player);
            if (enemies.Count == 0 || players.Count == 0)
            {
                CheckBattleEndCondition();
                return;
            }

            foreach (var enemy in enemies)
            {
                if (!enemy.IsAlive) continue;

                var nearest = FindNearestPlayer(enemy, players);
                if (nearest == null) break;

                if (TryEnemyAttack(enemy, nearest))
                {
                    continue;
                }

                if (enemy.MovementHandler == null) continue;

                var reachable = enemy.MovementHandler.GetReachableCells(enemy.GridPosition, enemy.MaxActionPoints, enemy.UnitId);
                if (reachable.Count == 0) continue;

                Vector2Int start = enemy.GridPosition;
                Vector2Int best = FindBestReachableCell(reachable, nearest.GridPosition);

                enemy.MovementHandler.ReleaseCell(enemy.GridPosition, enemy.UnitId);
                enemy.MovementHandler.OccupyCell(best, enemy.UnitId);
                enemy.GridPosition = best;

                var gridManager = enemy.MovementHandler as Grid.BFGridManager;
                if (gridManager != null) enemy.transform.position = (Vector3)gridManager.CellToWorld(best);

                int moveDistance = Mathf.Abs(best.x - start.x) + Mathf.Abs(best.y - start.y);
                Debug.Log($"[BattleAI] {enemy.DisplayName} moved {moveDistance} cells to {best}");

                TryEnemyAttack(enemy, nearest);
            }

            CheckBattleEndCondition();
            if (Result == null || !Result.HasResult) EndTurn();
        }

        private UnitRuntime FindNearestPlayer(UnitRuntime enemy, List<UnitRuntime> players)
        {
            UnitRuntime nearest = null;
            float minDistance = float.MaxValue;

            foreach (var player in players)
            {
                if (!player.IsAlive) continue;

                float distance = Vector2Int.Distance(enemy.GridPosition, player.GridPosition);
                if (distance >= minDistance) continue;

                minDistance = distance;
                nearest = player;
            }

            return nearest;
        }

        private Vector2Int FindBestReachableCell(List<Vector2Int> reachable, Vector2Int target)
        {
            Vector2Int best = reachable[0];
            float bestDistance = float.MaxValue;

            foreach (var cell in reachable)
            {
                float distance = Vector2Int.Distance(cell, target);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = cell;
            }

            return best;
        }

        private bool TryEnemyAttack(UnitRuntime enemy, UnitRuntime target)
        {
            if (enemy == null || target == null || !enemy.IsAlive || !target.IsAlive) return false;

            int distance = Mathf.Abs(target.GridPosition.x - enemy.GridPosition.x)
                         + Mathf.Abs(target.GridPosition.y - enemy.GridPosition.y);
            if (distance > enemy.AttackRange) return false;

            target.TakeDamage(enemy.Attack);
            Debug.Log($"[BattleAI] {enemy.DisplayName} attacked {target.DisplayName} for {enemy.Attack}");
            return true;
        }

        private void ResetAllUnitsForNewTurn()
        {
            if (Context?.Units == null) return;

            foreach (var unit in Context.Units)
            {
                unit?.ResetTurnActions();
            }
        }

        private void RaiseTurnEvent(BFTurnPhase phase)
        {
            _turnEventChannel?.Raise(new BFTurnEventData
            {
                Phase = phase,
                TurnNumber = Context?.TurnNumber ?? 0,
                RoundNumber = Context?.RoundNumber ?? 0
            });
        }

        private void RaiseBattleEndEvent()
        {
            if (_battleEventChannel == null || Result == null) return;

            _battleEventChannel.Raise(new BFBattleEventData
            {
                EventType = Result.IsPlayerVictory ? BFBattleEventType.Victory : BFBattleEventType.Defeat,
                BattleId = Result.BattleId ?? string.Empty,
                WinnerFaction = Result.WinnerFaction.ToString()
            });
        }
    }
}
