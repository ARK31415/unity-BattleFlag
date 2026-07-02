using BF.Framework.UI.Runtime.Pages;
using BF.Framework.UI.Runtime.Windows;
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

        public BFUIManager(BFUIRegistry registry)
            : this(registry, null, null)
        {
        }

        public BFUIManager(BFUIRegistry registry, BFUIRoot uiRoot, BFUILoader loader = null)
        {
            _loader = loader ?? new BFUILoader(registry);
            _uiRoot = uiRoot;
        }

        public GameObject OpenPage(string panelId)
        {
            return OpenPanel<BFPage>(panelId, _uiRoot != null ? _uiRoot.PageLayer : null);
        }

        public GameObject OpenWindow(string panelId)
        {
            return OpenPanel<BFWindow>(panelId, _uiRoot != null ? _uiRoot.WindowLayer : null);
        }

        private GameObject OpenPanel<TPanel>(string panelId, Transform parent)
            where TPanel : MonoBehaviour
        {
            GameObject instance = _loader.Load(panelId, parent);
            if (instance == null)
            {
                return null;
            }

            instance.GetComponent<TPanel>();
            return instance;
        }
    }
}
