using NUnit.Framework;
using UnityEngine;
using Wit.Framework.UI;

namespace BF.Game.Tests.EditMode.UI
{
    public sealed class WitUIManagerCacheTests
    {
        private WitUITestFixture _fixture;

        [SetUp]
        public void SetUp()
        {
            _fixture = WitUITestFixture.Create();
        }

        [TearDown]
        public void TearDown()
        {
            _fixture.Destroy();
        }

        [Test]
        public void CacheOnClose_ReusesExistingInstanceOnNextOpen()
        {
            var prefab = CreateTestPrefab("PF_UI_Cached", out _);
            _fixture.RegisterWindow("test.cached", prefab, WitUILayer.Screen, WitUICachePolicy.CacheOnClose);

            var result1 = _fixture.Manager.Open("test.cached");
            var instance1 = result1.View;
            _fixture.Manager.Close("test.cached");

            var result2 = _fixture.Manager.Open("test.cached");
            var instance2 = result2.View;

            Assert.That(instance2, Is.SameAs(instance1));
            Assert.That(instance2.gameObject.activeSelf, Is.True);
            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void DestroyOnClose_DoesNotReuseClosedInstance()
        {
            var prefab = CreateTestPrefab("PF_UI_Destroy", out _);
            _fixture.RegisterWindow("test.destroy", prefab, WitUILayer.Screen, WitUICachePolicy.DestroyOnClose);

            var result1 = _fixture.Manager.Open("test.destroy");
            var instance1 = result1.View;
            _fixture.Manager.Close("test.destroy");

            var result2 = _fixture.Manager.Open("test.destroy");
            var instance2 = result2.View;

            Assert.That(instance2, Is.Not.SameAs(instance1));
            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void Permanent_CloseHidesButKeepsInstanceAvailable()
        {
            var prefab = CreateTestPrefab("PF_UI_Permanent", out _);
            _fixture.RegisterWindow("test.permanent", prefab, WitUILayer.Screen, WitUICachePolicy.Permanent);

            var result1 = _fixture.Manager.Open("test.permanent");
            var instance1 = result1.View;
            _fixture.Manager.Close("test.permanent");

            Assert.That(instance1.gameObject.activeSelf, Is.False);

            var result2 = _fixture.Manager.Open("test.permanent");
            var instance2 = result2.View;

            Assert.That(instance2, Is.SameAs(instance1));
            Assert.That(instance2.gameObject.activeSelf, Is.True);
            Object.DestroyImmediate(prefab);
        }

        private GameObject CreateTestPrefab(string name, out WitUIView view)
        {
            var go = new GameObject(name);
            view = go.AddComponent<WitUIView>();
            go.SetActive(false);
            return go;
        }
    }
}
