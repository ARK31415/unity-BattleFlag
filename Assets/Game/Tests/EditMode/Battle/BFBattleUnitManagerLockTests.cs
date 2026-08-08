using BF.Game.Battle.Domain;
using BF.Game.Battle.Rules.Units;
using BF.Game.Runtime.Battle.Factory;
using BF.Game.Runtime.Battle.Managers;
using BF.Game.Runtime.Battle.Units;
using DomainBFGridPosition = BF.Game.Battle.Domain.Units.BFGridPosition;
using DomainBFUnitAttributes = BF.Game.Battle.Domain.Units.BFUnitAttributes;
using DomainBFUnitFaction = BF.Game.Battle.Domain.Events.BFUnitFaction;
using DomainBFUnitRole = BF.Game.Battle.Domain.Units.BFUnitRole;
using DomainBFUnitState = BF.Game.Battle.Domain.Units.BFUnitState;
using DomainBFUnitTier = BF.Game.Battle.Domain.Units.BFUnitTier;
using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace BF.Game.Tests.EditMode.Battle
{
    /// <summary>
    /// 验证 UnitManager 在动作锁定期间不会改变玩家选择状态。
    /// </summary>
    public class BFBattleUnitManagerLockTests
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
        public void TrySelectUnit_ReturnsFalseWhileActionLocked()
        {
            var manager = CreateManager();
            var unit = CreatePlayerUnit("Player");
            SetActionLocked(manager, true);

            bool selected = manager.TrySelectUnit(unit);

            Assert.That(selected, Is.False);
            Assert.That(manager.SelectedUnit, Is.Null);
        }

        [Test]
        public void DeselectUnit_KeepsSelectionWhileActionLocked()
        {
            var manager = CreateManager();
            var unit = CreatePlayerUnit("Player");
            Assert.That(manager.TrySelectUnit(unit), Is.True);
            SetActionLocked(manager, true);

            manager.DeselectUnit();

            Assert.That(manager.SelectedUnit, Is.SameAs(unit));
        }

        private static BFBattleUnitManager CreateManager()
        {
            return new GameObject("UnitManager").AddComponent<BFBattleUnitManager>();
        }

        private static UnitRuntime CreatePlayerUnit(string name)
        {
            var gameObject = new GameObject(name);
            var unit = gameObject.AddComponent<UnitRuntime>();
            unit.Identity.Faction = UnitFaction.Player;

            var context = new BFBattleContext("lock-test");
            var state = new DomainBFUnitState(
                "profile-lock",
                "runtime-lock",
                DomainBFUnitFaction.Player,
                DomainBFUnitRole.Warrior,
                DomainBFUnitTier.Normal,
                1,
                new DomainBFUnitAttributes(20, 5, 8),
                new DomainBFGridPosition(1, 2));
            Assert.That(context.TryRegisterUnit(state), Is.True);
            unit.BindRuleState(
                state,
                null,
                name,
                new BFBattleUnitHandle("lock-test", state.RuntimeId));
            return unit;
        }

        private static void SetActionLocked(BFBattleUnitManager manager, bool value)
        {
            var field = typeof(BFBattleUnitManager).GetField(
                "_isActionLocked",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(manager, value);
        }
    }
}
