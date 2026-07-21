using UnityEngine;

namespace Wit.Framework.UI
{
    /// <summary>
    /// 提供 UGUI Canvas 层级节点容器和 ModalBlocker 引用。
    /// UIManager 通过 WitUIRoot 将窗口挂载到正确的层级 Transform 下。
    /// </summary>
    public sealed class WitUIRoot : MonoBehaviour
    {
        [Header("Layers")]
        [SerializeField] private Transform _hudLayer;
        [SerializeField] private Transform _screenLayer;
        [SerializeField] private Transform _popupLayer;
        [SerializeField] private Transform _overlayLayer;
        [SerializeField] private Transform _toastLayer;

        [Header("Modal")]
        [SerializeField] private CanvasGroup _modalBlocker;

        public Transform HUDLayer => _hudLayer;
        public Transform ScreenLayer => _screenLayer;
        public Transform PopupLayer => _popupLayer;
        public Transform OverlayLayer => _overlayLayer;
        public Transform ToastLayer => _toastLayer;
        public CanvasGroup ModalBlocker => _modalBlocker;

        /// <summary>
        /// 根据层级返回对应 Transform 容器。
        /// </summary>
        public Transform GetLayerRoot(WitUILayer layer)
        {
            return layer switch
            {
                WitUILayer.HUD => HUDLayer,
                WitUILayer.Screen => ScreenLayer,
                WitUILayer.Popup => PopupLayer,
                WitUILayer.Overlay => OverlayLayer,
                WitUILayer.Toast => ToastLayer,
                _ => null
            };
        }

#if UNITY_EDITOR
        /// <summary>
        /// 仅用于测试时设置层级根节点。
        /// </summary>
        public void SetTestLayerRoots(Transform hud, Transform screen, Transform popup, Transform overlay, Transform toast, CanvasGroup modal)
        {
            _hudLayer = hud;
            _screenLayer = screen;
            _popupLayer = popup;
            _overlayLayer = overlay;
            _toastLayer = toast;
            _modalBlocker = modal;
        }
#endif
    }
}
