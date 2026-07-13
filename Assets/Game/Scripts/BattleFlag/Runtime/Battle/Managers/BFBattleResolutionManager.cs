using System.Collections.Generic;
using BF.Game.Runtime.Battle.Commands;
using BF.Game.Runtime.Battle.Units;
using UnityEngine;

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

        private BFAttackResolver _attackResolver;
        private BFBuffResolver _buffResolver;
        private BFTriggerResolver _triggerResolver;

        private readonly Dictionary<UnitRuntime, BFAttackContext> _pendingAttacks = new();
        private readonly HashSet<UnitRuntime> _awaitingDeathVisualCleanup = new();

        public bool IsAwaitingDeathVisualCleanup => _awaitingDeathVisualCleanup.Count > 0;

        private void Awake()
        {
            _attackResolver = new BFAttackResolver();
            _buffResolver = new BFBuffResolver();
            _triggerResolver = new BFTriggerResolver();
        }

        /// <summary>
        /// 设置 UnitManager 引用（由 BFBattleRoot 在初始化时调用）。
        /// </summary>
        public void SetUnitManager(BFBattleUnitManager unitManager)
        {
            _unitManager = unitManager;
        }

        /// <summary>
        /// 登记一次待结算攻击。
        /// </summary>
        public bool TryQueueAttack(UnitRuntime attacker, UnitRuntime target)
        {
            if (attacker == null || target == null)
            {
                Debug.LogError("[BFBattleResolutionManager] 攻击者或目标为空。");
                return false;
            }

            if (!attacker.IsAlive)
            {
                Debug.LogWarning("[BFBattleResolutionManager] 攻击者已死亡，无法发起攻击。");
                return false;
            }

            if (!target.IsAlive)
            {
                Debug.LogWarning("[BFBattleResolutionManager] 目标已死亡，无法发起攻击。");
                return false;
            }

            if (_pendingAttacks.ContainsKey(attacker))
            {
                Debug.LogWarning("[BFBattleResolutionManager] 攻击者已有待结算攻击，无法重复登记。");
                return false;
            }

            var context = new BFAttackContext(attacker, target);
            _pendingAttacks[attacker] = context;

            Debug.Log($"[BFBattleResolutionManager] 已登记攻击：{attacker.DisplayName} -> {target.DisplayName}");
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
                return false;
            }

            if (context.Consumed)
            {
                Debug.LogWarning("[BFBattleResolutionManager] 攻击上下文已被消费，防止重复结算。");
                return false;
            }

            if (!context.Target.IsAlive)
            {
                Debug.LogWarning("[BFBattleResolutionManager] 目标已死亡，清理待结算攻击。");
                _pendingAttacks.Remove(attacker);
                return false;
            }

            var result = _attackResolver.Resolve(context);
            _pendingAttacks[attacker] = context.AsConsumed();

            Debug.Log($"[BFBattleResolutionManager] 攻击结算：{result.Attacker.DisplayName} -> {result.Target.DisplayName}，伤害 {result.FinalDamage}，目标剩余 HP {result.TargetRemainingHp}");

            _unitManager?.HandleAttackResolved(result);

            if (result.TargetWasKilled)
            {
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
                Debug.LogWarning($"[BFBattleResolutionManager] {unit.DisplayName} 不在死亡视觉清理队列中。");
                return;
            }

            _awaitingDeathVisualCleanup.Remove(unit);
            unit.FinalizeDeathVisualCleanup();

            Debug.Log($"[BFBattleResolutionManager] {unit.DisplayName} 死亡视觉清理完成。");
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
                Debug.Log($"[BFBattleResolutionManager] 已清理 {attacker.DisplayName} 的待结算攻击。");
            }
        }
    }
}
