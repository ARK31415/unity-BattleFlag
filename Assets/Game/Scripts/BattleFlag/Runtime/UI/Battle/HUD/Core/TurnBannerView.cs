using BF.Game.Runtime.Battle.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace BF.Game.Runtime.UI.Battle.HUD.Core
{
    /// <summary>
    /// BattleHUD 顶部回合信息条。
    /// 该组件按多个文本区域显示中文回合数、阶段和行动方，不承载任何按钮交互。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TurnBannerView : MonoBehaviour
    {
        [SerializeField] private Text _turnNumberText;
        [SerializeField] private Text _phaseText;
        [SerializeField] private Text _actorSideText;

        public void Refresh(int turnNumber, BattlePhase phase)
        {
            if (_turnNumberText != null)
                _turnNumberText.text = $"第 {Mathf.Max(1, turnNumber)} 回合";

            if (_phaseText != null)
                _phaseText.text = FormatPhase(phase);

            if (_actorSideText != null)
                _actorSideText.text = FormatActorSide(phase);
        }

        private static string FormatPhase(BattlePhase phase)
        {
            return phase switch
            {
                BattlePhase.PlayerTurn => "玩家回合",
                BattlePhase.EnemyTurn => "敌方回合",
                BattlePhase.Resolution => "战斗结算",
                BattlePhase.Init => "战斗准备",
                _ => "等待中"
            };
        }

        private static string FormatActorSide(BattlePhase phase)
        {
            return phase switch
            {
                BattlePhase.PlayerTurn => "我方",
                BattlePhase.EnemyTurn => "敌方",
                BattlePhase.Resolution => "结算",
                _ => "无"
            };
        }
    }
}
