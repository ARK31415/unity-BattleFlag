using System;
using BF.Game.Battle.Domain.Units;
using BF.Game.Battle.Domain.Events;
using BF.Game.Battle.Rules.Units;
using NUnit.Framework;

namespace BF.Game.Tests.EditMode.Rules
{
    /// <summary>
    /// 以规则层可观察行为验证单位创建基础设施的最小合同。
    /// </summary>
    public sealed class BFBattleUnitCreationRulesTests
    {
        [Test]
        public void EffectiveMaxHPClampsAdditiveIntOverflow()
        {
            var attributes = new BFUnitAttributes(int.MaxValue, 0, 0);

            attributes.SetBonusMaxHP(1);

            Assert.That(attributes.EffectiveMaxHP, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void UnitStateExposesUnitLevelAsRuleState()
        {
            var property = typeof(BFUnitState).GetProperty("UnitLevel");

            Assert.That(property, Is.Not.Null);
            Assert.That(property.PropertyType, Is.EqualTo(typeof(int)));
        }

        [Test]
        public void BattleUnitRulesAssemblyExposesUnitStateFactory()
        {
            var factoryType = Type.GetType(
                "BF.Game.Battle.Rules.Units.BFUnitStateFactory, BF.Game.Battle.Rules");

            Assert.That(factoryType, Is.Not.Null);
        }

        [Test]
        public void UnitStateFactoryCreatesConfiguredRuleState()
        {
            var attributes = new BFUnitAttributes(30, 6, 8);
            var data = new BFUnitStateCreationData(
                "profile_001",
                BFUnitFaction.Enemy,
                BFUnitRole.Warrior,
                BFUnitTier.Elite,
                4,
                attributes,
                new BFGridPosition(2, 3));

            var state = new BFUnitStateFactory().Create("battle_unit_0001", data);

            Assert.That(state.ProfileId, Is.EqualTo("profile_001"));
            Assert.That(state.RuntimeId, Is.EqualTo("battle_unit_0001"));
            Assert.That(state.Faction, Is.EqualTo(BFUnitFaction.Enemy));
            Assert.That(state.UnitLevel, Is.EqualTo(4));
            Assert.That(state.Attributes, Is.SameAs(attributes));
            Assert.That(state.GridPosition, Is.EqualTo(new BFGridPosition(2, 3)));
            Assert.That(state.ActionState, Is.EqualTo(BFUnit_ActionState.Idle));
        }

        [Test]
        public void UnitStateRejectsInvalidFaction()
        {
            Assert.Throws<ArgumentException>(() => new BFUnitState(
                "profile_invalid",
                "runtime_invalid",
                (BFUnitFaction)99,
                BFUnitRole.Warrior,
                BFUnitTier.Normal,
                new BFUnitAttributes(10, 2, 3),
                new BFGridPosition(0, 0)));
        }
    }
}
