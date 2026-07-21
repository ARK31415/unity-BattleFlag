using UnityEngine;
using Wit.Framework.UI;

namespace BF.Game.Tests.EditMode.UI
{
    /// <summary>
    /// 为 UIManager EditMode 测试提供预构建的运行时环境。
    /// </summary>
    public sealed class WitUITestFixture
    {
        public GameObject RootGo { get; }
        public WitUIRoot Root { get; }
        public WitUIConfig Config { get; }
        public WitUIManager Manager { get; }
        public Transform HUD { get; }
        public Transform Screen { get; }
        public Transform Popup { get; }
        public Transform Overlay { get; }
        public Transform Toast { get; }
        public CanvasGroup ModalBlocker { get; }

        private WitUITestFixture()
        {
            RootGo = new GameObject("UIRoot");
            HUD = new GameObject("HUDLayer").transform;
            Screen = new GameObject("ScreenLayer").transform;
            Popup = new GameObject("PopupLayer").transform;
            Overlay = new GameObject("OverlayLayer").transform;
            Toast = new GameObject("ToastLayer").transform;
            HUD.SetParent(RootGo.transform, false);
            Screen.SetParent(RootGo.transform, false);
            Popup.SetParent(RootGo.transform, false);
            Overlay.SetParent(RootGo.transform, false);
            Toast.SetParent(RootGo.transform, false);

            var blockerGo = new GameObject("ModalBlocker");
            blockerGo.transform.SetParent(RootGo.transform, false);
            ModalBlocker = blockerGo.AddComponent<CanvasGroup>();
            ModalBlocker.alpha = 0f;
            ModalBlocker.interactable = false;
            ModalBlocker.blocksRaycasts = false;

            Root = RootGo.AddComponent<WitUIRoot>();
            Root.SetTestLayerRoots(HUD, Screen, Popup, Overlay, Toast, ModalBlocker);

            Config = ScriptableObject.CreateInstance<WitUIConfig>();

            Manager = RootGo.AddComponent<WitUIManager>();
        }

        public static WitUITestFixture Create()
        {
            var fixture = new WitUITestFixture();
            fixture.Manager.Configure(fixture.Config, fixture.Root);
            return fixture;
        }

        public void RegisterWindow(string key, GameObject prefab, WitUILayer layer,
            WitUICachePolicy cachePolicy = WitUICachePolicy.DestroyOnClose,
            bool unique = true, bool modal = false)
        {
            Config.AddTestDefinition(
                new WitUIWindowDefinition(key, prefab, layer, cachePolicy, unique, modal));
        }

        public void Destroy()
        {
            if (Config != null) Object.DestroyImmediate(Config);
            if (RootGo != null) Object.DestroyImmediate(RootGo);
        }
    }
}
