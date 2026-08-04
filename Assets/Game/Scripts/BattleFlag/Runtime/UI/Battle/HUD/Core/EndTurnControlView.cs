using System;
using UnityEngine;
using UnityEngine.UI;

namespace BF.Game.Runtime.UI.Battle.HUD.Core
{
    /// <summary>
    /// BattleHUD 右下角回合级结束回合按钮。
    /// 它只发起回合级 EndTurn 请求，不处理单位级等待命令。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EndTurnControlView : MonoBehaviour
    {
        [SerializeField] private Button _endTurnButton;
        [SerializeField] private GameObject _highlightFrame;
        [SerializeField] private Image _background;
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _disabledColor = new(0.45f, 0.45f, 0.45f, 1f);
        [SerializeField] private Color _highlightColor = new(1f, 0.82f, 0.18f, 1f);

        public event Action Clicked;

        private void Awake()
        {
            ResolveReferences();
            if (_endTurnButton != null) _endTurnButton.onClick.AddListener(HandleClicked);
        }

        private void OnDestroy()
        {
            if (_endTurnButton != null) _endTurnButton.onClick.RemoveListener(HandleClicked);
        }

        public void SetState(bool interactable, bool highlighted)
        {
            ResolveReferences();
            if (_endTurnButton != null)
                _endTurnButton.interactable = interactable;

            if (_highlightFrame != null)
                _highlightFrame.SetActive(highlighted);

            if (_background != null)
                _background.color = !interactable ? _disabledColor : highlighted ? _highlightColor : _normalColor;
        }

        private void HandleClicked()
        {
            Clicked?.Invoke();
        }

        private void ResolveReferences()
        {
            if (_endTurnButton == null) _endTurnButton = GetComponentInChildren<Button>(true);
            if (_background == null && _endTurnButton != null) _background = _endTurnButton.GetComponent<Image>();
        }
    }
}
