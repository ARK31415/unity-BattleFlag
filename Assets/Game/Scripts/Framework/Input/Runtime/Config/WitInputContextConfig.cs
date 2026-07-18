using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Wit.Framework.Input
{
    [CreateAssetMenu(
        fileName = "WitInputContextConfig",
        menuName = "Wit/Framework/Input/Input Context Config")]
    public sealed class WitInputContextConfig : ScriptableObject
    {
        [Header("Input Actions")]
        [Tooltip(".inputactions is the only source for concrete bindings. This config stores semantic mappings only.")]
        [SerializeField] private InputActionAsset _inputActions;

        [Header("Base Contexts")]
        [Tooltip("Base contexts are mutually exclusive. Only one base context should be active at a time.")]
        [SerializeField] private List<WitInputContextDefinition> _baseContexts = new();

        [Header("Overlay Contexts")]
        [Tooltip("Overlay contexts can be combined on top of the current base context.")]
        [SerializeField] private List<WitInputContextDefinition> _overlayContexts = new();

        [Header("Actions")]
        [Tooltip("Maps project semantic action keys to Action Map / Action names in the InputActionAsset.")]
        [SerializeField] private List<WitInputActionDefinition> _actions = new();

        [Header("Startup Profiles")]
        [Tooltip("Startup profiles are scene or entry presets. They are not bindings and not a state machine.")]
        [SerializeField] private List<WitInputStartupProfile> _startupProfiles = new();

        public InputActionAsset InputActions => _inputActions;

        public bool TryGetBaseContext(string key, out WitInputContextDefinition definition)
        {
            return TryFindByKey(_baseContexts, key, out definition);
        }

        public bool TryGetOverlayContext(string key, out WitInputContextDefinition definition)
        {
            return TryFindByKey(_overlayContexts, key, out definition);
        }

        public bool TryGetAction(string key, out WitInputActionDefinition definition)
        {
            return TryFindByKey(_actions, key, out definition);
        }

        public bool TryGetStartupProfile(string key, out WitInputStartupProfile profile)
        {
            return TryFindByKey(_startupProfiles, key, out profile);
        }

        private static bool TryFindByKey<T>(IReadOnlyList<T> definitions, string key, out T match)
            where T : IWitInputKeyedDefinition
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                T definition = definitions[i];
                if (definition != null && string.Equals(definition.Key, key, StringComparison.Ordinal))
                {
                    match = definition;
                    return true;
                }
            }

            match = default;
            return false;
        }
    }

    public interface IWitInputKeyedDefinition
    {
        string Key { get; }
    }

    [Serializable]
    public sealed class WitInputContextDefinition : IWitInputKeyedDefinition
    {
        [SerializeField] private string _key;
        [SerializeField] private string _actionMap;

        public string Key => _key;
        public string ActionMap => _actionMap;
    }

    [Serializable]
    public sealed class WitInputActionDefinition : IWitInputKeyedDefinition
    {
        [SerializeField] private string _key;
        [SerializeField] private string _actionMap;
        [SerializeField] private string _action;

        public string Key => _key;
        public string ActionMap => _actionMap;
        public string Action => _action;
    }

    [Serializable]
    public sealed class WitInputStartupProfile : IWitInputKeyedDefinition
    {
        [SerializeField] private string _key;
        [SerializeField] private string _baseContextKey;
        [SerializeField] private List<string> _overlayContextKeys = new();

        public string Key => _key;
        public string BaseContextKey => _baseContextKey;
        public IReadOnlyList<string> OverlayContextKeys => _overlayContextKeys;
    }
}
