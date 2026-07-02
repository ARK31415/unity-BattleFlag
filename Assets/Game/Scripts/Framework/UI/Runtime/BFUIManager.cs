using UnityEngine;

namespace BF.Framework.UI.Runtime
{
    /// <summary>
    /// 负责驱动最小 UI 打开流程。
    /// </summary>
    public class BFUIManager
    {
        private readonly BFUILoader _loader;
        private readonly BFUIRoot _uiRoot;

        public BFUIManager(BFUIRegistry registry, BFUIRoot uiRoot = null)
        {
            _loader = new BFUILoader(registry);
            _uiRoot = uiRoot;
        }

        public GameObject OpenPage(string panelId)
            => OpenPanel(panelId, _uiRoot != null ? _uiRoot.PageLayer : null);

        public GameObject OpenWindow(string panelId)
            => OpenPanel(panelId, _uiRoot != null ? _uiRoot.WindowLayer : null);

        public GameObject OpenWidget(string panelId)
            => OpenPanel(panelId, _uiRoot != null ? _uiRoot.WidgetLayer : null);

        public GameObject OpenToast(string panelId)
            => OpenPanel(panelId, _uiRoot != null ? _uiRoot.ToastLayer : null);

        private GameObject OpenPanel(string panelId, Transform parent)
            => _loader.Load(panelId, parent);
    }
}
