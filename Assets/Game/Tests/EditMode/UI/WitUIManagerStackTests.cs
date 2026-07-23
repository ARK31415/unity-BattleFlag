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
        public void Open_AlreadyOpenWindow_ReusesInstanceAndCallsOnReopened()
        {
            var firstContext = new TestContext("first");
            var secondContext = new TestContext("second");
            var prefab = CreateTrackingPrefab("PF_UI_Reopen", out _);
            _fixture.RegisterWindow("test.reopen", prefab, WitUILayer.Screen);

            var first = _fixture.Manager.Open("test.reopen", firstContext);
            var view = (TrackingView)first.View;
            var second = _fixture.Manager.Open("test.reopen", secondContext);

            Assert.That(second.Succeeded, Is.True);
            Assert.That(second.ReusedExisting, Is.True);
            Assert.That(second.View, Is.SameAs(first.View));
            Assert.That(view.OpenedCount, Is.EqualTo(1));
            Assert.That(view.ReopenedCount, Is.EqualTo(1));
            Assert.That(view.LastContext, Is.SameAs(secondContext));
            Assert.That(_fixture.Manager.OpenViewCount, Is.EqualTo(1));
            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void Open_TypedViewRejectsMismatchedContext()
        {
            var prefab = CreateTypedPrefab("PF_UI_Typed", out _);
            _fixture.RegisterWindow("test.typed", prefab, WitUILayer.Screen);

            var result = _fixture.Manager.Open("test.typed", 123);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain(nameof(StringContext)));
            Assert.That(_fixture.Manager.OpenViewCount, Is.EqualTo(0));
            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void Open_EmptyContextViewNormalizesNullContext()
        {
            var prefab = CreateEmptyContextPrefab("PF_UI_Empty", out _);
            _fixture.RegisterWindow("test.empty", prefab, WitUILayer.Screen);

            var result = _fixture.Manager.Open("test.empty");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.View.Context, Is.SameAs(WitEmptyContext.Instance));
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

        private GameObject CreateTrackingPrefab(string name, out TrackingView view)
        {
            var go = new GameObject(name);
            view = go.AddComponent<TrackingView>();
            go.SetActive(false);
            return go;
        }

        private GameObject CreateTypedPrefab(string name, out TypedStringView view)
        {
            var go = new GameObject(name);
            view = go.AddComponent<TypedStringView>();
            go.SetActive(false);
            return go;
        }

        private GameObject CreateEmptyContextPrefab(string name, out EmptyContextView view)
        {
            var go = new GameObject(name);
            view = go.AddComponent<EmptyContextView>();
            go.SetActive(false);
            return go;
        }

        private sealed class TestContext
        {
            public TestContext(string name)
            {
                Name = name;
            }

            public string Name { get; }
        }

        private sealed class StringContext { }

        private sealed class TrackingView : WitUIView
        {
            public int OpenedCount { get; private set; }
            public int ReopenedCount { get; private set; }
            public object LastContext { get; private set; }

            protected override void OnOpened(object context)
            {
                OpenedCount++;
                LastContext = context;
            }

            protected override void OnReopened(object context)
            {
                ReopenedCount++;
                LastContext = context;
            }
        }

        private sealed class TypedStringView : WitUIView<StringContext> { }

        private sealed class EmptyContextView : WitUIView<WitEmptyContext> { }
    }
}
