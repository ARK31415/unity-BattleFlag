using BF.Framework.Core.App;
using NUnit.Framework;

namespace BF.Game.Tests.EditMode.Core
{
    public class BFAppFlowControllerTests
    {
        [Test]
        public void EnterBoot_SetsStateToBoot()
        {
            var controller = new BFAppFlowController();
            controller.EnterBoot();
            Assert.That(controller.CurrentState, Is.EqualTo(BFAppFlowState.Boot));
        }
    }
}
