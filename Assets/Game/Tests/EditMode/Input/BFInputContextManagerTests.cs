using System.Reflection;
using BF.Game.Runtime.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BF.Game.Tests.EditMode.Input
{
    public class BFInputContextManagerTests
    {
        private GameObject _owner;
        private BFInputContextManager _manager;

        [SetUp]
        public void SetUp()
        {
            _owner = new GameObject("InputContextManager");
            _manager = _owner.AddComponent<BFInputContextManager>();
            SetInputActions(_manager, CreateInputActions());
        }

        [TearDown]
        public void TearDown()
        {
            if (_owner != null)
                Object.DestroyImmediate(_owner);
        }

        [Test]
        public void InitializeDefaultBattleContexts_EnablesBattleGlobalBattleCameraAndUI()
        {
            _manager.InitializeDefaultBattleContexts();

            Assert.That(_manager.CurrentBaseContext, Is.EqualTo(BFInputBaseContext.Battle));
            Assert.That(_manager.IsOverlayEnabled(BFInputOverlayContext.Global), Is.True);
            Assert.That(_manager.IsOverlayEnabled(BFInputOverlayContext.BattleCamera), Is.True);
            Assert.That(_manager.IsOverlayEnabled(BFInputOverlayContext.UI), Is.True);
            Assert.That(_manager.TryGetAction(BFInputActionId.BattleSelect, out InputAction action), Is.True);
            Assert.That(action.enabled, Is.True);
        }

        [Test]
        public void SetBaseContextNone_DisablesBattleActionsButKeepsOverlayActions()
        {
            _manager.InitializeDefaultBattleContexts();

            _manager.SetBaseContext(BFInputBaseContext.None);

            Assert.That(_manager.TryGetAction(BFInputActionId.BattleSelect, out InputAction battleSelect), Is.True);
            Assert.That(_manager.TryGetAction(BFInputActionId.BattleCameraMove, out InputAction cameraMove), Is.True);
            Assert.That(battleSelect.enabled, Is.False);
            Assert.That(cameraMove.enabled, Is.True);
        }

        [Test]
        public void DisableOverlay_DisablesMappedActionMap()
        {
            _manager.InitializeDefaultBattleContexts();

            _manager.DisableOverlay(BFInputOverlayContext.BattleCamera);

            Assert.That(_manager.IsOverlayEnabled(BFInputOverlayContext.BattleCamera), Is.False);
            Assert.That(_manager.TryGetAction(BFInputActionId.BattleCameraMove, out InputAction cameraMove), Is.True);
            Assert.That(cameraMove.enabled, Is.False);
        }

        [Test]
        public void TryRegisterPerformed_ReturnsDisposableSubscription()
        {
            _manager.InitializeDefaultBattleContexts();
            int callCount = 0;

            bool registered = _manager.TryRegisterPerformed(
                BFInputActionId.BattleSelect,
                _ => callCount++,
                out BFInputActionSubscription subscription);

            Assert.That(registered, Is.True);
            Assert.That(subscription, Is.Not.Null);

            subscription.Dispose();

            Assert.That(callCount, Is.EqualTo(0));
        }

        private static void SetInputActions(BFInputContextManager manager, InputActionAsset asset)
        {
            FieldInfo field = typeof(BFInputContextManager).GetField(
                "_inputActions",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(manager, asset);
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
