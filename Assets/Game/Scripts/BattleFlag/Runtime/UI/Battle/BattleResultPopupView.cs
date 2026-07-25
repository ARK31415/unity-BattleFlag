using UnityEngine;
using UnityEngine.UI;
using Wit.Framework.UI;

namespace BF.Game.Runtime.UI.Battle
{
    /// <summary>
    /// 战斗结果弹窗 Window。由 WitUIManager 打开并使用 Popup/Modal 流程管理。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleResultPopupView : WitUIView<BattleResultContext>
    {
        [SerializeField] private Text _resultText;
        [SerializeField] private Button _closeButton;

        private BattleResultContext _context;

        protected override void OnOpened(BattleResultContext context)
        {
            Bind(context);
        }

        protected override void OnReopened(BattleResultContext context)
        {
            Bind(context);
        }

        protected override void OnClosing()
        {
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(OnCloseClicked);
        }

        private void Bind(BattleResultContext context)
        {
            _context = context;
            if (_resultText != null)
                _resultText.text = context.IsVictory ? "VICTORY" : "DEFEAT";

            if (_closeButton == null) return;

            _closeButton.onClick.RemoveListener(OnCloseClicked);
            _closeButton.onClick.AddListener(OnCloseClicked);
        }

        private void OnCloseClicked()
        {
            _context?.CloseRequested?.Invoke();
        }
    }
}
