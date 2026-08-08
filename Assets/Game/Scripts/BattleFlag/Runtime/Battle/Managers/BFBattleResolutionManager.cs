using System.Collections.Generic;
using BF.Game.Battle.Rules.Units;
using BF.Game.Runtime.Battle.Commands;
using BF.Game.Runtime.Battle.Flow;
using BF.Game.Runtime.Battle.Units;
using UnityEngine;
using DomainBattleSession = BF.Game.Battle.Domain.BFBattleSession;

namespace BF.Game.Runtime.Battle.Managers
{
    /// <summary>
    /// 战斗结算层场景级协调器。
    /// 负责登记待结算攻击、在命中帧触发结算、处理死亡视觉清理。
    /// </summary>
    public class BFBattleResolutionManager : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private BFBattleUnitManager _unitManager;
        [SerializeField] private BFBattleActionCoordinator _actionCoordinator;

        private BFAttackResolver _attackResolver;
        private BFBuffResolver _buffResolver;
        private BFTriggerResolver _triggerResolver;
        private DomainBattleSession _battleSession;
        private BFUnitStateRules _unitStateRules;

        private readonly Dictionary<UnitRuntime, BFAttackContext> _pendingAttacks = new();
        private readonly HashSet<UnitRuntime> _awaitingDeathVisualCleanup = new();
        private readonly List<UnitRuntime> _attackersToClearBuffer = new();

        public bool IsAwaitingDeathVisualCleanup => _awaitingDeathVisualCleanup.Count > 0;

        private void Awake()
        {
            _attackResolver = null;
            _buffResolver = new BFBuffResolver();
            _triggerResolver = new BFTriggerResolver();
        }

        /// <summary>
        /// 将结算协调器绑定到当前战斗会话，并为攻击结算注入对应的单位规则入口。
        /// </summary>
        /// <param name="session">当前战斗会话。</param>
        public void SetBattleSession(DomainBattleSession session)
        {
            if (_battleSession != null && _battleSession != session)
                throw new System.InvalidOperationException(
                    "BFBattleResolutionManager is already attached to another battle session.");

            _battleSession = session;
            _unitStateRules = session == null ? null : new BFUnitStateRules(session.Context);
            _attackResolver = new BFAttackResolver(_unitStateRules);
        }

        /// <summary>
        /// 设置 UnitManager 引用（由 BFBattleRoot 在初始化时调用）。
        /// </summary>
        public void SetUnitManager(BFBattleUnitManager unitManager)
        {
            _unitManager = unitManager;
            _actionCoordinator = unitManager != null ? unitManager.ActionCoordinator : null;
        }

        /// <summary>绑定统一行动协调器，避免结算结果回流到混合单位管理器。</summary>
        public void SetActionCoordinator(BFBattleActionCoordinator actionCoordinator)
        {
            _actionCoordinator = actionCoordinator;
        }

        /// <summary>
        /// 登记一次待结算攻击。
        /// </summary>
        public bool TryQueueAttack(UnitRuntime attacker, UnitRuntime target)
        {
            if (attacker == null || target == null || _battleSession == null ||
                _battleSession.State != BF.Game.Battle.Domain.BFBattleSessionState.Running)
            {
                Debug.LogError("[BFBattleResolutionManager] 攻击者或目标为空。");
                return false;
            }

            if (!attacker.IsRuleBound || !attacker.RuleState.IsAlive)
            {
                Debug.LogWarning("[BFBattleResolutionManager] 攻击者已死亡，无法发起攻击。");
                return false;
            }

            if (!target.IsRuleBound || !target.RuleState.IsAlive)
            {
                Debug.LogWarning("[BFBattleResolutionManager] 目标已死亡，无法发起攻击。");
                return false;
            }

            if (_pendingAttacks.ContainsKey(attacker))
            {
                Debug.LogWarning("[BFBattleResolutionManager] 攻击者已有待结算攻击，无法重复登记。");
                return false;
            }

            // 这里保存的是结算层快照；动画命中帧到来后再消费，避免攻击动画开始时立即扣血。
            var context = new BFAttackContext(attacker, target);
            _pendingAttacks[attacker] = context;

            Debug.Log($"[BFBattleResolutionManager] 已登记攻击：{attacker.Identity.DisplayName} -> {target.Identity.DisplayName}");
            return true;
        }

        /// <summary>
        /// 尝试结算攻击者的待结算攻击（由动画命中帧事件调用）。
        /// </summary>
        public bool TryResolveQueuedAttack(UnitRuntime attacker)
        {
            if (attacker == null)
            {
                Debug.LogWarning("[BFBattleResolutionManager] 攻击者为空，无法结算。");
                return false;
            }

            if (!_pendingAttacks.TryGetValue(attacker, out var context))
            {
                Debug.LogWarning("[BFBattleResolutionManager] 攻击者无待结算攻击。");
                // 上下文不同步（如 pending 已被异常清理）时仍要释放攻击表现生命周期，
                // 防止攻击者停留在 Attack 状态并阻塞动作锁。
                _actionCoordinator?.HandleAttackResolutionFailed(attacker);
                return false;
            }

            if (context.Consumed)
            {
                Debug.LogWarning("[BFBattleResolutionManager] 攻击上下文已被消费，防止重复结算。");
                return false;
            }

            // 目标在命中帧前可能已经死亡；此时清理待结算攻击，不再生成伤害结果。
            if (!context.Target.IsRuleBound || !context.Target.RuleState.IsAlive)
            {
                Debug.LogWarning("[BFBattleResolutionManager] 目标已死亡，清理待结算攻击。");
                _pendingAttacks.Remove(attacker);
                _actionCoordinator?.HandleAttackResolutionFailed(attacker);
                return false;
            }

            if (_attackResolver == null)
            {
                Debug.LogWarning("[BFBattleResolutionManager] 未绑定 BattleSession，无法进行攻击结算。");
                _pendingAttacks.Remove(attacker);
                _actionCoordinator?.HandleAttackResolutionFailed(attacker);
                return false;
            }

            var result = _attackResolver.Resolve(context);
            _pendingAttacks.Remove(attacker);

            // 规则拒绝时返回明确失败结果。失败路径必须释放表现攻击生命周期，
            // 且不能继续发布攻击成功事实。
            if (!result.Succeeded)
            {
                Debug.LogWarning($"[BFBattleResolutionManager] 攻击规则结算失败：{result.FailureReason}");
                _actionCoordinator?.HandleAttackResolutionFailed(attacker);
                return false;
            }

            Debug.Log($"[BFBattleResolutionManager] 攻击结算：{result.Attacker.Identity.DisplayName} -> {result.Target.Identity.DisplayName}，伤害 {result.FinalDamage}，目标剩余 HP {result.TargetRemainingHp}");

            _actionCoordinator?.HandleAttackResolved(result);

            if (result.TargetWasKilled)
            {
                // 逻辑死亡已在伤害入口完成，这里只登记等待死亡动画完成后的视觉清理。
                _awaitingDeathVisualCleanup.Add(result.Target);
            }

            return true;
        }

        /// <summary>
        /// 通知死亡视觉动画完成（由动画完成事件调用）。
        /// </summary>
        public void NotifyDeathVisualFinished(UnitRuntime unit)
        {
            if (unit == null) return;

            if (!_awaitingDeathVisualCleanup.Contains(unit))
            {
                Debug.LogWarning($"[BFBattleResolutionManager] {unit.Identity.DisplayName} 不在死亡视觉清理队列中。");
                return;
            }

            _awaitingDeathVisualCleanup.Remove(unit);
            unit.FinalizeDeathVisualCleanup();

            Debug.Log($"[BFBattleResolutionManager] {unit.Identity.DisplayName} 死亡视觉清理完成。");
        }

        /// <summary>
        /// 检查是否有待结算的攻击。
        /// </summary>
        public bool HasPendingAttack(UnitRuntime attacker)
        {
            return attacker != null && _pendingAttacks.ContainsKey(attacker);
        }

        /// <summary>
        /// 清理攻击者的待结算攻击（用于异常情况）。
        /// </summary>
        public void ClearPendingAttack(UnitRuntime attacker)
        {
            if (attacker != null && _pendingAttacks.ContainsKey(attacker))
            {
                _pendingAttacks.Remove(attacker);
                Debug.Log($"[BFBattleResolutionManager] 已清理 {attacker.Identity.DisplayName} 的待结算攻击。");
            }
        }

        /// <summary>
        /// 清理与被禁用单位有关的所有待结算攻击。
        ///
        /// 单位既可能是攻击者，也可能是待命中的目标。目标被禁用时不能只清理目标自身的
        /// Combat 状态，否则攻击者的 pending attack 和全局行动锁会继续存留到超时。
        /// </summary>
        /// <param name="unit">被禁用或销毁的单位。</param>
        public void ClearPendingAttacksInvolving(UnitRuntime unit)
        {
            if (unit == null || _pendingAttacks.Count == 0)
                return;

            _attackersToClearBuffer.Clear();
            foreach (var pendingAttack in _pendingAttacks)
            {
                if (pendingAttack.Key == unit || pendingAttack.Value.Target == unit)
                    _attackersToClearBuffer.Add(pendingAttack.Key);
            }

            foreach (var attacker in _attackersToClearBuffer)
            {
                if (!_pendingAttacks.Remove(attacker))
                    continue;

                _actionCoordinator?.HandleAttackResolutionFailed(attacker);
            }

            _attackersToClearBuffer.Clear();
        }
    }
}
