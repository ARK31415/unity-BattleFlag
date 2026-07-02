using NUnit.Framework;
using System.IO;

namespace BF.Game.Tests.EditMode.Framework
{
    public class BFAssemblyDefinitionTests
    {
        [Test]
        public void RequiredAssemblyDefinitionsExist()
        {
            string[] files =
            {
                "Assets/Game/Scripts/Framework/UI/BF.Framework.UI.asmdef",
                "Assets/Game/Scripts/Framework/Core/BF.Framework.Core.asmdef",
                "Assets/Game/Scripts/Framework/SaveMission/BF.Framework.SaveMission.asmdef",
                "Assets/Game/Scripts/Framework/Inventory/BF.Framework.Inventory.asmdef",
                "Assets/Game/Scripts/BattleFlag/Runtime/BF.Game.Runtime.asmdef"
            };

            foreach (string file in files)
            {
                Assert.That(File.Exists(file), Is.True, file);
            }
        }
    }
}
