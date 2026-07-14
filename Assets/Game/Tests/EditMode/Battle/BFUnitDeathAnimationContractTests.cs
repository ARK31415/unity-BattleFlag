using NUnit.Framework;
using System.IO;

namespace BF.Game.Tests.EditMode.Battle
{
    public class BFUnitDeathAnimationContractTests
    {
        [TestCase("Assets/Game/Animations/Units/Human/AN_BF_HumanDeath.anim")]
        [TestCase("Assets/Game/Animations/Units/Wizard/AN_BF_UnitWizardDead.anim")]
        public void DeathClip_DoesNotLoop(string clipPath)
        {
            string yaml = File.ReadAllText(clipPath);

            Assert.That(yaml, Does.Contain("m_LoopTime: 0"));
        }

        [TestCase("Assets/Game/Animations/Units/Human/AC_BF_UnitHuman.controller")]
        [TestCase("Assets/Game/Animations/Units/Wizard/AC_BF_UnitWizard.controller")]
        public void DeathTransition_CannotTransitionToSelf(string controllerPath)
        {
            string yaml = File.ReadAllText(controllerPath);
            int conditionIndex = yaml.IndexOf("m_ConditionEvent: IsDead", System.StringComparison.Ordinal);
            Assert.That(conditionIndex, Is.GreaterThanOrEqualTo(0));

            int selfTransitionIndex = yaml.IndexOf("m_CanTransitionToSelf:", conditionIndex, System.StringComparison.Ordinal);
            Assert.That(selfTransitionIndex, Is.GreaterThanOrEqualTo(0));

            string transitionTail = yaml.Substring(selfTransitionIndex, "m_CanTransitionToSelf: 0".Length);
            Assert.That(transitionTail, Is.EqualTo("m_CanTransitionToSelf: 0"));
        }
    }
}
