using System;
using BF.Game.Battle.Domain;
using BF.Game.Battle.Domain.Events;
using BF.Game.Battle.Rules.Battle;
using BF.Game.Runtime.Battle;
using BF.Game.Runtime.Battle.Units;
using UnityEngine;
using DomainBattleSession = BF.Game.Battle.Domain.BFBattleSession;

namespace BF.Game.Runtime.Battle.Managers
{
    /// <summary>
    /// 战斗阶段枚举。
    /// </summary>
    public enum BattlePhase
    {
        /// <summary>未进入有效阶段。</summary>
        None,

        /// <summary>战斗初始化阶段。</summary>
        Init,

        /// <summary>玩家行动阶段。</summary>
        PlayerTurn,

        /// <summary>敌方行动阶段。</summary>
        EnemyTurn,

        /// <summary>行动结果结算阶段。</summary>
        Resolution
    }

    /// <summary>
    /// 回合管理器。管理战斗阶段切换、回合计数、结束回合触发，以及
    /// 结束回合按钮高亮条件判断（Spec 第 6 节）。
    /// 不负责可达格计算、单位列表管理、AI 行动决策。
    /// </summary>
    public class BFBattleTurnManager : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private BFBattleUnitManager _unitManager;

        private DomainBattleSession _battleSession;
        private BFBattleProgressRules _battleProgressRules;

        /// <summary>
        /// 当前战斗阶段；始终从当前 BattleSession 的 Context 投影读取。
        /// </summary>
        public BattlePhase CurrentPhase => _battleSession == null
            ? BattlePhase.None
            : FromDomainPhase(_battleSession.Context.CurrentPhase);

        /// <summary>
        /// 当前回合编号；绑定 Session 后以 Context 为唯一来源。
        /// </summary>
        public int TurnNumber => _battleSession == null ? 0 : _battleSession.Context.TurnNumber;

        /// <summary>
        /// 当前轮次编号；绑定 Session 后以 Context 为唯一来源。
        /// </summary>
        public int RoundNumber => _battleSession == null ? 0 : _battleSession.Context.RoundNumber;

        /// <summary>
        /// 指示回合管理器是否已经绑定战斗会话。
        /// </summary>
        public bool HasBattleSession => _battleSession != null;

        /// <summary>阶段变化事件（旧阶段, 新阶段）。</summary>
        public event Action<BattlePhase, BattlePhase> OnPhaseChanged;

        /// <summary>
        /// 玩家是否已无合法操作（true = 无合法操作，应高亮结束回合按钮）。
        /// Spec 第 6 节：高亮不等于自动结束回合。
        /// </summary>
        public event Action<bool> OnNoLegalActionChanged;

        /// <summary>
        /// 将回合管理器绑定到一个战斗会话。
        ///
        /// 同一个管理器可以重复绑定同一会话，但不能改绑到其他会话。
        /// </summary>
        /// <param name="session">要绑定的战斗会话。</param>
        /// <exception cref="InvalidOperationException">当管理器已经绑定其他会话时抛出。</exception>
        public void SetBattleSession(DomainBattleSession session)
        {
            if (_battleSession != null && _battleSession != session)
                throw new InvalidOperationException("BFBattleTurnManager is already attached to another battle session.");

            _battleSession = session;
            _battleProgressRules = session == null ? null : new BFBattleProgressRules(session);
        }

        /// <summary>启动战斗流程。</summary>
        public void StartBattle()
        {
            if (_battleSession == null || _battleProgressRules == null)
            {
                Debug.LogWarning("[BFBattleTurnManager] Cannot start battle without a BattleSession.");
                return;
            }

            Debug.Log("[BFBattleTurnManager] Starting battle");

            if (_battleSession.State == BF.Game.Battle.Domain.BFBattleSessionState.Created)
            {
                _battleProgressRules.StartBattle();
            }

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
            if (_battleSession == null || _battleProgressRules == null) return;
            if (CurrentPhase == newPhase && newPhase != BattlePhase.Init) return;

            var oldPhase = CurrentPhase;
            var nextTurnNumber = TurnNumber;
            var nextRoundNumber = RoundNumber;

            if (newPhase == BattlePhase.PlayerTurn)
            {
                nextTurnNumber++;
                if (oldPhase == BattlePhase.EnemyTurn)
                    nextRoundNumber++;
            }

            Debug.Log($"[BFBattleTurnManager] Phase: {oldPhase} → {newPhase}");
            switch (newPhase)
            {
                case BattlePhase.PlayerTurn:
                    _unitManager?.ResetAllUnitsForNewTurn();
                    RefreshPlayerLegalActions();
                    break;

                case BattlePhase.EnemyTurn:
                    break;

                case BattlePhase.Resolution:
                    break;
            }

            _battleProgressRules.TryUpdateProgress(
                ToDomainPhase(newPhase),
                nextTurnNumber,
                nextRoundNumber);

            // 保留旧 C# 观察者，但现在回调读取到的是更新后的阶段与回合数据。
            OnPhaseChanged?.Invoke(oldPhase, newPhase);

            if (newPhase == BattlePhase.EnemyTurn)
                _unitManager?.ExecuteEnemyTurn();
        }

        private static BFBattlePhase ToDomainPhase(BattlePhase phase)
        {
            return phase switch
            {
                BattlePhase.Init => BFBattlePhase.Init,
                BattlePhase.PlayerTurn => BFBattlePhase.PlayerTurn,
                BattlePhase.EnemyTurn => BFBattlePhase.EnemyTurn,
                BattlePhase.Resolution => BFBattlePhase.Resolution,
                _ => BFBattlePhase.None
            };
        }

        private static BattlePhase FromDomainPhase(BFBattlePhase phase)
        {
            return phase switch
            {
                BFBattlePhase.Init => BattlePhase.Init,
                BFBattlePhase.PlayerTurn => BattlePhase.PlayerTurn,
                BFBattlePhase.EnemyTurn => BattlePhase.EnemyTurn,
                BFBattlePhase.Resolution => BattlePhase.Resolution,
                _ => BattlePhase.None
            };
        }

    }
}
