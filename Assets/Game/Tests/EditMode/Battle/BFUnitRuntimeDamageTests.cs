using BF.Game.Runtime.Battle.Units;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace BF.Game.Tests.EditMode.Battle
{
    /// <summary>
    /// 验证单位伤害入口、受伤事件和死亡状态切换的生命周期合同。
    /// </summary>
    public class BFUnitRuntimeDamageTests
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
        public void TakeDamage_WhenUnitSurvives_RaisesHurtReceivedOnly()
        {
            UnitRuntime unit = CreateUnit(10);
            int hurtCount = 0;
            int deathCount = 0;
            unit.HurtReceived += _ => hurtCount++;
            unit.DeathStarted += _ => deathCount++;

            unit.TakeDamage(3);

            Assert.That(unit.Stats.CurrentHP, Is.EqualTo(7));
            Assert.That(unit.Stats.IsAlive, Is.True);
            Assert.That(hurtCount, Is.EqualTo(1));
            Assert.That(deathCount, Is.Zero);
            Assert.That(unit.StateMachine.CurrentState, Is.TypeOf<UnitIdleState>());
        }

        [Test]
        public void TakeDamage_WhenDamageKillsUnit_RaisesDeathStartedAndDoesNotRaiseHurtReceived()
        {
            UnitRuntime unit = CreateUnit(10);
            int hurtCount = 0;
            int deathCount = 0;
            unit.HurtReceived += _ => hurtCount++;
            unit.DeathStarted += _ => deathCount++;

            unit.TakeDamage(10);

            Assert.That(unit.Stats.CurrentHP, Is.Zero);
            Assert.That(unit.Stats.IsAlive, Is.False);
            Assert.That(hurtCount, Is.Zero);
            Assert.That(deathCount, Is.EqualTo(1));
            Assert.That(unit.StateMachine.CurrentState, Is.TypeOf<UnitDeadState>());
        }

        [Test]
        public void ApplyResolvedDamage_WhenUnitSurvives_RaisesHurtReceivedOnly()
        {
            UnitRuntime unit = CreateUnit(10);
            int hurtCount = 0;
            int deathCount = 0;
            unit.HurtReceived += _ => hurtCount++;
            unit.DeathStarted += _ => deathCount++;

            unit.ApplyResolvedDamage(4);

            Assert.That(unit.Stats.CurrentHP, Is.EqualTo(6));
            Assert.That(hurtCount, Is.EqualTo(1));
            Assert.That(deathCount, Is.Zero);
        }

        [TestCase(0)]
        [TestCase(-5)]
        public void TakeDamage_WhenDamageIsNotPositive_DoesNotRaiseDamageEvents(int damage)
        {
            UnitRuntime unit = CreateUnit(10);
            int hurtCount = 0;
            int deathCount = 0;
            unit.HurtReceived += _ => hurtCount++;
            unit.DeathStarted += _ => deathCount++;

            unit.TakeDamage(damage);

            Assert.That(unit.Stats.CurrentHP, Is.EqualTo(10));
            Assert.That(unit.Stats.IsAlive, Is.True);
            Assert.That(hurtCount, Is.Zero);
            Assert.That(deathCount, Is.Zero);
        }

        private UnitRuntime CreateUnit(int hp)
        {
            var gameObject = new GameObject("UnitRuntime Damage Test");
            _createdObjects.Add(gameObject);

            var unit = gameObject.AddComponent<UnitRuntime>();
            unit.Stats.MaxHP = hp;
            unit.BeginBattle();

            return unit;
        }
    }
}
