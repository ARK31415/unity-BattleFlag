using BF.Game.Runtime.Battle.Managers;
using BF.Game.Runtime.Battle.Units;
using UnityEngine;

namespace BF.Game.Runtime.Battle.Presentation
{
    /// <summary>
    /// 单位表现桥接层。负责将 UnitRuntime 逻辑状态翻译为 Animator 参数，
    /// 并接收动画事件通知结算层。
    /// </summary>
    [RequireComponent(typeof(UnitRuntime))]
    [RequireComponent(typeof(Animator))]
    public class BFUnitAnimationPresenter : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private UnitRuntime _runtime;
        [SerializeField] private Animator _animator;
        [SerializeField] private SpriteRenderer _spriteRenderer;

        [Header("Resolution")]
        [SerializeField] private BFBattleResolutionManager _resolutionManager;

        private static readonly int IsMoving = Animator.StringToHash("IsMoving");
        private static readonly int IsDead = Animator.StringToHash("IsDead");
        private static readonly int Attack = Animator.StringToHash("Attack");
        private static readonly int Hurt = Animator.StringToHash("Hurt");

        private void Awake()
        {
            if (_runtime == null) _runtime = GetComponent<UnitRuntime>();
            if (_animator == null) _animator = GetComponent<Animator>();
            if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            if (_runtime != null)
            {
                _runtime.HurtReceived += OnHurtReceived;
                _runtime.DeathStarted += OnDeathStarted;
            }
        }

        private void OnDisable()
        {
            if (_runtime != null)
            {
                _runtime.HurtReceived -= OnHurtReceived;
                _runtime.DeathStarted -= OnDeathStarted;
            }
        }

        private void Update()
        {
            if (_runtime == null || _animator == null) return;

            _animator.SetBool(IsMoving, _runtime.CurrentState is UnitMoveState);
            _animator.SetBool(IsDead, !_runtime.IsAlive);
        }

        /// <summary>
        /// 设置 ResolutionManager 引用（由外部调用或在 Start 中查找）。
        /// </summary>
        public void SetResolutionManager(BFBattleResolutionManager resolutionManager)
        {
            _resolutionManager = resolutionManager;
        }

        private void OnHurtReceived(UnitRuntime unit)
        {
            if (_animator != null)
            {
                _animator.SetTrigger(Hurt);
            }
        }

        private void OnDeathStarted(UnitRuntime unit)
        {
            if (_animator != null)
            {
                _animator.SetBool(IsDead, true);
            }
        }

        /// <summary>
        /// 动画事件：攻击命中帧。
        /// </summary>
        public void OnAnimationAttackHit()
        {
            if (_runtime == null || _resolutionManager == null)
            {
                Debug.LogWarning("[BFUnitAnimationPresenter] Runtime 或 ResolutionManager 未绑定。");
                return;
            }

            if (_runtime.TryConsumeQueuedAttack(out _))
            {
                _resolutionManager.TryResolveQueuedAttack(_runtime);
            }
        }

        /// <summary>
        /// 动画事件：死亡动画完成。
        /// </summary>
        public void OnAnimationDeathFinished()
        {
            if (_runtime == null || _resolutionManager == null)
            {
                Debug.LogWarning("[BFUnitAnimationPresenter] Runtime 或 ResolutionManager 未绑定。");
                return;
            }

            _resolutionManager.NotifyDeathVisualFinished(_runtime);
        }

        /// <summary>
        /// 触发攻击动画（由外部调用或在状态进入时自动触发）。
        /// </summary>
        public void PlayAttack()
        {
            if (_animator != null)
            {
                _animator.SetTrigger(Attack);
            }
        }

        /// <summary>
        /// 刷新状态（用于外部强制刷新）。
        /// </summary>
        public void RefreshState()
        {
            if (_runtime == null || _animator == null) return;

            _animator.SetBool(IsMoving, _runtime.CurrentState is UnitMoveState);
            _animator.SetBool(IsDead, !_runtime.IsAlive);
        }
    }
}
