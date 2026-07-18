using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Wit.Framework.Input
{
    [DisallowMultipleComponent]
    public sealed class WitInputContextManager : MonoBehaviour
    {
        [SerializeField] private WitInputContextConfig _config;
        [SerializeField] private string _startupProfileKey;
        [SerializeField] private bool _applyStartupProfileOnAwake;

        private readonly HashSet<string> _enabledOverlayKeys = new(StringComparer.Ordinal);

        public static WitInputContextManager Instance { get; private set; }
        public string CurrentBaseContextKey { get; private set; } = string.Empty;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[Input] Scene contains more than one WitInputContextManager.", this);
                enabled = false;
                return;
            }

            Instance = this;

            if (_applyStartupProfileOnAwake && !string.IsNullOrEmpty(_startupProfileKey))
                ApplyStartupProfile(_startupProfileKey);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public bool ApplyStartupProfile(string key)
        {
            if (!TryGetConfig(out WitInputContextConfig config)) return false;
            if (!config.TryGetStartupProfile(key, out WitInputStartupProfile profile))
            {
                Debug.LogError($"[Input] Missing startup profile: {key}", this);
                return false;
            }

            string[] enabledOverlays = new string[_enabledOverlayKeys.Count];
            _enabledOverlayKeys.CopyTo(enabledOverlays);
            for (int i = 0; i < enabledOverlays.Length; i++)
                DisableOverlay(enabledOverlays[i]);

            if (!SetBaseContext(profile.BaseContextKey))
                return false;

            bool success = true;
            IReadOnlyList<string> overlays = profile.OverlayContextKeys;
            for (int i = 0; i < overlays.Count; i++)
                success &= EnableOverlay(overlays[i]);

            return success;
        }

        public bool SetBaseContext(string key)
        {
            if (!string.IsNullOrEmpty(CurrentBaseContextKey))
            {
                if (TryGetConfig(out WitInputContextConfig oldConfig)
                    && oldConfig.TryGetBaseContext(CurrentBaseContextKey, out WitInputContextDefinition oldContext))
                {
                    SetMapEnabled(oldContext.ActionMap, false);
                }
            }

            CurrentBaseContextKey = string.Empty;

            if (string.IsNullOrEmpty(key))
                return true;

            if (!TryGetConfig(out WitInputContextConfig config)) return false;
            if (!config.TryGetBaseContext(key, out WitInputContextDefinition context))
            {
                Debug.LogError($"[Input] Missing base context: {key}", this);
                return false;
            }

            if (!SetMapEnabled(context.ActionMap, true))
                return false;

            CurrentBaseContextKey = key;
            return true;
        }

        public bool EnableOverlay(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (_enabledOverlayKeys.Contains(key)) return true;
            if (!TryGetConfig(out WitInputContextConfig config)) return false;
            if (!config.TryGetOverlayContext(key, out WitInputContextDefinition context))
            {
                Debug.LogError($"[Input] Missing overlay context: {key}", this);
                return false;
            }

            if (!SetMapEnabled(context.ActionMap, true))
                return false;

            _enabledOverlayKeys.Add(key);
            return true;
        }

        public bool DisableOverlay(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (!_enabledOverlayKeys.Remove(key)) return true;
            if (!TryGetConfig(out WitInputContextConfig config)) return false;
            if (!config.TryGetOverlayContext(key, out WitInputContextDefinition context))
            {
                Debug.LogError($"[Input] Missing overlay context: {key}", this);
                return false;
            }

            return SetMapEnabled(context.ActionMap, false);
        }

        public bool IsOverlayEnabled(string key)
        {
            return _enabledOverlayKeys.Contains(key);
        }

        public bool TryGetAction(string key, out InputAction action)
        {
            action = null;
            if (!TryGetConfig(out WitInputContextConfig config)) return false;
            if (config.InputActions == null)
            {
                Debug.LogError("[Input] WitInputContextConfig has no InputActionAsset assigned.", this);
                return false;
            }

            if (!config.TryGetAction(key, out WitInputActionDefinition actionDefinition))
            {
                Debug.LogError($"[Input] Missing action definition: {key}", this);
                return false;
            }

            InputActionMap map = config.InputActions.FindActionMap(actionDefinition.ActionMap, false);
            if (map == null)
            {
                Debug.LogError($"[Input] Missing input action map: {actionDefinition.ActionMap}", this);
                return false;
            }

            action = map.FindAction(actionDefinition.Action, false);
            if (action != null) return true;

            Debug.LogError($"[Input] Missing input action: {actionDefinition.ActionMap}/{actionDefinition.Action}", this);
            return false;
        }

        public bool TryRegisterPerformed(
            string key,
            Action<InputAction.CallbackContext> performed,
            out WitInputActionSubscription subscription)
        {
            subscription = null;
            if (performed == null) return false;
            if (!TryGetAction(key, out InputAction action)) return false;

            action.performed += performed;
            subscription = new WitInputActionSubscription(action, performed, null);
            return true;
        }

        private bool TryGetConfig(out WitInputContextConfig config)
        {
            config = _config;
            if (config != null) return true;

            Debug.LogError("[Input] WitInputContextManager has no WitInputContextConfig assigned.", this);
            return false;
        }

        private bool SetMapEnabled(string mapName, bool enabledState)
        {
            if (!TryGetConfig(out WitInputContextConfig config)) return false;
            if (config.InputActions == null)
            {
                Debug.LogError("[Input] WitInputContextConfig has no InputActionAsset assigned.", this);
                return false;
            }

            InputActionMap map = config.InputActions.FindActionMap(mapName, false);
            if (map == null)
            {
                Debug.LogError($"[Input] Missing input action map: {mapName}", this);
                return false;
            }

            if (enabledState) map.Enable();
            else map.Disable();
            return true;
        }
    }
}
