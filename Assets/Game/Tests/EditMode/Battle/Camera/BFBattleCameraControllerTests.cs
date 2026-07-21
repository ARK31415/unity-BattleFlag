using System.Reflection;
using BF.Game.Runtime.Battle.Cameras;
using BF.Game.Runtime.Input;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;

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
        public void ShouldBlockCameraInput_BlocksWhenBattleGameplayDisabled()
        {
            using TestInputManager testInput = new();
            SetPrivateField(_controller, "_inputManager", testInput.Manager);
            SetPrivateField(_controller, "_blockInputWhenPointerOverUI", false);

            Assert.That((bool)InvokePrivate(_controller, "ShouldBlockCameraInput"), Is.True);

            testInput.Manager.EnableGroup(BFInputMapGroupId.BattleGameplay);

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

        private sealed class TestInputManager : System.IDisposable
        {
            private readonly GameObject _owner;
            private readonly BFInputConfig _config;

            public TestInputManager()
            {
                _config = ScriptableObject.CreateInstance<BFInputConfig>();
                _config.SetTestDefinitions(
                    new[]
                    {
                        new BFInputMapGroupDefinition(BFInputMapGroupId.BattleGameplay,
                            new[] { BFInputActionMapId.Battle, BFInputActionMapId.BattleCamera, BFInputActionMapId.Global })
                    },
                    new[]
                    {
                        new BFInputProfileDefinition(BFInputProfileId.BattleHud,
                            new[] { BFInputMapGroupId.BattleGameplay })
                    },
                    BFInputProfileId.BattleHud);

                _owner = new GameObject("BFInputManager");
                _owner.SetActive(false);
                Manager = _owner.AddComponent<BFInputManager>();
                SetPrivateField(Manager, "_config", _config);
                SetPrivateField(Manager, "_applyStartupProfileOnAwake", false);
                InvokePrivate(Manager, "Awake");
            }

            public BFInputManager Manager { get; }

            public void Dispose()
            {
                if (Manager != null)
                    InvokePrivate(Manager, "OnDestroy");
                if (_owner != null)
                    Object.DestroyImmediate(_owner);
                if (_config != null)
                    Object.DestroyImmediate(_config);
            }
        }
    }
}
