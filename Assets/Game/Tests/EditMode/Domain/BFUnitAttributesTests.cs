using BF.Game.Battle.Domain.Units;
using NUnit.Framework;

namespace BF.Game.Tests.EditMode.Domain
{
    /// <summary>
    /// 验证规则层单位属性的基础值、加成值、最终值和动态当前值边界。
    /// </summary>
    public sealed class BFUnitAttributesTests
    {
        [Test]
        public void EffectiveValues_AddBaseAndBonusWithoutChangingBaseValues()
        {
            var attributes = new BFUnitAttributes(100, 5, 20);

            attributes.SetBonusMaxHP(25);
            attributes.SetBonusMaxActionPoints(2);
            attributes.SetBonusAttackPower(8);

            Assert.That(attributes.BaseMaxHP, Is.EqualTo(100));
            Assert.That(attributes.BaseMaxActionPoints, Is.EqualTo(5));
            Assert.That(attributes.BaseAttackPower, Is.EqualTo(20));
            Assert.That(attributes.EffectiveMaxHP, Is.EqualTo(125));
            Assert.That(attributes.EffectiveMaxActionPoints, Is.EqualTo(7));
            Assert.That(attributes.EffectiveAttackPower, Is.EqualTo(28));
        }

        [Test]
        public void MaxHPIncreaseDoesNotRestoreCurrentHPAndDecreaseClampsIt()
        {
            var attributes = new BFUnitAttributes(100, 5, 20, currentHP: 60);

            attributes.SetBonusMaxHP(40);
            Assert.That(attributes.CurrentHP, Is.EqualTo(60));

            attributes.SetBonusMaxHP(-80);
            Assert.That(attributes.EffectiveMaxHP, Is.EqualTo(20));
            Assert.That(attributes.CurrentHP, Is.EqualTo(20));
        }

        [Test]
        public void NegativeBonusCannotMakeEffectiveValueNegative()
        {
            var attributes = new BFUnitAttributes(10, 3, 5);

            attributes.SetBonusMaxHP(-20);
            attributes.SetBonusMaxActionPoints(-10);
            attributes.SetBonusAttackPower(-10);

            Assert.That(attributes.EffectiveMaxHP, Is.Zero);
            Assert.That(attributes.EffectiveMaxActionPoints, Is.Zero);
            Assert.That(attributes.EffectiveAttackPower, Is.Zero);
        }

        [Test]
        public void CurrentHPAndActionPointsRemainWithinEffectiveBounds()
        {
            var attributes = new BFUnitAttributes(100, 5, 20);

            attributes.SetCurrentHP(200);
            attributes.SetRemainingActionPoints(20);
            Assert.That(attributes.CurrentHP, Is.EqualTo(100));
            Assert.That(attributes.RemainingActionPoints, Is.EqualTo(5));

            attributes.SetCurrentHP(-1);
            attributes.SetRemainingActionPoints(-1);
            Assert.That(attributes.CurrentHP, Is.Zero);
            Assert.That(attributes.RemainingActionPoints, Is.Zero);
            Assert.That(attributes.IsAlive, Is.False);
        }
    }
}
