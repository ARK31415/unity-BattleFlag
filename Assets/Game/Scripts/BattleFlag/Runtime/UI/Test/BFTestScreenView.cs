using UnityEngine;
using UnityEngine.UI;
using Wit.Framework.UI;

namespace BF.Game.Runtime.UI.Test
{
    /// <summary>
    /// 测试用 Screen View，验证框架 Screen 层级和栈行为。
    /// 非正式玩法 UI，后续正式 Screen 应继承 WitUIView 并实现自身逻辑。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BFTestScreenView : WitUIView
    {
        [SerializeField] private Text _titleText;

        /// <summary>最近一次设置的标题文本，用于测试验证。</summary>
        public string LastTitle { get; private set; }

        /// <summary>设置标题文本。</summary>
        public void SetTitle(string title)
        {
            LastTitle = title;
            if (_titleText != null)
                _titleText.text = title;
        }
    }
}
