using BF.Game.Runtime.Battle.Commands;
using BF.Game.Runtime.Battle.Units;
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
            unit.Grid.GridPosition = new Vector2Int(1, 5);

            unit.BeginBattle();

            Assert.That(unit.Identity.DisplayName, Is.EqualTo("Player Mage"));
            Assert.That(unit.Identity.Faction, Is.EqualTo(UnitFaction.Player));
            Assert.That(unit.Identity.Role, Is.EqualTo(BFUnitRole.Mage));
            Assert.That(unit.Stats.MaxHP, Is.EqualTo(24));
            Assert.That(unit.Stats.CurrentHP, Is.EqualTo(24));
            Assert.That(unit.Stats.Attack, Is.EqualTo(11));
            Assert.That(unit.Stats.AttackRange, Is.EqualTo(2));
            Assert.That(unit.Stats.AttackCost, Is.EqualTo(3));
            Assert.That(unit.Stats.RemainingActionPoints, Is.EqualTo(6));
            Assert.That(unit.Grid.GridPosition, Is.EqualTo(new Vector2Int(1, 5)));
            Assert.That(unit.StateMachine.CurrentState, Is.TypeOf<UnitIdleState>());
        }

        [Test]
        public void MaxHPChange_DoesNotImplicitlyHealCurrentHP()
        {
            UnitRuntime unit = CreateUnit("HP Rule Test");
            unit.Stats.MaxHP = 20;
            unit.BeginBattle();

            unit.Stats.CurrentHP = 8;
            unit.Stats.MaxHP = 40;

            Assert.That(unit.Stats.CurrentHP, Is.EqualTo(8));
            Assert.That(unit.Stats.MaxHP, Is.EqualTo(40));
        }

        [Test]
        public void ActionPoints_AreOwnedByStatsRuntime()
        {
            UnitRuntime unit = CreateUnit("AP Test");
            unit.Stats.MaxActionPoints = 6;
            unit.BeginBattle();

            unit.Stats.ConsumeActionPoints(2);

            Assert.That(unit.Stats.RemainingActionPoints, Is.EqualTo(4));

            unit.BeginTurn();

            Assert.That(unit.Stats.RemainingActionPoints, Is.EqualTo(6));
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

            Assert.That(unit.StateMachine.CurrentState, Is.TypeOf<UnitAttackState>());

            unit.StateMachine.ChangeState(unit.StateMachine.IdleState);

            Assert.That(unit.StateMachine.CurrentState, Is.TypeOf<UnitIdleState>());
        }

        [Test]
        public void GridRuntime_CapturesSpawnGridPositionWhenFirstPositionIsSet()
        {
            UnitRuntime unit = CreateUnit("Grid Test");

            unit.Grid.GridPosition = new Vector2Int(2, 3);
            unit.Grid.GridPosition = new Vector2Int(4, 5);

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

        private UnitRuntime CreateUnit(string name)
        {
            var gameObject = new GameObject(name);
            _createdObjects.Add(gameObject);
            return gameObject.AddComponent<UnitRuntime>();
        }

    }
}
