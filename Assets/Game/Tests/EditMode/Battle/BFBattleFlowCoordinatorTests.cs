using System;
using System.Linq;
using BF.Game.Battle.Domain;
using BF.Game.Battle.Domain.Events;
using BF.Game.Battle.Domain.Units;
using BF.Game.Battle.Rules.Units;
using BF.Game.Runtime.Battle.AI;
using BF.Game.Runtime.Battle.Flow;
using BF.Game.Runtime.Battle.Input;
using BF.Game.Runtime.Battle.Managers;
using BF.Game.Runtime.Battle.Units;
using NUnit.Framework;
using UnityEngine;
using DomainUnitRole = BF.Game.Battle.Domain.Units.BFUnitRole;

namespace BF.Game.Tests.EditMode.Battle
{
    /// <summary>
    /// 验证 3.5 拆分后的流程组件边界：选择只保存身份，流程组件独立存在，
    /// 生产代码不再通过旧的单位门面统一持有它们。
    /// </summary>
    public sealed class BFBattleFlowCoordinatorTests
    {
        [TearDown]
        public void TearDown()
        {
            foreach (var gameObject in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (gameObject.name.Contains("BattleFlowTest"))
                    UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void SelectionController_StoresRuntimeIdentityWithoutRuntimeOrRuleStateReference()
        {
            var owner = new GameObject("BattleFlowTest.Selection");
            var controller = owner.AddComponent<BFBattleSelectionController>();

            Assert.That(controller.TrySelect("runtime-player"), Is.True);
            Assert.That(controller.SelectedRuntimeId, Is.EqualTo("runtime-player"));
            Assert.That(controller.TrySelect(""), Is.False);
            Assert.That(controller.SelectedRuntimeId, Is.EqualTo("runtime-player"));

            var fields = typeof(BFBattleSelectionController)
                .GetFields(System.Reflection.BindingFlags.Instance |
                           System.Reflection.BindingFlags.Public |
                           System.Reflection.BindingFlags.NonPublic);
            Assert.That(fields.Any(field => typeof(UnitRuntime).IsAssignableFrom(field.FieldType)), Is.False);
            Assert.That(fields.Any(field => typeof(BFUnitState).IsAssignableFrom(field.FieldType)), Is.False);
        }

        [Test]
        public void FlowComponents_AreIndependentFromLegacyFacade()
        {
            var owner = new GameObject("BattleFlowTest.Components");
            var actionCoordinator = owner.AddComponent<BFBattleActionCoordinator>();
            var selectionController = owner.AddComponent<BFBattleSelectionController>();
            var movementCoordinator = owner.AddComponent<BFBattleMovementCoordinator>();
            var enemyActionController = owner.AddComponent<BFBattleEnemyActionController>();
            var outcomeCoordinator = owner.AddComponent<BFBattleOutcomeCoordinator>();

            Assert.That(actionCoordinator, Is.Not.Null);
            Assert.That(selectionController, Is.Not.Null);
            Assert.That(movementCoordinator, Is.Not.Null);
            Assert.That(enemyActionController, Is.Not.Null);
            Assert.That(outcomeCoordinator, Is.Not.Null);
            Assert.That(typeof(BFBattleRoot).GetProperty("Unit" + "Manager"), Is.Null);
        }

        [Test]
        public void ActionCoordinator_ExposesRuntimeIdActionGateway()
        {
            Assert.That(typeof(IBFBattleActionGateway).IsAssignableFrom(typeof(BFBattleActionCoordinator)), Is.True);
            Assert.That(typeof(IBFBattleActionGateway).GetMethod(nameof(IBFBattleActionGateway.TryMove)), Is.Not.Null);
            Assert.That(typeof(IBFBattleActionGateway).GetMethod(nameof(IBFBattleActionGateway.TryAttack)), Is.Not.Null);
            Assert.That(typeof(IBFBattleActionGateway).GetMethod(nameof(IBFBattleActionGateway.TryWait)), Is.Not.Null);
        }

        [Test]
        public void EnemyActionController_DoesNotOwnRuleStateOrRuntimeUnitCollections()
        {
            var fields = typeof(BFBattleEnemyActionController)
                .GetFields(System.Reflection.BindingFlags.Instance |
                           System.Reflection.BindingFlags.Public |
                           System.Reflection.BindingFlags.NonPublic);

            Assert.That(fields.Any(field => typeof(BFUnitState).IsAssignableFrom(field.FieldType)), Is.False);
            Assert.That(fields.Any(field => typeof(UnitRuntime[]).IsAssignableFrom(field.FieldType)), Is.False);
        }

        [Test]
        public void OutcomeCoordinator_ExistsAsIndependentFlowComponent()
        {
            var owner = new GameObject("BattleFlowTest.Outcome");
            var coordinator = owner.AddComponent<BFBattleOutcomeCoordinator>();

            Assert.That(coordinator, Is.Not.Null);
            Assert.That(typeof(MonoBehaviour).IsAssignableFrom(typeof(BFBattleOutcomeCoordinator)), Is.True);
        }

        [Test]
        public void OutcomeCoordinator_ClearsSelectionWhenBattleCompletesWithRemainingActionPoints()
        {
            var owner = new GameObject("BattleFlowTest.OutcomeSelection");
            var selectionController = owner.AddComponent<BFBattleSelectionController>();
            var outcomeCoordinator = owner.AddComponent<BFBattleOutcomeCoordinator>();
            var context = new BFBattleContext("battle-outcome-selection");
            var session = new BFBattleSession(context);
            var player = new BFUnitState(
                "player-profile",
                "player-runtime",
                BFUnitFaction.Player,
                DomainUnitRole.Warrior,
                BFUnitTier.Normal,
                1,
                new BFUnitAttributes(10, 4, 4, currentHP: 10, remainingActionPoints: 4),
                new BFGridPosition(0, 0));
            var defeatedEnemy = new BFUnitState(
                "enemy-profile",
                "enemy-runtime",
                BFUnitFaction.Enemy,
                DomainUnitRole.Warrior,
                BFUnitTier.Normal,
                1,
                new BFUnitAttributes(10, 4, 4, currentHP: 0, remainingActionPoints: 4),
                new BFGridPosition(1, 0));

            Assert.That(context.TryRegisterUnit(player), Is.True);
            Assert.That(context.TryRegisterUnit(defeatedEnemy), Is.True);
            session.Start();
            Assert.That(selectionController.TrySelect(player.RuntimeId), Is.True);

            outcomeCoordinator.SetDependencies(null, session, selectionController);
            outcomeCoordinator.Evaluate();

            Assert.That(session.State, Is.EqualTo(BFBattleSessionState.Completed));
            Assert.That(selectionController.HasSelection, Is.False);
            session.Dispose();
        }
    }
}
