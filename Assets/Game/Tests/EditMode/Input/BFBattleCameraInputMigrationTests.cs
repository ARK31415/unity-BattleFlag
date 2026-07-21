using System.IO;
using NUnit.Framework;

namespace BF.Game.Tests.EditMode.Input
{
    public sealed class BFBattleCameraInputMigrationTests
    {
        [Test]
        public void Source_UsesBFInputManagerAndMapGroupInsteadOfOverlayContext()
        {
            string path = "Assets/Game/Scripts/BattleFlag/Runtime/Battle/Camera/BFBattleCameraController.cs";
            string source = File.ReadAllText(path);

            Assert.That(source, Does.Not.Contain("WitInputContextManager"));
            Assert.That(source, Does.Not.Contain("IsOverlayEnabled"));
            Assert.That(source, Does.Not.Contain("BattleCameraOverlay"));
            Assert.That(source, Does.Contain("BFInputManager"));
            Assert.That(source, Does.Contain("BFInputMapGroupId.BattleGameplay"));
        }
    }
}
