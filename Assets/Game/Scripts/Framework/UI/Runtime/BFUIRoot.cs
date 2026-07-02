using UnityEngine;

namespace BF.Framework.UI.Runtime
{
    /// <summary>
    /// UI 分层根节点，承载页面、窗口、小组件与提示层级。
    /// </summary>
    public class BFUIRoot : MonoBehaviour
    {
        public Transform PageLayer;
        public Transform WindowLayer;
        public Transform WidgetLayer;
        public Transform ToastLayer;
    }
}
