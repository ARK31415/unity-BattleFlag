using BF.Framework.UI.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace BF.Game.Tests.EditMode.UI
{
    public class BFUIRegistryTests
    {
        [Test]
        public void RegisterPrefab_AllowsLookupById()
        {
            var registry = new BFUIRegistry();
            var prefab = new GameObject("TestPagePrefab");

            registry.Register("TestPage", prefab);

            bool found = registry.TryGetPrefab("TestPage", out GameObject result);

            Assert.That(found, Is.True);
            Assert.That(result, Is.EqualTo(prefab));
            Object.DestroyImmediate(prefab);
        }
    }
}
