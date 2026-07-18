using System.Collections.Generic;
using System.Reflection;
using BF.Game.Runtime.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Wit.Framework.Input;

namespace BF.Game.Tests.EditMode.Input
{
    public class WitInputContextManagerTests
    {
        private GameObject _owner;
        private WitInputContextManager _manager;
        private WitInputContextConfig _config;
        private InputActionAsset _inputActions;

        [SetUp]
        public void SetUp()
        {
            _inputActions = CreateInputActions();
            _config = CreateConfig(_inputActions);
            _owner = new GameObject("InputContextManager");
            _manager = _owner.AddComponent<WitInputContextManager>();
            SetPrivateField(_manager, "_config", _config);
        }

        [TearDown]
        public void TearDown()
        {
            if (_owner != null)
                Object.DestroyImmediate(_owner);

            if (_config != null)
                Object.DestroyImmediate(_config);

            if (_inputActions != null)
                Object.DestroyImmediate(_inputActions);
        }

        [Test]
        public void ApplyStartupProfile_EnablesBattleGlobalBattleCameraAndUI()
        {
            bool applied = _manager.ApplyStartupProfile(BFBattleFlagInputKeys.BattleTestProfile);

            Assert.That(applied, Is.True);
            Assert.That(_manager.CurrentBaseContextKey, Is.EqualTo(BFBattleFlagInputKeys.BattleContext));
            Assert.That(_manager.IsOverlayEnabled(BFBattleFlagInputKeys.GlobalOverlay), Is.True);
            Assert.That(_manager.IsOverlayEnabled(BFBattleFlagInputKeys.BattleCameraOverlay), Is.True);
            Assert.That(_manager.IsOverlayEnabled(BFBattleFlagInputKeys.UIOverlay), Is.True);
            Assert.That(_manager.TryGetAction(BFBattleFlagInputKeys.BattleSelect, out InputAction action), Is.True);
            Assert.That(action.enabled, Is.True);
        }

        [Test]
        public void SetBaseContextEmpty_DisablesBattleActionsButKeepsOverlayActions()
        {
            _manager.ApplyStartupProfile(BFBattleFlagInputKeys.BattleTestProfile);

            bool changed = _manager.SetBaseContext(string.Empty);

            Assert.That(changed, Is.True);
            Assert.That(_manager.TryGetAction(BFBattleFlagInputKeys.BattleSelect, out InputAction battleSelect), Is.True);
            Assert.That(_manager.TryGetAction(BFBattleFlagInputKeys.BattleCameraMove, out InputAction cameraMove), Is.True);
            Assert.That(battleSelect.enabled, Is.False);
            Assert.That(cameraMove.enabled, Is.True);
        }

        [Test]
        public void DisableOverlay_DisablesMappedActionMap()
        {
            _manager.ApplyStartupProfile(BFBattleFlagInputKeys.BattleTestProfile);

            bool disabled = _manager.DisableOverlay(BFBattleFlagInputKeys.BattleCameraOverlay);

            Assert.That(disabled, Is.True);
            Assert.That(_manager.IsOverlayEnabled(BFBattleFlagInputKeys.BattleCameraOverlay), Is.False);
            Assert.That(_manager.TryGetAction(BFBattleFlagInputKeys.BattleCameraMove, out InputAction cameraMove), Is.True);
            Assert.That(cameraMove.enabled, Is.False);
        }

        [Test]
        public void TryRegisterPerformed_ReturnsDisposableSubscription()
        {
            _manager.ApplyStartupProfile(BFBattleFlagInputKeys.BattleTestProfile);

            bool registered = _manager.TryRegisterPerformed(
                BFBattleFlagInputKeys.BattleSelect,
                _ => { },
                out WitInputActionSubscription subscription);

            Assert.That(registered, Is.True);
            Assert.That(subscription, Is.Not.Null);
            Assert.DoesNotThrow(() => subscription.Dispose());
            Assert.DoesNotThrow(() => subscription.Dispose());
        }

        [Test]
        public void TryGetAction_MissingActionDefinition_ReturnsFalse()
        {
            LogAssert.Expect(LogType.Error, "[Input] Missing action definition: missing.action");

            bool found = _manager.TryGetAction("missing.action", out InputAction action);

            Assert.That(found, Is.False);
            Assert.That(action, Is.Null);
        }

        [Test]
        public void ApplyStartupProfile_MissingProfile_ReturnsFalse()
        {
            LogAssert.Expect(LogType.Error, "[Input] Missing startup profile: missing.profile");

            bool applied = _manager.ApplyStartupProfile("missing.profile");

            Assert.That(applied, Is.False);
        }

        private static WitInputContextConfig CreateConfig(InputActionAsset asset)
        {
            WitInputContextConfig config = ScriptableObject.CreateInstance<WitInputContextConfig>();
            SetPrivateField(config, "_inputActions", asset);
            SetPrivateField(config, "_baseContexts", new List<WitInputContextDefinition>
            {
                CreateContext(BFBattleFlagInputKeys.BattleContext, "Battle")
            });
            SetPrivateField(config, "_overlayContexts", new List<WitInputContextDefinition>
            {
                CreateContext(BFBattleFlagInputKeys.GlobalOverlay, "Global"),
                CreateContext(BFBattleFlagInputKeys.BattleCameraOverlay, "BattleCamera"),
                CreateContext(BFBattleFlagInputKeys.UIOverlay, "UI")
            });
            SetPrivateField(config, "_actions", new List<WitInputActionDefinition>
            {
                CreateAction(BFBattleFlagInputKeys.BattlePoint, "Battle", "Point"),
                CreateAction(BFBattleFlagInputKeys.BattleSelect, "Battle", "Select"),
                CreateAction(BFBattleFlagInputKeys.BattleCancel, "Battle", "Cancel"),
                CreateAction(BFBattleFlagInputKeys.BattleEndTurn, "Battle", "EndTurn"),
                CreateAction(BFBattleFlagInputKeys.BattleCameraMove, "BattleCamera", "Move"),
                CreateAction(BFBattleFlagInputKeys.BattleCameraZoom, "BattleCamera", "Zoom"),
                CreateAction(BFBattleFlagInputKeys.GlobalPause, "Global", "Pause")
            });
            SetPrivateField(config, "_startupProfiles", new List<WitInputStartupProfile>
            {
                CreateProfile(
                    BFBattleFlagInputKeys.BattleTestProfile,
                    BFBattleFlagInputKeys.BattleContext,
                    BFBattleFlagInputKeys.GlobalOverlay,
                    BFBattleFlagInputKeys.BattleCameraOverlay,
                    BFBattleFlagInputKeys.UIOverlay)
            });
            return config;
        }

        private static WitInputContextDefinition CreateContext(string key, string actionMap)
        {
            WitInputContextDefinition context = new();
            SetPrivateField(context, "_key", key);
            SetPrivateField(context, "_actionMap", actionMap);
            return context;
        }

        private static WitInputActionDefinition CreateAction(string key, string actionMap, string action)
        {
            WitInputActionDefinition definition = new();
            SetPrivateField(definition, "_key", key);
            SetPrivateField(definition, "_actionMap", actionMap);
            SetPrivateField(definition, "_action", action);
            return definition;
        }

        private static WitInputStartupProfile CreateProfile(
            string key,
            string baseContextKey,
            params string[] overlayContextKeys)
        {
            WitInputStartupProfile profile = new();
            SetPrivateField(profile, "_key", key);
            SetPrivateField(profile, "_baseContextKey", baseContextKey);
            SetPrivateField(profile, "_overlayContextKeys", new List<string>(overlayContextKeys));
            return profile;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }

        private static InputActionAsset CreateInputActions()
        {
            InputActionAsset asset = ScriptableObject.CreateInstance<InputActionAsset>();

            InputActionMap battle = new("Battle");
            battle.AddAction("Point", InputActionType.Value, "<Mouse>/position");
            battle.AddAction("Select", InputActionType.Button, "<Mouse>/leftButton");
            battle.AddAction("Cancel", InputActionType.Button, "<Mouse>/rightButton");
            battle.AddAction("EndTurn", InputActionType.Button, "<Keyboard>/space");
            asset.AddActionMap(battle);

            InputActionMap camera = new("BattleCamera");
            camera.AddAction("Move", InputActionType.Value);
            camera.AddAction("Zoom", InputActionType.Value, "<Mouse>/scroll");
            asset.AddActionMap(camera);

            InputActionMap global = new("Global");
            global.AddAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            asset.AddActionMap(global);

            InputActionMap ui = new("UI");
            ui.AddAction("Navigate", InputActionType.PassThrough);
            ui.AddAction("Submit", InputActionType.Button);
            ui.AddAction("Cancel", InputActionType.Button);
            ui.AddAction("Point", InputActionType.PassThrough);
            ui.AddAction("Click", InputActionType.PassThrough);
            ui.AddAction("RightClick", InputActionType.PassThrough);
            ui.AddAction("MiddleClick", InputActionType.PassThrough);
            ui.AddAction("ScrollWheel", InputActionType.PassThrough);
            ui.AddAction("TrackedDevicePosition", InputActionType.PassThrough);
            ui.AddAction("TrackedDeviceOrientation", InputActionType.PassThrough);
            asset.AddActionMap(ui);

            return asset;
        }
    }
}
