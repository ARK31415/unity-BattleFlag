using System.Collections.Generic;
using System.Reflection;
using BF.Game.Runtime.Battle.Cameras;
using BF.Game.Runtime.Input;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using Wit.Framework.Input;

namespace BF.Game.Tests.EditMode.Battle.Cameras
{
    public class BFBattleCameraControllerTests
    {
        private GameObject _cameraObject;
        private CinemachineCamera _cinemachineCamera;
        private BFBattleCameraController _controller;

        [SetUp]
        public void SetUp()
        {
            _cameraObject = new GameObject("BattleCamera");
            _cinemachineCamera = _cameraObject.AddComponent<CinemachineCamera>();
            _controller = _cameraObject.AddComponent<BFBattleCameraController>();
            SetPrivateField(_controller, "_smoothTime", 0f);
            SetPrivateField(_controller, "_moveSpeed", 8f);
            SetPrivateField(_controller, "_zoomSpeed", 2f);
            SetPrivateField(_controller, "_minOrthographicSize", 4f);
            SetPrivateField(_controller, "_maxOrthographicSize", 12f);
            SetOrthographicSize(5f);
            InvokePrivate(_controller, "SnapTargetsToCurrentState");
        }

        [TearDown]
        public void TearDown()
        {
            if (_cameraObject != null)
                Object.DestroyImmediate(_cameraObject);
        }

        [Test]
        public void StepCamera_BlockedInput_DoesNotMoveOrZoom()
        {
            _cameraObject.transform.position = new Vector3(1f, 2f, -10f);
            SetOrthographicSize(5f);
            InvokePrivate(_controller, "SnapTargetsToCurrentState");

            StepCamera(Vector2.right, 120f, true, 1f);

            Assert.That(_cameraObject.transform.position, Is.EqualTo(new Vector3(1f, 2f, -10f)));
            Assert.That(_cinemachineCamera.Lens.OrthographicSize, Is.EqualTo(5f));
        }

        [Test]
        public void StepCamera_MoveInput_UsesWorldXYPlane()
        {
            _cameraObject.transform.position = new Vector3(1f, 2f, -10f);
            InvokePrivate(_controller, "SnapTargetsToCurrentState");

            StepCamera(Vector2.right, 0f, false, 1f);

            Assert.That(_cameraObject.transform.position.x, Is.EqualTo(9f).Within(0.0001f));
            Assert.That(_cameraObject.transform.position.y, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(_cameraObject.transform.position.z, Is.EqualTo(-10f).Within(0.0001f));
        }

        [Test]
        public void StepCamera_ZoomInput_ClampsOrthographicSize()
        {
            StepCamera(Vector2.zero, 1000f, false, 1f);
            Assert.That(_cinemachineCamera.Lens.OrthographicSize, Is.EqualTo(4f));

            StepCamera(Vector2.zero, -1000f, false, 1f);
            Assert.That(_cinemachineCamera.Lens.OrthographicSize, Is.EqualTo(12f));
        }

        [Test]
        public void SnapTargetsToCurrentState_ClampsInitialOrthographicSize()
        {
            SetOrthographicSize(2f);

            InvokePrivate(_controller, "SnapTargetsToCurrentState");

            Assert.That(_cinemachineCamera.Lens.OrthographicSize, Is.EqualTo(4f));
        }

        [Test]
        public void ShouldBlockCameraInput_RespectsBattleCameraOverlay()
        {
            using TestInputContext inputContext = new();
            SetPrivateField(_controller, "_inputContextManager", inputContext.Manager);
            SetPrivateField(_controller, "_blockInputWhenPointerOverUI", false);

            Assert.That((bool)InvokePrivate(_controller, "ShouldBlockCameraInput"), Is.True);

            inputContext.Manager.EnableOverlay(BFBattleFlagInputKeys.BattleCameraOverlay);

            Assert.That((bool)InvokePrivate(_controller, "ShouldBlockCameraInput"), Is.False);
        }

        private void StepCamera(Vector2 moveInput, float zoomInput, bool inputBlocked, float deltaTime)
        {
            InvokePrivate(_controller, "StepCamera", moveInput, zoomInput, inputBlocked, deltaTime);
        }

        private void SetOrthographicSize(float orthographicSize)
        {
            LensSettings lens = _cinemachineCamera.Lens;
            lens.OrthographicSize = orthographicSize;
            _cinemachineCamera.Lens = lens;
        }

        private static object InvokePrivate(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(target, args);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }

        private sealed class TestInputContext : System.IDisposable
        {
            private readonly GameObject _owner;
            private readonly WitInputContextConfig _config;
            private readonly InputActionAsset _inputActions;

            public TestInputContext()
            {
                _inputActions = CreateInputActions();
                _config = CreateConfig(_inputActions);
                _owner = new GameObject("InputContextManager");
                Manager = _owner.AddComponent<WitInputContextManager>();
                SetPrivateField(Manager, "_config", _config);
            }

            public WitInputContextManager Manager { get; }

            public void Dispose()
            {
                if (_owner != null)
                    Object.DestroyImmediate(_owner);
                if (_config != null)
                    Object.DestroyImmediate(_config);
                if (_inputActions != null)
                    Object.DestroyImmediate(_inputActions);
            }

            private static WitInputContextConfig CreateConfig(InputActionAsset asset)
            {
                WitInputContextConfig config = ScriptableObject.CreateInstance<WitInputContextConfig>();
                SetPrivateField(config, "_inputActions", asset);
                SetPrivateField(config, "_overlayContexts", new List<WitInputContextDefinition>
                {
                    CreateContext(BFBattleFlagInputKeys.BattleCameraOverlay, "BattleCamera")
                });
                SetPrivateField(config, "_actions", new List<WitInputActionDefinition>
                {
                    CreateAction(BFBattleFlagInputKeys.BattleCameraMove, "BattleCamera", "Move"),
                    CreateAction(BFBattleFlagInputKeys.BattleCameraZoom, "BattleCamera", "Zoom")
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

            private static InputActionAsset CreateInputActions()
            {
                InputActionAsset asset = ScriptableObject.CreateInstance<InputActionAsset>();
                InputActionMap camera = new("BattleCamera");
                camera.AddAction("Move", InputActionType.Value);
                camera.AddAction("Zoom", InputActionType.Value, "<Mouse>/scroll");
                asset.AddActionMap(camera);
                return asset;
            }
        }
    }
}
