using UnityEngine;
using UnityEngine.UI;
using Wit.Framework.UI;

namespace BF.Game.Runtime.UI.Test
{
    /// <summary>
    /// 测试用 HUD View，验证框架 HUD 层级不进入返回栈。
    /// 非正式玩法 UI，后续正式 HUD 应继承 WitUIView 并实现自身逻辑。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BFTestHudView : WitUIView
    {
        [SerializeField] private Text _labelText;

        protected override void OnOpened(object context)
        {
            if (context is string text && _labelText != null)
                _labelText.text = text;
        }
    }
}
