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
    [RequireComponent(typeof(SpriteRenderer))]
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

            ApplyInitialFacing();
        }

        private void Start()
        {
            if (_resolutionManager == null)
            {
                _resolutionManager = FindFirstObjectByType<BFBattleResolutionManager>();
            }
        }


        private void OnEnable()
        {
            if (_runtime != null)
            {
                // Hurt 和 Death 仍是表现层事件，不进入正式逻辑状态机。
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

            // Presenter 只把正式状态和存活结果翻译成 Animator 参数，不反向修改 Stats 或 StateMachine。
            _animator.SetBool(IsMoving, _runtime.StateMachine.CurrentState is UnitMoveState);
            _animator.SetBool(IsDead, !_runtime.Stats.IsAlive);
        }

        /// <summary>
        /// 设置 ResolutionManager 引用（由外部调用或在 Start 中查找）。
        /// </summary>
        public void SetResolutionManager(BFBattleResolutionManager resolutionManager)
        {
            _resolutionManager = resolutionManager;
        }

        /// <summary>
        /// 初始化兜底朝向：玩家面朝右，敌人面朝左。
        /// </summary>
        public void ApplyInitialFacing()
        {
            if (_runtime == null || _spriteRenderer == null) return;

            SetFacingRight(_runtime.Identity.Faction != UnitFaction.Enemy);
        }

        /// <summary>
        /// 按移动分段设置朝向；纯上下移动保持当前朝向。
        /// </summary>
        public void FaceMovementStep(Vector2Int fromCell, Vector2Int toCell)
        {
            if (_spriteRenderer == null) return;
            if (toCell.x > fromCell.x)
            {
                SetFacingRight(true);
            }
            else if (toCell.x < fromCell.x)
            {
                SetFacingRight(false);
            }
        }

        /// <summary>
        /// 攻击前面向目标；同列目标保持当前朝向。
        /// </summary>
        public void FaceTarget(Vector2Int attackerCell, Vector2Int targetCell)
        {
            if (_spriteRenderer == null) return;
            if (targetCell.x > attackerCell.x)
            {
                SetFacingRight(true);
            }
            else if (targetCell.x < attackerCell.x)
            {
                SetFacingRight(false);
            }
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

            // 动画命中帧先消费 Combat 上下文；只有消费成功才通知结算层，防止重复动画事件多次扣血。
            if (_runtime.Combat.TryConsumeQueuedAttack(_runtime, out _))
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

            // 死亡动画完成后才通知结算层做最终隐藏，保证逻辑死亡和视觉退场解耦。
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

            _animator.SetBool(IsMoving, _runtime.StateMachine.CurrentState is UnitMoveState);
            _animator.SetBool(IsDead, !_runtime.Stats.IsAlive);
        }

        private void SetFacingRight(bool isFacingRight)
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.flipX = !isFacingRight;
            }
        }
    }
}
