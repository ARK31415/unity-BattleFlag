using BF.Framework.UI.Runtime;
using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

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

        [Test]
        public void RegisterConfig_AllowsLookupById()
        {
            var registry = new BFUIRegistry();
            var prefab = new GameObject("ConfigPrefab");
            var config = ScriptableObject.CreateInstance<BFUIRegistryConfig>();

            var entries = new List<BFUIRegistryEntry>
            {
                new BFUIRegistryEntry { panelId = "ConfigPage", prefab = prefab }
            };
            var field = typeof(BFUIRegistryConfig).GetField("_entries",
                BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(config, entries);

            registry.Register(config);

            bool found = registry.TryGetPrefab("ConfigPage", out GameObject result);
            Assert.That(found, Is.True);
            Assert.That(result, Is.EqualTo(prefab));
            Object.DestroyImmediate(prefab);
            ScriptableObject.DestroyImmediate(config);
        }

        [Test]
        public void TryGetPanelId_ReturnsAttributeValue()
        {
            string panelId = BFUIRegistry.TryGetPanelId<TestPanel>();
            Assert.That(panelId, Is.EqualTo("TestPanel123"));
        }

        [BFUIPanel("TestPanel123")]
        private class TestPanel : MonoBehaviour { }
    }
}
