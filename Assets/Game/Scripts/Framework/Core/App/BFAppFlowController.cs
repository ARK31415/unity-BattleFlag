using System;
using UnityEngine;

namespace BF.Framework.Core.App
{
    /// <summary>
    /// 应用主流程状态机。
    /// 管理 Boot → MainMenu → LevelSelect → LoadingBattle → InBattle 的状态流转。
    /// </summary>
    public class BFAppFlowController
    {
        public BFAppFlowState CurrentState { get; private set; } = BFAppFlowState.None;

        /// <summary>
        /// 状态变化事件。参数为 (旧状态, 新状态)。
        /// </summary>
        public event Action<BFAppFlowState, BFAppFlowState> OnStateChanged;

        public void EnterBoot()
        {
            TransitionTo(BFAppFlowState.Boot);
        }

        public void EnterMainMenu()
        {
            if (CurrentState != BFAppFlowState.Boot && CurrentState != BFAppFlowState.InBattle)
            {
                Debug.LogWarning($"[BFAppFlow] 无法从 {CurrentState} 进入 MainMenu。");
                return;
            }
            TransitionTo(BFAppFlowState.MainMenu);
        }

        public void EnterLevelSelect()
        {
            if (CurrentState != BFAppFlowState.MainMenu)
            {
                Debug.LogWarning($"[BFAppFlow] 无法从 {CurrentState} 进入 LevelSelect。");
                return;
            }
            TransitionTo(BFAppFlowState.LevelSelect);
        }

        public void EnterLoadingBattle()
        {
            if (CurrentState != BFAppFlowState.MainMenu &&
                CurrentState != BFAppFlowState.LevelSelect)
            {
                Debug.LogWarning($"[BFAppFlow] 无法从 {CurrentState} 进入 LoadingBattle。");
                return;
            }
            TransitionTo(BFAppFlowState.LoadingBattle);
        }

        public void EnterInBattle()
        {
            if (CurrentState != BFAppFlowState.LoadingBattle)
            {
                Debug.LogWarning($"[BFAppFlow] 无法从 {CurrentState} 进入 InBattle。");
                return;
            }
            TransitionTo(BFAppFlowState.InBattle);
        }

        public void ReturnToMainMenu()
        {
            if (CurrentState != BFAppFlowState.InBattle &&
                CurrentState != BFAppFlowState.BattleSettlement &&
                CurrentState != BFAppFlowState.SystemValidation)
            {
                Debug.LogWarning($"[BFAppFlow] 无法从 {CurrentState} 返回 MainMenu。");
                return;
            }
            TransitionTo(BFAppFlowState.MainMenu);
        }

        /// <summary>
        /// 进入系统验证关（P2）。
        /// </summary>
        public void EnterSystemValidation()
        {
            if (CurrentState != BFAppFlowState.MainMenu)
            {
                Debug.LogWarning($"[BFAppFlow] 无法从 {CurrentState} 进入 SystemValidation。");
                return;
            }
            TransitionTo(BFAppFlowState.SystemValidation);
        }

        /// <summary>
        /// 进入战斗结算（P2）。
        /// </summary>
        public void EnterBattleSettlement()
        {
            if (CurrentState != BFAppFlowState.InBattle)
            {
                Debug.LogWarning($"[BFAppFlow] 无法从 {CurrentState} 进入 BattleSettlement。");
                return;
            }
            TransitionTo(BFAppFlowState.BattleSettlement);
        }

        private void TransitionTo(BFAppFlowState newState)
        {
            if (CurrentState == newState) return;

            BFAppFlowState oldState = CurrentState;
            CurrentState = newState;
            Debug.Log($"[BFAppFlow] 状态转换: {oldState} → {newState}");
            OnStateChanged?.Invoke(oldState, newState);
        }
    }
}
