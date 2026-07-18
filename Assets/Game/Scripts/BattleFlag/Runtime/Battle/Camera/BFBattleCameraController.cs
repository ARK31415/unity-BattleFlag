using BF.Game.Runtime.Input;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Wit.Framework.Input;

namespace BF.Game.Runtime.Battle.Cameras
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CinemachineCamera))]
    public class BFBattleCameraController : MonoBehaviour
    {
        private const float ScrollWheelScale = 0.01f;

        [Header("References")]
        [SerializeField] private CinemachineCamera _cinemachineCamera;
        [SerializeField] private CinemachineConfiner2D _confiner2D;
        [SerializeField] private WitInputContextManager _inputContextManager;
        [SerializeField] private EventSystem _eventSystem;

        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 8f;

        [Header("Zoom")]
        [SerializeField] private float _zoomSpeed = 2f;
        [SerializeField] private float _minOrthographicSize = 4f;
        [SerializeField] private float _maxOrthographicSize = 12f;

        [Header("Smoothing")]
        [SerializeField] private float _smoothTime = 0.08f;

        [Header("Input Blocking")]
        [SerializeField] private bool _blockInputWhenPointerOverUI = true;

        private Transform _cachedTransform;
        private InputAction _moveAction;
        private InputAction _zoomAction;
        private Vector3 _targetPosition;
        private Vector3 _moveVelocity;
        private float _targetOrthographicSize;
        private float _zoomVelocity;
        private float _lastAppliedOrthographicSize;
        private bool _loggedMissingInputContext;
        private bool _loggedMissingActions;

        private void Reset()
        {
            TryGetComponent(out _cinemachineCamera);
            TryGetComponent(out _confiner2D);
            _moveSpeed = 8f;
            _zoomSpeed = 2f;
            _minOrthographicSize = 4f;
            _maxOrthographicSize = 12f;
            _smoothTime = 0.08f;
            _blockInputWhenPointerOverUI = true;
        }

        private void Awake()
        {
            CacheReferences();
            SnapTargetsToCurrentState();
        }

        private void OnEnable()
        {
            CacheReferences();
            ResolveInputActions();
            SnapTargetsToCurrentState();
        }

        private void Start()
        {
            CacheReferences();
            ResolveInputActions();
            ReportConfigurationIssues();
        }

        private void Update()
        {
            CacheRuntimeReferences();

            bool inputBlocked = ShouldBlockCameraInput();
            Vector2 moveInput = Vector2.zero;
            float zoomInput = 0f;

            if (!inputBlocked && TryResolveInputActions())
            {
                moveInput = _moveAction.ReadValue<Vector2>();
                zoomInput = _zoomAction.ReadValue<Vector2>().y;
            }

            StepCamera(moveInput, zoomInput, inputBlocked, Time.deltaTime);
        }

        private void OnValidate()
        {
            _moveSpeed = Mathf.Max(0f, _moveSpeed);
            _zoomSpeed = Mathf.Max(0f, _zoomSpeed);
            _minOrthographicSize = Mathf.Max(0.01f, _minOrthographicSize);
            _maxOrthographicSize = Mathf.Max(_minOrthographicSize, _maxOrthographicSize);
            _smoothTime = Mathf.Max(0f, _smoothTime);
        }

        private void CacheReferences()
        {
            _cachedTransform = transform;
            if (_cinemachineCamera == null)
                TryGetComponent(out _cinemachineCamera);
            if (_confiner2D == null)
                TryGetComponent(out _confiner2D);
            CacheRuntimeReferences();
        }

        private void CacheRuntimeReferences()
        {
            _inputContextManager ??= WitInputContextManager.Instance;
            _eventSystem ??= EventSystem.current;
        }

        private void ResolveInputActions()
        {
            if (_inputContextManager == null)
                return;

            _inputContextManager.TryGetAction(BFBattleFlagInputKeys.BattleCameraMove, out _moveAction);
            _inputContextManager.TryGetAction(BFBattleFlagInputKeys.BattleCameraZoom, out _zoomAction);
        }

        private bool TryResolveInputActions()
        {
            if (_moveAction != null && _zoomAction != null)
                return true;

            ResolveInputActions();
            bool hasActions = _moveAction != null && _zoomAction != null;
            if (!hasActions && !_loggedMissingActions)
            {
                Debug.LogWarning("[BattleCamera] Missing battle camera input actions.", this);
                _loggedMissingActions = true;
            }

            return hasActions;
        }

        private bool ShouldBlockCameraInput()
        {
            if (_inputContextManager == null)
            {
                if (!_loggedMissingInputContext)
                {
                    Debug.LogWarning("[BattleCamera] Missing WitInputContextManager; camera input is disabled.", this);
                    _loggedMissingInputContext = true;
                }
                return true;
            }

            if (!_inputContextManager.IsOverlayEnabled(BFBattleFlagInputKeys.BattleCameraOverlay))
                return true;

            return IsPointerOverUI();
        }

        private bool IsPointerOverUI()
        {
            if (!_blockInputWhenPointerOverUI || _eventSystem == null)
                return false;

            return _eventSystem.IsPointerOverGameObject();
        }

        private void SnapTargetsToCurrentState()
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;

            _targetPosition = _cachedTransform.position;
            float currentSize = GetCurrentOrthographicSize();
            _targetOrthographicSize = Mathf.Clamp(currentSize, _minOrthographicSize, _maxOrthographicSize);
            _lastAppliedOrthographicSize = currentSize;
            ApplyOrthographicSize(_targetOrthographicSize);
            _moveVelocity = Vector3.zero;
            _zoomVelocity = 0f;
        }

        private void StepCamera(Vector2 moveInput, float zoomInput, bool inputBlocked, float deltaTime)
        {
            if (_cinemachineCamera == null || _cachedTransform == null)
                return;

            if (inputBlocked)
            {
                HaltPendingMotion();
                return;
            }

            float dt = Mathf.Max(0f, deltaTime);
            Vector2 clampedMove = Vector2.ClampMagnitude(moveInput, 1f);
            Vector3 moveDelta = new(clampedMove.x, clampedMove.y, 0f);
            _targetPosition += moveDelta * (_moveSpeed * dt);

            if (!Mathf.Approximately(zoomInput, 0f))
            {
                float zoomDelta = zoomInput * _zoomSpeed * ScrollWheelScale;
                _targetOrthographicSize = Mathf.Clamp(
                    _targetOrthographicSize - zoomDelta,
                    _minOrthographicSize,
                    _maxOrthographicSize);
            }

            if (_smoothTime <= 0f || dt <= 0f)
            {
                _cachedTransform.position = _targetPosition;
                ApplyOrthographicSize(_targetOrthographicSize);
                return;
            }

            _cachedTransform.position = Vector3.SmoothDamp(
                _cachedTransform.position,
                _targetPosition,
                ref _moveVelocity,
                _smoothTime,
                Mathf.Infinity,
                dt);

            float smoothedSize = Mathf.SmoothDamp(
                GetCurrentOrthographicSize(),
                _targetOrthographicSize,
                ref _zoomVelocity,
                _smoothTime,
                Mathf.Infinity,
                dt);
            ApplyOrthographicSize(smoothedSize);
        }

        private void HaltPendingMotion()
        {
            if (_cachedTransform == null)
                return;

            _targetPosition = _cachedTransform.position;
            _targetOrthographicSize = GetCurrentOrthographicSize();
            _moveVelocity = Vector3.zero;
            _zoomVelocity = 0f;
        }

        private float GetCurrentOrthographicSize()
        {
            return _cinemachineCamera != null
                ? _cinemachineCamera.Lens.OrthographicSize
                : _minOrthographicSize;
        }

        private void ApplyOrthographicSize(float orthographicSize)
        {
            if (_cinemachineCamera == null)
                return;

            float clampedSize = Mathf.Clamp(orthographicSize, _minOrthographicSize, _maxOrthographicSize);
            LensSettings lens = _cinemachineCamera.Lens;
            lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
            lens.OrthographicSize = clampedSize;
            _cinemachineCamera.Lens = lens;

            if (!Mathf.Approximately(_lastAppliedOrthographicSize, clampedSize))
            {
                _confiner2D?.InvalidateLensCache();
                _lastAppliedOrthographicSize = clampedSize;
            }
        }

        private void ReportConfigurationIssues()
        {
            if (_cinemachineCamera == null)
                Debug.LogError("[BattleCamera] Missing CinemachineCamera component.", this);

            if (_confiner2D == null)
                Debug.LogWarning("[BattleCamera] Missing CinemachineConfiner2D; bounds limiting is unavailable.", this);
        }
    }
}
