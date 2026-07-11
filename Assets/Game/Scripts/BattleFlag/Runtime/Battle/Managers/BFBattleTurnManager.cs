using System;
using BF.Game.Runtime.Battle.Events;
using BF.Game.Runtime.Battle.Units;
using UnityEngine;

namespace BF.Game.Runtime.Battle.Managers
{
    /// <summary>
    /// 战斗阶段枚举。
    /// </summary>
    public enum BattlePhase
    {
        None,
        Init,
        PlayerTurn,
        EnemyTurn,
        Resolution
    }

    /// <summary>
    /// 回合管理器。管理战斗阶段切换、回合计数、结束回合触发，以及
    /// 结束回合按钮高亮条件判断（Spec 第 6 节）。
    /// 不负责可达格计算、单位列表管理、AI 行动决策。
    /// </summary>
    public class BFBattleTurnManager : MonoBehaviour
    {
        [Header("Event Channels")]
        [SerializeField] private BFTurnEventSO _turnEventChannel;
        [SerializeField] private BFBattleEventSO _battleEventChannel;

        [Header("Dependencies")]
        [SerializeField] private BFBattleUnitManager _unitManager;

        /// <summary>当前战斗阶段。</summary>
        public BattlePhase CurrentPhase { get; private set; } = BattlePhase.None;

        /// <summary>当前回合编号（从 1 开始，每次 PlayerTurn 递增）。</summary>
        public int TurnNumber { get; private set; }

        /// <summary>当前轮次编号（EnemyTurn → PlayerTurn 时递增）。</summary>
        public int RoundNumber { get; private set; }

        /// <summary>阶段变化事件（旧阶段, 新阶段）。</summary>
        public event Action<BattlePhase, BattlePhase> OnPhaseChanged;

        /// <summary>
        /// 玩家是否已无合法操作（true = 无合法操作，应高亮结束回合按钮）。
        /// Spec 第 6 节：高亮不等于自动结束回合。
        /// </summary>
        public event Action<bool> OnNoLegalActionChanged;

        /// <summary>启动战斗流程。</summary>
        public void StartBattle()
        {
            TurnNumber = 0;
            RoundNumber = 0;
            Debug.Log("[BFBattleTurnManager] Starting battle");
            TransitionTo(BattlePhase.Init);
            TransitionTo(BattlePhase.PlayerTurn);
        }

        /// <summary>玩家手动结束回合。</summary>
        public void EndTurn()
        {
            if (CurrentPhase == BattlePhase.PlayerTurn)
                TransitionTo(BattlePhase.EnemyTurn);
            else if (CurrentPhase == BattlePhase.EnemyTurn)
                TransitionTo(BattlePhase.PlayerTurn);
        }

        /// <summary>
        /// 强制进入结算阶段（由 UnitManager 在全灭判定后调用）。
        /// </summary>
        public void TransitionToResolution()
        {
            TransitionTo(BattlePhase.Resolution);
        }

        /// <summary>
        /// 刷新玩家合法操作状态并广播（选中/移动/攻击后调用）。
        /// </summary>
        public void RefreshPlayerLegalActions()
        {
            bool hasLegal = _unitManager != null && _unitManager.PlayerHasLegalAction();
            OnNoLegalActionChanged?.Invoke(!hasLegal);
        }

        private void TransitionTo(BattlePhase newPhase)
        {
            if (CurrentPhase == newPhase && newPhase != BattlePhase.Init) return;

            var oldPhase = CurrentPhase;
            CurrentPhase = newPhase;
            Debug.Log($"[BFBattleTurnManager] Phase: {oldPhase} → {newPhase}");
            OnPhaseChanged?.Invoke(oldPhase, newPhase);

            switch (newPhase)
            {
                case BattlePhase.PlayerTurn:
                    TurnNumber++;
                    if (oldPhase == BattlePhase.EnemyTurn) RoundNumber++;
                    _unitManager?.ResetAllUnitsForNewTurn();
                    RaiseTurnEvent(BFTurnPhase.PlayerTurnStarted);
                    RefreshPlayerLegalActions();
                    break;

                case BattlePhase.EnemyTurn:
                    RaiseTurnEvent(BFTurnPhase.EnemyTurnStarted);
                    _unitManager?.ExecuteEnemyTurn();
                    break;

                case BattlePhase.Resolution:
                    RaiseBattleEndEvent();
                    break;
            }
        }

        private void RaiseTurnEvent(BFTurnPhase phase)
        {
            _turnEventChannel?.Raise(new BFTurnEventData
            {
                Phase = phase,
                TurnNumber = TurnNumber,
                RoundNumber = RoundNumber
            });
        }

        private void RaiseBattleEndEvent()
        {
            var result = _unitManager?.Result;
            if (_battleEventChannel == null || result == null) return;

            _battleEventChannel.Raise(new BFBattleEventData
            {
                EventType = result.IsPlayerVictory ? BFBattleEventType.Victory : BFBattleEventType.Defeat,
                BattleId = result.BattleId ?? string.Empty,
                WinnerFaction = result.WinnerFaction.ToString()
            });
        }
    }
}
