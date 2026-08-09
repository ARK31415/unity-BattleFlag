using BF.Game.Runtime.Battle.Commands;
using BF.Game.Battle.Domain;
using BF.Game.Battle.Rules.Units;
using BF.Game.Runtime.Battle.Data;
using BF.Game.Runtime.Battle.Factory;
using BF.Game.Runtime.Battle.Units;
using DomainBFGridPosition = BF.Game.Battle.Domain.Units.BFGridPosition;
using DomainBFUnitAttributes = BF.Game.Battle.Domain.Units.BFUnitAttributes;
using DomainBFUnitFaction = BF.Game.Battle.Domain.Events.BFUnitFaction;
using DomainBFUnitRole = BF.Game.Battle.Domain.Units.BFUnitRole;
using DomainBFUnitState = BF.Game.Battle.Domain.Units.BFUnitState;
using DomainBFUnitTier = BF.Game.Battle.Domain.Units.BFUnitTier;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace BF.Game.Tests.EditMode.Battle
{
    /// <summary>
    /// 验证 UnitRuntime 拆分后的公开合同：根只暴露子组件入口，业务能力由职责组件提供。
    /// </summary>
    public class BFUnitRuntimeComponentSplitTests
    {
        private readonly List<GameObject> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _createdObjects.Count; i++)
            {
                if (_createdObjects[i] != null)
                {
                    Object.DestroyImmediate(_createdObjects[i]);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public void UnitRuntime_ExposesReadonlyRuntimeComponentEntries()
        {
            UnitRuntime unit = CreateUnit("Split API Test");
            unit.Identity.DisplayName = "Player Mage";
            unit.Identity.Faction = UnitFaction.Player;
            unit.Identity.Role = BFUnitRole.Mage;
            unit.Stats.MaxHP = 24;
            unit.Stats.Attack = 11;
            unit.Stats.AttackRange = 2;
            unit.Stats.AttackCost = 3;
            unit.Stats.MaxActionPoints = 6;
            unit.Grid.SetGridPosition(new Vector2Int(1, 5));

            unit.BeginBattle();

            Assert.That(unit.Identity.DisplayName, Is.EqualTo("Player Mage"));
            Assert.That(unit.Identity.Faction, Is.EqualTo(UnitFaction.Player));
            Assert.That(unit.Identity.Role, Is.EqualTo(BFUnitRole.Mage));
            Assert.That(unit.Stats.MaxHP, Is.EqualTo(24));
            Assert.That(unit.Stats.Attack, Is.EqualTo(11));
            Assert.That(unit.Stats.AttackRange, Is.EqualTo(2));
            Assert.That(unit.Stats.AttackCost, Is.EqualTo(3));
            Assert.That(unit.Grid.GridPosition, Is.EqualTo(new Vector2Int(1, 5)));
            Assert.That(unit.StateMachine.CurrentState, Is.TypeOf<BFUnit_PresentationIdleState>());
        }

        [Test]
        public void ActionPoints_AreProjectedFromRuleStateAndNotWritableByRuntime()
        {
            var context = new BFBattleContext("ap-projection-test");
            var state = new DomainBFUnitState(
                "profile-test",
                "runtime-test",
                DomainBFUnitFaction.Player,
                DomainBFUnitRole.Warrior,
                DomainBFUnitTier.Normal,
                1,
                new DomainBFUnitAttributes(20, 5, 8),
                new DomainBFGridPosition(1, 2));
            Assert.That(context.TryRegisterUnit(state), Is.True);

            var unit = CreateUnit("AP Test");
            unit.BindRuleState(state, null, "AP Unit", new BFBattleUnitHandle("ap-projection-test", state.RuntimeId));

            Assert.That(unit.Stats.RemainingActionPoints, Is.EqualTo(5));

            var rules = new BFUnitStateRules(context);
            Assert.That(rules.TryConsumeActionPoints(state.RuntimeId, 2), Is.True);
            unit.RefreshRuleStateProjection();

            Assert.That(unit.Stats.RemainingActionPoints, Is.EqualTo(3));
            Assert.That(unit.Stats.HasActed, Is.False);
        }

        [Test]
        public void QueuedAttackContext_IsOwnedByCombatRuntimeAndConsumedOnce()
        {
            UnitRuntime attacker = CreateUnit("Attacker");
            UnitRuntime target = CreateUnit("Target");
            attacker.BeginBattle();
            target.BeginBattle();

            bool started = attacker.Combat.BeginQueuedAttack(target);
            bool consumed = attacker.Combat.TryConsumeQueuedAttack(attacker, out BFAttackContext context);
            bool consumedAgain = attacker.Combat.TryConsumeQueuedAttack(attacker, out _);

            Assert.That(started, Is.True);
            Assert.That(consumed, Is.True);
            Assert.That(consumedAgain, Is.False);
            Assert.That(attacker.Combat.HasQueuedAttack, Is.False);
            Assert.That(context.Attacker, Is.SameAs(attacker));
            Assert.That(context.Target, Is.SameAs(target));

            attacker.Combat.ClearQueuedAttack();
        }

        [Test]
        public void StateMachineRuntime_OwnsFormalStateChanges()
        {
            UnitRuntime unit = CreateUnit("State Test");
            unit.BeginBattle();

            unit.StateMachine.ChangeState(unit.StateMachine.AttackState);

            Assert.That(unit.StateMachine.CurrentState, Is.TypeOf<BFUnit_PresentationAttackState>());

            unit.StateMachine.ChangeState(unit.StateMachine.IdleState);

            Assert.That(unit.StateMachine.CurrentState, Is.TypeOf<BFUnit_PresentationIdleState>());
        }

        [Test]
        public void GridRuntime_CapturesSpawnGridPositionWhenFirstPositionIsSet()
        {
            UnitRuntime unit = CreateUnit("Grid Test");

            unit.Grid.SetGridPosition(new Vector2Int(2, 3));
            unit.Grid.SetGridPosition(new Vector2Int(4, 5));

            Assert.That(unit.Grid.GridPosition, Is.EqualTo(new Vector2Int(4, 5)));
            Assert.That(unit.Grid.SpawnGridPosition, Is.EqualTo(new Vector2Int(2, 3)));
        }

        [Test]
        public void UnitRuntime_DoesNotExposeOldBusinessPassthroughApi()
        {
            var type = typeof(UnitRuntime);
            string[] oldProperties =
            {
                "DisplayName",
                "Faction",
                "Role",
                "MaxHP",
                "CurrentHP",
                "Attack",
                "AttackRange",
                "AttackCost",
                "MaxActionPoints",
                "RemainingActionPoints",
                "HasActed",
                "IsAlive",
                "GridPosition",
                "SpawnGridPosition",
                "CurrentState",
                "HasQueuedAttack"
            };
            string[] oldMethods =
            {
                "ResetTurnActions",
                "ConsumeActionPoints",
                "BeginQueuedAttack",
                "TryConsumeQueuedAttack",
                "NotifyAttackResolved",
                "ChangeState",
                "GetMoveState",
                "GetAttackState",
                "GetIdleState"
            };

            foreach (string propertyName in oldProperties)
            {
                Assert.That(type.GetProperty(propertyName), Is.Null, propertyName);
            }

            foreach (string methodName in oldMethods)
            {
                Assert.That(type.GetMethod(methodName), Is.Null, methodName);
            }
        }

        [Test]
        public void BoundRuleState_RefreshProjectsRuleChangesWithoutWritingBack()
        {
            var context = new BFBattleContext("projection-test");
            var state = new DomainBFUnitState(
                "profile-test",
                "runtime-test",
                DomainBFUnitFaction.Player,
                DomainBFUnitRole.Warrior,
                DomainBFUnitTier.Normal,
                1,
                new DomainBFUnitAttributes(20, 5, 8, 12, 3),
                new DomainBFGridPosition(1, 2));
            Assert.That(context.TryRegisterUnit(state), Is.True);

            var unit = CreateUnit("Rule Projection Test");
            unit.BindRuleState(
                state,
                null,
                "Rule Unit",
                new BFBattleUnitHandle("projection-test", state.RuntimeId));

            var rules = new BFUnitStateRules(context);
            Assert.That(rules.TryApplyDamage(state.RuntimeId, 4, out _), Is.True);
            Assert.That(rules.TryConsumeActionPoints(state.RuntimeId, 2), Is.True);
            Assert.That(
                rules.TrySetGridPosition(state.RuntimeId, new DomainBFGridPosition(4, 7)),
                Is.True);

            unit.RefreshRuleStateProjection();

            Assert.That(unit.Stats.CurrentHP, Is.EqualTo(state.Attributes.CurrentHP));
            Assert.That(unit.Stats.RemainingActionPoints, Is.EqualTo(state.Attributes.RemainingActionPoints));
            Assert.That(unit.Grid.GridPosition, Is.EqualTo(new Vector2Int(4, 7)));
            Assert.That(unit.Grid.SpawnGridPosition, Is.EqualTo(new Vector2Int(1, 2)));
            Assert.That(state.Attributes.CurrentHP, Is.EqualTo(8));
            Assert.That(state.Attributes.RemainingActionPoints, Is.EqualTo(1));
        }

        private UnitRuntime CreateUnit(string name)
        {
            var gameObject = new GameObject(name);
            _createdObjects.Add(gameObject);
            return gameObject.AddComponent<UnitRuntime>();
        }

    }
}
