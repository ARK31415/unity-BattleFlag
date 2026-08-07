using System;
using BF.Game.Battle.Domain;
using BF.Game.Battle.Domain.Events;

namespace BF.Game.Battle.Rules.Battle
{
    /// <summary>
    /// 战斗阶段、回合、轮次和完成结果的受控规则入口。
    ///
    /// 该类型负责先更新 Context，再发布领域事实事件；不负责 Unity 表现或 SO 事件转发。
    /// </summary>
    public sealed class BFBattleProgressRules
    {
        private readonly BFBattleSession _session;

        /// <summary>创建绑定到指定战斗会话的进度规则入口。</summary>
        /// <param name="session">本场战斗规则会话。</param>
        public BFBattleProgressRules(BFBattleSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        /// <summary>
        /// 启动战斗并在会话进入 Running 后发布战斗开始事实。
        /// </summary>
        public void StartBattle()
        {
            _session.Start();
            _session.Publish(new BFBattleStartedEvent(_session.Context.BattleId));
        }

        /// <summary>
        /// 更新阶段、回合和轮次，并发布更新后的阶段事实。
        /// </summary>
        /// <param name="phase">新的战斗阶段。</param>
        /// <param name="turnNumber">新的回合编号。</param>
        /// <param name="roundNumber">新的轮次编号。</param>
        /// <returns>true 表示状态发生了更新。</returns>
        public bool TryUpdateProgress(BFBattlePhase phase, int turnNumber, int roundNumber)
        {
            if (_session.State != BFBattleSessionState.Running)
                return false;
            if (turnNumber < 0 || roundNumber < 0)
                return false;

            var previousPhase = _session.Context.CurrentPhase;
            if (previousPhase == phase
                && _session.Context.TurnNumber == turnNumber
                && _session.Context.RoundNumber == roundNumber)
            {
                return false;
            }

            _session.UpdateProgress(phase, turnNumber, roundNumber);
            _session.Publish(new BFBattlePhaseChangedEvent(
                _session.Context.BattleId,
                previousPhase,
                phase,
                turnNumber,
                roundNumber));
            return true;
        }

        /// <summary>
        /// 写入战斗结果、发布完成事实并完成当前 Session。
        /// </summary>
        /// <param name="result">已经完成计算的战斗结果。</param>
        public void CompleteBattle(BattleResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (!string.Equals(result.BattleId, _session.Context.BattleId, StringComparison.Ordinal))
                throw new ArgumentException("战斗结果的 BattleId 与当前 Session 不一致。", nameof(result));

            _session.SetResult(result);
            _session.Publish(new BFBattleCompletedEvent(
                _session.Context.BattleId,
                result.WinnerFaction,
                result.TotalTurns));
            _session.Complete();
        }
    }
}
