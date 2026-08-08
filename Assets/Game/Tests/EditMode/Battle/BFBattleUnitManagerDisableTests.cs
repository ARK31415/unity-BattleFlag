using BF.Game.Battle.Domain;
using BF.Game.Battle.Domain.Units;
using BF.Game.Battle.Rules.Units;
using BF.Game.Runtime.Battle.Factory;
using BF.Game.Runtime.Battle.Managers;
using BF.Game.Runtime.Battle.Units;
using NUnit.Framework;
using UnityEngine;
using DomainUnitFaction = BF.Game.Battle.Domain.Events.BFUnitFaction;
using DomainUnitRole = BF.Game.Battle.Domain.Units.BFUnitRole;

namespace BF.Game.Tests.EditMode.Battle
{
    /// <summary>
    /// 验证 UnitManager 禁用时的清理责任（Spec 3.2 6.6）：
    /// 命中前禁用只清理未完成攻击上下文并恢复规则状态，不消耗 AP、不造成伤害、不发布成功事实。
    /// </summary>
    public sealed class BFBattleUnitManagerDisableTests
    {
        [TearDown]
        public void TearDown()
        {
            foreach (var manager in Object.FindObjectsByType<BFBattleUnitManager>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(manager.gameObject);
            }

            foreach (var unit in Object.FindObjectsByType<UnitRuntime>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(unit.gameObject);
            }
        }

        [Test]
        public void OnDisable_ClearsQueuedAttackAndRestoresRuleStateWithoutConsumingActionPoints()
        {
            var context = new BFBattleContext("disable-test");
            var attackerState = new BFUnitState(
                "profile-attacker",
                "runtime-attacker",
                DomainUnitFaction.Player,
                DomainUnitRole.Warrior,
                BFUnitTier.Normal,
                1,
                new BFUnitAttributes(20, 5, 8, baseAttackRange: 3, baseAttackCost: 2),
                new BFGridPosition(1, 2));
            var targetState = new BFUnitState(
                "profile-target",
                "runtime-target",
                DomainUnitFaction.Enemy,
                DomainUnitRole.Warrior,
                BFUnitTier.Normal,
                1,
                new BFUnitAttributes(20, 5, 8),
                new BFGridPosition(3, 3));
            Assert.That(context.TryRegisterUnit(attackerState), Is.True);
            Assert.That(context.TryRegisterUnit(targetState), Is.True);

            var session = new BFBattleSession(context);
            var manager = new GameObject("UnitManager").AddComponent<BFBattleUnitManager>();
            manager.SetBattleSession(session);

            var attacker = CreateUnit("Attacker");
            attacker.BindRuleState(
                attackerState,
                null,
                "Attacker",
                new BFBattleUnitHandle("disable-test", attackerState.RuntimeId));
            var target = CreateUnit("Target");
            target.BindRuleState(
                targetState,
                null,
                "Target",
                new BFBattleUnitHandle("disable-test", targetState.RuntimeId));
            manager.RegisterUnit(attacker);

            // 攻击开始：规则状态进入 Attack，但 AP 未消耗。
            var rules = new BFUnitStateRules(context);
            var startResult = rules.TryStartAttack(
                new AttackRequest(attackerState.RuntimeId, targetState.RuntimeId, 2));
            Assert.That(startResult.Succeeded, Is.True, startResult.FailureReason);
            Assert.That(attackerState.ActionState, Is.EqualTo(BFUnit_ActionState.Attack));
            attacker.Combat.BeginQueuedAttack(target);

            // 模拟组件被禁用时的清理责任（EditMode 不派发生命周期回调，直接调用清理入口）。
            manager.CleanupInterruptedActions();

            Assert.That(attacker.Combat.HasQueuedAttack, Is.False);
            Assert.That(attackerState.ActionState, Is.EqualTo(BFUnit_ActionState.Idle));
            Assert.That(attackerState.Attributes.RemainingActionPoints, Is.EqualTo(5));
            Assert.That(targetState.Attributes.CurrentHP, Is.EqualTo(20));
            Assert.That(manager.IsActionLocked, Is.False);
        }

        private static UnitRuntime CreateUnit(string name)
        {
            var gameObject = new GameObject(name);
            return gameObject.AddComponent<UnitRuntime>();
        }
    }
}
