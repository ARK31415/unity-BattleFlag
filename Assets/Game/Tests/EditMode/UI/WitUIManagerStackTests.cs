using NUnit.Framework;
using UnityEngine;
using Wit.Framework.UI;

namespace BF.Game.Tests.EditMode.UI
{
    public sealed class WitUIManagerStackTests
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
        public void Open_Screen_InstantiatesUnderScreenLayer()
        {
            var prefab = CreateTestPrefab("PF_UI_TestScreen", out _);
            _fixture.RegisterWindow("test.screen", prefab, WitUILayer.Screen);

            var result = _fixture.Manager.Open("test.screen");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.View.transform.parent, Is.SameAs(_fixture.Screen));
            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void Open_Screen_PassesContextToView()
        {
            var ctx = new object();
            var prefab = CreateTestPrefab("PF_UI_TestScreen", out _);
            _fixture.RegisterWindow("test.screen", prefab, WitUILayer.Screen);

            var result = _fixture.Manager.Open("test.screen", ctx);

            Assert.That(result.View.Context, Is.SameAs(ctx));
            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void TryGetOpenView_ReturnsOpenedView()
        {
            var prefab = CreateTestPrefab("PF_UI_TestScreen", out _);
            _fixture.RegisterWindow("test.screen", prefab, WitUILayer.Screen);

            var result = _fixture.Manager.Open("test.screen");
            bool found = _fixture.Manager.TryGetOpenView("test.screen", out WitUIView view);

            Assert.That(found, Is.True);
            Assert.That(view, Is.SameAs(result.View));
            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void Open_MissingKey_ReturnsFailure()
        {
            var result = _fixture.Manager.Open("missing.key");

            Assert.That(result.Succeeded, Is.False);
            Assert.That(_fixture.Manager.OpenViewCount, Is.EqualTo(0));
        }

        [Test]
        public void OpenViewCount_ReflectsCurrentlyOpenViews()
        {
            var prefab = CreateTestPrefab("PF_UI_TestScreen", out _);
            _fixture.RegisterWindow("test.screen", prefab, WitUILayer.HUD);

            _fixture.Manager.Open("test.screen");
            Assert.That(_fixture.Manager.OpenViewCount, Is.EqualTo(1));

            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void Open_SecondScreen_DisablesFirstScreenInteractable()
        {
            var prefabA = CreateTestPrefab("PF_UI_ScreenA", out _);
            var prefabB = CreateTestPrefab("PF_UI_ScreenB", out _);
            _fixture.RegisterWindow("screen.a", prefabA, WitUILayer.Screen);
            _fixture.RegisterWindow("screen.b", prefabB, WitUILayer.Screen);

            _fixture.Manager.Open("screen.a");
            _fixture.Manager.TryGetOpenView("screen.a", out var instanceA);
            _fixture.Manager.Open("screen.b");
            _fixture.Manager.TryGetOpenView("screen.b", out var instanceB);

            Assert.That(_fixture.Manager.ScreenStackCount, Is.EqualTo(2));
            Assert.That(instanceA.GetComponent<CanvasGroup>().interactable, Is.False);
            Assert.That(instanceB.GetComponent<CanvasGroup>().interactable, Is.True);
            Object.DestroyImmediate(prefabA);
            Object.DestroyImmediate(prefabB);
        }

        [Test]
        public void Open_Popup_IncrementsPopupStackWithoutAffectingScreenStack()
        {
            var screenPrefab = CreateTestPrefab("PF_UI_Screen", out _);
            var popupPrefab = CreateTestPrefab("PF_UI_Popup", out _);
            _fixture.RegisterWindow("test.screen", screenPrefab, WitUILayer.Screen);
            _fixture.RegisterWindow("test.popup", popupPrefab, WitUILayer.Popup);

            _fixture.Manager.Open("test.screen");
            Assert.That(_fixture.Manager.ScreenStackCount, Is.EqualTo(1));

            _fixture.Manager.Open("test.popup");
            Assert.That(_fixture.Manager.ScreenStackCount, Is.EqualTo(1));
            Assert.That(_fixture.Manager.PopupStackCount, Is.EqualTo(1));

            Object.DestroyImmediate(screenPrefab);
            Object.DestroyImmediate(popupPrefab);
        }

        [Test]
        public void Back_ClosesPopupBeforeScreen()
        {
            var screenPrefab = CreateTestPrefab("PF_UI_Screen", out _);
            var popupPrefab = CreateTestPrefab("PF_UI_Popup", out _);
            _fixture.RegisterWindow("test.screen", screenPrefab, WitUILayer.Screen);
            _fixture.RegisterWindow("test.popup", popupPrefab, WitUILayer.Popup);

            _fixture.Manager.Open("test.screen");
            _fixture.Manager.Open("test.popup");
            Assert.That(_fixture.Manager.PopupStackCount, Is.EqualTo(1));

            bool backResult = _fixture.Manager.Back();
            Assert.That(backResult, Is.True);
            Assert.That(_fixture.Manager.PopupStackCount, Is.EqualTo(0));
            Assert.That(_fixture.Manager.ScreenStackCount, Is.EqualTo(1));

            bool backResult2 = _fixture.Manager.Back();
            Assert.That(backResult2, Is.True);
            Assert.That(_fixture.Manager.ScreenStackCount, Is.EqualTo(0));

            Object.DestroyImmediate(screenPrefab);
            Object.DestroyImmediate(popupPrefab);
        }

        [Test]
        public void Open_HUD_DoesNotEnterAnyStack()
        {
            var hudPrefab = CreateTestPrefab("PF_UI_HUD", out _);
            _fixture.RegisterWindow("test.hud", hudPrefab, WitUILayer.HUD);

            _fixture.Manager.Open("test.hud");

            Assert.That(_fixture.Manager.ScreenStackCount, Is.EqualTo(0));
            Assert.That(_fixture.Manager.PopupStackCount, Is.EqualTo(0));
            Assert.That(_fixture.Manager.OpenViewCount, Is.EqualTo(1));
            Object.DestroyImmediate(hudPrefab);
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
