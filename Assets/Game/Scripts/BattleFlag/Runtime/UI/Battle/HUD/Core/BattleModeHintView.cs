using UnityEngine;
using UnityEngine.UI;

namespace BF.Game.Runtime.UI.Battle.HUD.Core
{
    /// <summary>
    /// BattleHUD 的特殊状态提示。
    /// 普通选中和空闲阶段不显示，仅在选择目标、敌方行动等特殊状态短文本提示。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleModeHintView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _rootCanvasGroup;
        [SerializeField] private Text _hintText;

        private void Awake()
        {
            Hide();
        }

        public void Show(string text)
        {
            if (_hintText != null)
                _hintText.text = text;
            SetVisible(true);
        }

        public void Hide()
        {
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (_rootCanvasGroup == null)
            {
                gameObject.SetActive(visible);
                return;
            }

            _rootCanvasGroup.alpha = visible ? 1f : 0f;
            _rootCanvasGroup.interactable = false;
            _rootCanvasGroup.blocksRaycasts = false;
            gameObject.SetActive(visible);
        }
    }
}
