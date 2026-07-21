using NUnit.Framework;
using UnityEngine;
using Wit.Framework.UI;

namespace BF.Game.Tests.EditMode.UI
{
    public sealed class WitUIInputCoordinatorTests
    {
        [Test]
        public void WitUIRoot_ReturnsConfiguredLayerRoots()
        {
            var rootGo = new GameObject("UIRoot");
            var hud = new GameObject("HUDLayer").transform;
            var screen = new GameObject("ScreenLayer").transform;
            hud.SetParent(rootGo.transform, false);
            screen.SetParent(rootGo.transform, false);

            var root = rootGo.AddComponent<WitUIRoot>();
            root.SetTestLayerRoots(hud, screen, null, null, null, null);

            Assert.That(root.GetLayerRoot(WitUILayer.HUD), Is.SameAs(hud));
            Assert.That(root.GetLayerRoot(WitUILayer.Screen), Is.SameAs(screen));
            Object.DestroyImmediate(rootGo);
        }

        [Test]
        public void InputCoordinator_ReceivesStateAfterOpenAndClose()
        {
            var fixture = WitUITestFixture.Create();
            var coordinator = new RecordingInputCoordinator();
            fixture.Manager.Configure(fixture.Config, fixture.Root, coordinator);

            var screenPrefab = CreateTestPrefab("PF_UI_Screen", out _);
            fixture.RegisterWindow("test.screen", screenPrefab, WitUILayer.Screen);
            fixture.Manager.Open("test.screen");

            Assert.That(coordinator.CallCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(coordinator.LastHasAnyUI, Is.True);

            fixture.Manager.Close("test.screen");
            Assert.That(coordinator.LastHasAnyUI, Is.False);

            Object.DestroyImmediate(screenPrefab);
            fixture.Destroy();
        }

        [Test]
        public void ModalPopup_ShowsModalBlockerUntilClosed()
        {
            var fixture = WitUITestFixture.Create();

            var screenPrefab = CreateTestPrefab("PF_UI_Screen", out _);
            var popupPrefab = CreateTestPrefab("PF_UI_Popup", out _);
            fixture.RegisterWindow("test.screen", screenPrefab, WitUILayer.Screen);
            fixture.RegisterWindow("test.popup", popupPrefab, WitUILayer.Popup,
                WitUICachePolicy.DestroyOnClose, true, modal: true);

            fixture.Manager.Open("test.screen");
            Assert.That(fixture.ModalBlocker.interactable, Is.False);

            fixture.Manager.Open("test.popup");
            Assert.That(fixture.ModalBlocker.interactable, Is.True);

            fixture.Manager.Close("test.popup");
            Assert.That(fixture.ModalBlocker.interactable, Is.False);

            Object.DestroyImmediate(screenPrefab);
            Object.DestroyImmediate(popupPrefab);
            fixture.Destroy();
        }

        private GameObject CreateTestPrefab(string name, out WitUIView view)
        {
            var go = new GameObject(name);
            view = go.AddComponent<WitUIView>();
            go.SetActive(false);
            return go;
        }

        private sealed class RecordingInputCoordinator : IUIInputCoordinator
        {
            public int CallCount { get; private set; }
            public bool LastHasBlockingUI { get; private set; }
            public bool LastHasAnyUI { get; private set; }

            public void OnUIStateChanged(bool hasBlockingUI, bool hasAnyUI)
            {
                CallCount++;
                LastHasBlockingUI = hasBlockingUI;
                LastHasAnyUI = hasAnyUI;
            }
        }
    }
}
