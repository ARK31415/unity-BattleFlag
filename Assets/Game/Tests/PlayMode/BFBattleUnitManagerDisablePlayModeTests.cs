using BF.Game.Battle.Domain;
using BF.Game.Battle.Domain.Units;
using BF.Game.Battle.Rules.Units;
using BF.Game.Runtime.Battle.Factory;
using BF.Game.Runtime.Battle.Managers;
using BF.Game.Runtime.Battle.Units;
using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using DomainUnitFaction = BF.Game.Battle.Domain.Events.BFUnitFaction;
using DomainUnitRole = BF.Game.Battle.Domain.Units.BFUnitRole;

namespace BF.Game.Tests.PlayMode.Battle
{
    /// <summary>
    /// 验证真实 Unity SetActive(false) 生命周期会通过适配层恢复未完成规则行动。
    /// </summary>
    public sealed class BFBattleUnitManagerDisablePlayModeTests
    {
        [UnityTest]
        public IEnumerator RuntimeSetActiveFalse_RestoresInterruptedRuleStateThroughUnitManager()
        {
            var context = new BFBattleContext("disable-runtime-test");
            var attackerState = new BFUnitState(
                "profile-attacker",
                "runtime-attacker",
                DomainUnitFaction.Player,
                DomainUnitRole.Warrior,
                BFUnitTier.Normal,
                new BFUnitAttributes(20, 5, 8, baseAttackRange: 3, baseAttackCost: 2),
                new BFGridPosition(1, 2));
            var targetState = new BFUnitState(
                "profile-target",
                "runtime-target",
                DomainUnitFaction.Enemy,
                DomainUnitRole.Warrior,
                BFUnitTier.Normal,
                new BFUnitAttributes(20, 5, 8),
                new BFGridPosition(3, 3));
            Assert.That(context.TryRegisterUnit(attackerState), Is.True);
            Assert.That(context.TryRegisterUnit(targetState), Is.True);

            var session = new BFBattleSession(context);
            var managerObject = new GameObject("UnitManager");
            var manager = managerObject.AddComponent<BFBattleUnitManager>();

            var attackerObject = new GameObject("Attacker");
            var attacker = attackerObject.AddComponent<UnitRuntime>();
            attacker.BindRuleState(
                attackerState,
                null,
                "Attacker",
                new BFBattleUnitHandle("disable-runtime-test", attackerState.RuntimeId));
            var targetObject = new GameObject("Target");
            var target = targetObject.AddComponent<UnitRuntime>();
            target.BindRuleState(
                targetState,
                null,
                "Target",
                new BFBattleUnitHandle("disable-runtime-test", targetState.RuntimeId));

            manager.SetBattleSession(session);
            manager.RegisterUnit(attacker);

            var rules = new BFUnitStateRules(context);
            Assert.That(
                rules.TryStartAttack(new AttackRequest(attackerState.RuntimeId, targetState.RuntimeId, 2)).Succeeded,
                Is.True);
            Assert.That(attacker.Combat.BeginQueuedAttack(target), Is.True);

            attackerObject.SetActive(false);
            yield return null;

            Assert.That(attackerState.ActionState, Is.EqualTo(BFUnit_ActionState.Idle));
            Assert.That(attackerState.Attributes.RemainingActionPoints, Is.EqualTo(5));
            Assert.That(attacker.Combat.HasQueuedAttack, Is.False);

            Object.Destroy(managerObject);
            Object.Destroy(attackerObject);
            Object.Destroy(targetObject);
        }
    }
}
