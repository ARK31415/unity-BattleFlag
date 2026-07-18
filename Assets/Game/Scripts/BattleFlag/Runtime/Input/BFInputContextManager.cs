using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BF.Game.Runtime.Input
{
    /// <summary>
    /// BattleFlag 输入上下文管理器。
    ///
    /// 职责边界：
    /// - 持有项目输入资产，并统一启停基础上下文和叠加上下文对应的 Action Map。
    /// - 向业务脚本提供稳定的输入动作查询和订阅入口。
    /// - 不解释战斗命令、单位移动、镜头控制或 UI 业务。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BFInputContextManager : MonoBehaviour
    {
        private const string BattleMap = "Battle";
        private const string BattleCameraMap = "BattleCamera";
        private const string GlobalMap = "Global";
        private const string UIMap = "UI";

        [SerializeField] private InputActionAsset _inputActions;
        [SerializeField] private bool _initializeDefaultBattleContextsOnAwake;

        private readonly HashSet<BFInputOverlayContext> _enabledOverlays = new();

        public static BFInputContextManager Instance { get; private set; }
        public BFInputBaseContext CurrentBaseContext { get; private set; } = BFInputBaseContext.None;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[Input] Scene contains more than one BFInputContextManager.", this);
                enabled = false;
                return;
            }

            Instance = this;

            if (_initializeDefaultBattleContextsOnAwake)
                InitializeDefaultBattleContexts();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void InitializeDefaultBattleContexts()
        {
            SetBaseContext(BFInputBaseContext.Battle);
            EnableOverlay(BFInputOverlayContext.Global);
            EnableOverlay(BFInputOverlayContext.BattleCamera);
            EnableOverlay(BFInputOverlayContext.UI);
        }

        public void SetBaseContext(BFInputBaseContext context)
        {
            DisableMapForBaseContext(CurrentBaseContext);
            CurrentBaseContext = context;
            EnableMapForBaseContext(CurrentBaseContext);
        }

        public void EnableOverlay(BFInputOverlayContext context)
        {
            if (_enabledOverlays.Add(context))
                SetMapEnabled(GetMapName(context), true);
        }

        public void DisableOverlay(BFInputOverlayContext context)
        {
            if (_enabledOverlays.Remove(context))
                SetMapEnabled(GetMapName(context), false);
        }

        public bool IsOverlayEnabled(BFInputOverlayContext context)
        {
            return _enabledOverlays.Contains(context);
        }

        public bool TryGetAction(BFInputActionId id, out InputAction action)
        {
            action = null;
            if (_inputActions == null)
            {
                Debug.LogError("[Input] BFInputContextManager has no InputActionAsset assigned.", this);
                return false;
            }

            string actionPath = GetActionPath(id);
            action = _inputActions.FindAction(actionPath, false);
            if (action != null) return true;

            Debug.LogError($"[Input] Missing input action: {actionPath}", this);
            return false;
        }

        public bool TryRegisterPerformed(
            BFInputActionId id,
            Action<InputAction.CallbackContext> performed,
            out BFInputActionSubscription subscription)
        {
            subscription = null;
            if (performed == null) return false;
            if (!TryGetAction(id, out InputAction action)) return false;

            action.performed += performed;
            subscription = new BFInputActionSubscription(action, performed, null);
            return true;
        }

        private void EnableMapForBaseContext(BFInputBaseContext context)
        {
            if (context == BFInputBaseContext.Battle)
                SetMapEnabled(BattleMap, true);
        }

        private void DisableMapForBaseContext(BFInputBaseContext context)
        {
            if (context == BFInputBaseContext.Battle)
                SetMapEnabled(BattleMap, false);
        }

        private void SetMapEnabled(string mapName, bool enabledState)
        {
            if (string.IsNullOrEmpty(mapName) || _inputActions == null) return;

            InputActionMap map = _inputActions.FindActionMap(mapName, false);
            if (map == null)
            {
                Debug.LogError($"[Input] Missing input action map: {mapName}", this);
                return;
            }

            if (enabledState) map.Enable();
            else map.Disable();
        }

        private static string GetMapName(BFInputOverlayContext context)
        {
            return context switch
            {
                BFInputOverlayContext.Global => GlobalMap,
                BFInputOverlayContext.BattleCamera => BattleCameraMap,
                BFInputOverlayContext.UI => UIMap,
                BFInputOverlayContext.Debug => string.Empty,
                _ => string.Empty
            };
        }

        private static string GetActionPath(BFInputActionId id)
        {
            return id switch
            {
                BFInputActionId.BattlePoint => "Battle/Point",
                BFInputActionId.BattleSelect => "Battle/Select",
                BFInputActionId.BattleCancel => "Battle/Cancel",
                BFInputActionId.BattleEndTurn => "Battle/EndTurn",
                BFInputActionId.BattleCameraMove => "BattleCamera/Move",
                BFInputActionId.BattleCameraZoom => "BattleCamera/Zoom",
                BFInputActionId.GlobalPause => "Global/Pause",
                _ => string.Empty
            };
        }
    }
}
