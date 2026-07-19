using BF.Game.Runtime.Battle.Managers;
using BF.Game.Runtime.Battle.Units;
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
            unit.BeginBattle();
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
