using NUnit.Framework;
using UnityEngine;
using Wit.Framework.UI;

namespace BF.Game.Tests.EditMode.UI
{
    public sealed class WitUIConfigTests
    {
        [Test]
        public void TryGetWindow_ReturnsRegisteredDefinitionByKey()
        {
            var config = ScriptableObject.CreateInstance<WitUIConfig>();
            var prefab = new GameObject("PF_UI_TestScreen");
            config.SetTestDefinitions(new[]
            {
                new WitUIWindowDefinition("test.screen", prefab, WitUILayer.Screen, WitUICachePolicy.CacheOnClose, true, false)
            });

            bool found = config.TryGetWindow("test.screen", out WitUIWindowDefinition definition);

            Assert.That(found, Is.True);
            Assert.That(definition.Key, Is.EqualTo("test.screen"));
            Assert.That(definition.Prefab, Is.SameAs(prefab));
            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void TryGetWindow_MissingKey_ReturnsFalseAndNullDefinition()
        {
            var config = ScriptableObject.CreateInstance<WitUIConfig>();

            bool found = config.TryGetWindow("missing.key", out WitUIWindowDefinition definition);

            Assert.That(found, Is.False);
            Assert.That(definition, Is.Null);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void ValidateDefinitions_ReportsDuplicateKeyEmptyPrefabAndDisabledUnique()
        {
            var config = ScriptableObject.CreateInstance<WitUIConfig>();
            var prefab = new GameObject("PF_UI_TestScreen");
            prefab.AddComponent<WitUIView>();
            config.SetTestDefinitions(new[]
            {
                new WitUIWindowDefinition("test.screen", prefab, WitUILayer.Screen, WitUICachePolicy.CacheOnClose, true, false),
                new WitUIWindowDefinition("test.screen", null, WitUILayer.Popup, WitUICachePolicy.DestroyOnClose, false, true)
            });

            var errors = config.ValidateDefinitions();

            Assert.That(errors, Has.Some.Contains("重复"));
            Assert.That(errors, Has.Some.Contains("prefab 为空"));
            Assert.That(errors, Has.Some.Contains("Unique=false"));
            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void ValidateDefinitions_ReportsPrefabWithoutView()
        {
            var config = ScriptableObject.CreateInstance<WitUIConfig>();
            var prefab = new GameObject("PF_UI_NoView");
            config.SetTestDefinitions(new[]
            {
                new WitUIWindowDefinition("test.no-view", prefab, WitUILayer.Screen, WitUICachePolicy.DestroyOnClose, true, false)
            });

            var errors = config.ValidateDefinitions();

            Assert.That(errors, Has.Some.Contains("缺少 WitUIView"));
            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(config);
        }
    }
}
