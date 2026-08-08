using System;
using System.Linq;
using BF.Game.Battle.Domain;
using BF.Game.Battle.Domain.Units;
using BF.Game.Runtime.Battle.AI;
using BF.Game.Runtime.Battle.Flow;
using BF.Game.Runtime.Battle.Input;
using BF.Game.Runtime.Battle.Managers;
using BF.Game.Runtime.Battle.Units;
using NUnit.Framework;
using UnityEngine;

namespace BF.Game.Tests.EditMode.Battle
{
    /// <summary>
    /// 验证第三阶段 3.3 的流程职责边界。
    /// 这些测试只约束公开结构和身份边界，不锁定内部协程实现。
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
        public void BattleUnitManager_ExposesUnifiedActionCoordinatorAndSelectionController()
        {
            var owner = new GameObject("BattleFlowTest.Manager");
            var manager = owner.AddComponent<BFBattleUnitManager>();
            // BFBattleUnitManager 的 RequireComponent 已经创建了唯一实例；
            // 这里解析现有组件，避免违反 DisallowMultipleComponent。
            var actionCoordinator = owner.GetComponent<BFBattleActionCoordinator>();
            var selectionController = owner.GetComponent<BFBattleSelectionController>();

            actionCoordinator.SetUnitManager(manager);
            manager.SetActionCoordinator(actionCoordinator);
            manager.SetSelectionController(selectionController);

            Assert.That(manager.ActionCoordinator, Is.SameAs(actionCoordinator));
            Assert.That(manager.SelectionController, Is.SameAs(selectionController));
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
    }
}
