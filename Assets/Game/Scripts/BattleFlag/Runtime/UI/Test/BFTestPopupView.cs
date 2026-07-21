using UnityEngine;
using UnityEngine.UI;
using Wit.Framework.UI;

namespace BF.Game.Runtime.UI.Test
{
    /// <summary>
    /// 测试用 Popup View，验证框架 Popup 层级和模态行为。
    /// 非正式玩法 UI，后续正式 Popup 应继承 WitUIView 并实现自身逻辑。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BFTestPopupView : WitUIView
    {
        [SerializeField] private Text _bodyText;

        protected override void OnOpened(object context)
        {
            if (context is string text && _bodyText != null)
                _bodyText.text = text;
        }
    }
}
