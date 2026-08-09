using System;
using BF.Game.Battle.Domain.Units;
using BF.Game.Runtime.Battle.Data;
using BF.Game.Runtime.Battle.Factory;
using UnityEngine;

namespace BF.Game.Runtime.Battle.Units
{
    /// <summary>
    /// 单位根组件，作为场景发现锚点、子组件管理器和生命周期协调入口。
    ///
    /// 职责边界：
    /// - 负责缓存并校验 Identity、Stats、Grid、Combat、StateMachine 五个运行时子组件。
    /// - 负责下发战斗开始、回合开始、回合结束和死亡视觉清理等单位级生命周期。
    /// - 不保存阵营、HP、AP、格子、攻击上下文或正式状态等业务数据；外部系统应进入对应子组件读取。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BFUnitIdentityRuntime))]
    [RequireComponent(typeof(BFUnitStatsRuntime))]
    [RequireComponent(typeof(BFUnitGridRuntime))]
    [RequireComponent(typeof(BFUnitCombatRuntime))]
    [RequireComponent(typeof(BFUnit_PresentationStateMachineRuntime))]
    public class UnitRuntime : MonoBehaviour
    {
        [Header("Runtime Components")]
        /// <summary>单位身份子组件引用，可由 Inspector 预设；为空时运行期从同一根节点缓存或补齐。</summary>
        [SerializeField] private BFUnitIdentityRuntime _identity;
        /// <summary>单位数值子组件引用。</summary>
        [SerializeField] private BFUnitStatsRuntime _stats;
        /// <summary>单位格子子组件引用。</summary>
        [SerializeField] private BFUnitGridRuntime _grid;
        /// <summary>单位攻击上下文子组件引用。</summary>
        [SerializeField] private BFUnitCombatRuntime _combat;
        /// <summary>单位状态机子组件引用。</summary>
        [SerializeField] private BFUnit_PresentationStateMachineRuntime _stateMachine;

        private BFUnitState _ruleState;
        private BFUnitUnityBindingSO _unityBinding;
        private BFBattleUnitHandle _unitHandle;

        [Header("Optional Visual Cleanup")]
        /// <summary>死亡动画完成后统一关闭的 SpriteRenderer；为空时只跳过对应清理。</summary>
        [SerializeField] private SpriteRenderer _spriteRenderer;
        /// <summary>死亡动画完成后统一关闭的 Animator。</summary>
        [SerializeField] private Animator _animator;
        /// <summary>死亡动画完成后统一关闭的 Collider2D。</summary>
        [SerializeField] private Collider2D _collider2D;

        /// <summary>单位身份入口，包含显示名、阵营和角色类型。</summary>
        public BFUnitIdentityRuntime Identity => EnsureIdentity();

        /// <summary>单位数值入口，包含 HP、AP、攻击力和消耗等运行时数值。</summary>
        public BFUnitStatsRuntime Stats => EnsureStats();

        /// <summary>单位格子入口，包含当前格和出生格语义。</summary>
        public BFUnitGridRuntime Grid => EnsureGrid();

        /// <summary>单位攻击上下文入口，负责动画命中帧前后的待结算攻击状态。</summary>
        public BFUnitCombatRuntime Combat => EnsureCombat();

        /// <summary>单位表现状态机入口，只承载 Idle、Move、Attack、Dead 等表现状态。</summary>
        public BFUnit_PresentationStateMachineRuntime StateMachine => EnsureStateMachine();

        /// <summary>当前绑定的 RuntimeId；未绑定规则状态时为空。</summary>
        public string RuntimeId => _unitHandle?.RuntimeId;

        /// <summary>当前绑定的 BattleId；未绑定规则状态时为空。</summary>
        public string BattleId => _unitHandle?.BattleId;

        /// <summary>当前绑定的规则状态，只读暴露引用，修改仍由规则层负责。</summary>
        public BFUnitState RuleState => _ruleState;

        /// <summary>当前是否已完成规则状态绑定。</summary>
        public bool IsRuleBound => _ruleState != null && _unitHandle != null;

        /// <summary>当前单位实例使用的数据定义；场景手摆单位可为空。</summary>
        public BFUnitDefinitionSO Definition { get; private set; }

        /// <summary>移动能力注入入口；当前由棋盘管理器在单位吸附到格子时写入。</summary>
        public IMovementHandler MovementHandler { get; set; }

        /// <summary>受到非致死伤害时广播给表现层，Hurt 只作为动画覆盖效果。</summary>
        public event Action<UnitRuntime> HurtReceived;

        /// <summary>进入逻辑死亡时广播给表现层，视觉清理仍等待死亡动画完成事件。</summary>
        public event Action<UnitRuntime> DeathStarted;

        /// <summary>
        /// Unity 对象被禁用时通知适配层。
        /// 该通知不携带规则结果，规则状态恢复必须由订阅的管理器通过 Rules 完成。
        /// </summary>
        public event Action<UnitRuntime> Disabled;

        /// <summary>
        /// Inspector Reset 回调：自动补齐缺失的子组件。
        /// </summary>
        private void Reset()
        {
            CacheRuntimeComponents(addIfMissing: true);
        }

        /// <summary>
        /// Awake 建立最小可用依赖，避免 Root、Manager 或 Presenter 在 Start 前访问到半初始化单位。
        /// </summary>
        private void Awake()
        {
            InitializeRuntime();
        }

        /// <summary>
        /// 表现状态机由单位根统一驱动，表现状态数据本身归 StateMachine 组件。
        /// </summary>
        private void Update()
        {
            _stateMachine?.LogicUpdate();
        }

        /// <summary>
        /// 物理更新驱动当前状态的固定步长更新。
        /// </summary>
        private void FixedUpdate()
        {
            _stateMachine?.PhysicsUpdate();
        }

        /// <summary>
        /// 单位自身被禁用时清理本地表现上下文。
        ///
        /// 命中前禁用只清理 Combat 上下文并把表现状态恢复为 Idle；已经由规则层提交的
        /// AP、伤害和死亡结果不会回滚，规则状态恢复由适配层通过规则入口完成。
        /// </summary>
        private void OnDisable()
        {
            CleanupDisabledRuntime();
            Disabled?.Invoke(this);
        }

        /// <summary>
        /// 清理被禁用单位未完成的攻击上下文与表现状态。
        ///
        /// 该方法同时用于组件禁用回调与测试验证；规则状态的恢复由适配层负责，
        /// Runtime 不会因此重新成为规则事实来源（Spec 3.2 6.6）。
        /// </summary>
        internal void CleanupDisabledRuntime()
        {
            if (_combat != null)
            {
                _combat.ClearQueuedAttack();
            }

            if (_stateMachine != null && Stats != null && Stats.IsAlive &&
                _stateMachine.CurrentState != null &&
                _stateMachine.CurrentState != _stateMachine.IdleState)
            {
                _stateMachine.ChangeState(_stateMachine.IdleState);
            }
        }

        /// <summary>
        /// 初始化单位根的运行时依赖。
        ///
        /// 该方法可由场景根节点在战斗初始化阶段重复调用；已存在的子组件会被复用，
        /// 缺失组件会按 RequireComponent 合同补齐并记录清晰错误，避免静默半初始化。
        /// </summary>
        public void InitializeRuntime()
        {
            CacheRuntimeComponents(addIfMissing: true);
            CacheOptionalVisualComponents();
            ValidateRuntimeComponents();
            _stateMachine.Initialize(this);
        }

        /// <summary>
        /// 进入战斗时初始化表现生命周期并回到 Idle。
        ///
        /// 正式战斗单位的 HP、AP 等规则数值只能来自规则状态投影，不能在此处重置。
        /// </summary>
        public void BeginBattle()
        {
            InitializeRuntime();
            StateMachine.ChangeState(StateMachine.IdleState);
        }

        /// <summary>
        /// 接收规则状态并建立 Unity 表现投影。
        /// 新 Factory 使用此方法，不再把 Unity 配置作为规则状态来源。
        /// </summary>
        public void BindRuleState(
            BFUnitState state,
            BFUnitUnityBindingSO unityBinding,
            string displayName,
            BFBattleUnitHandle handle,
            BFUnitDefinitionSO definition = null)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (handle == null) throw new ArgumentNullException(nameof(handle));
            if (!string.Equals(state.RuntimeId, handle.RuntimeId, StringComparison.Ordinal))
                throw new ArgumentException("Rule state and handle RuntimeId do not match.", nameof(handle));

            InitializeRuntime();
            _ruleState = state;
            _unityBinding = unityBinding;
            _unitHandle = handle;
            Definition = definition;
            Grid.InitializeSpawnPosition(new Vector2Int(
                state.GridPosition.X,
                state.GridPosition.Y));
            RefreshRuleStateProjection(displayName);
        }

        /// <summary>刷新规则状态到 Identity、Stats 和 Grid 的表现投影。</summary>
        public void RefreshRuleStateProjection(string displayName = null)
        {
            if (!IsRuleBound) return;

            Identity.InitializeFromRuleState(_ruleState, displayName ?? Identity.DisplayName);
            Stats.InitializeFromRuleState(_ruleState.Attributes);
            Grid.SetGridPosition(new Vector2Int(
                _ruleState.GridPosition.X,
                _ruleState.GridPosition.Y));
            ApplyUnityBinding(_unityBinding);
        }

        /// <summary>解除规则状态与 Runtime 的绑定，不销毁 Unity 对象，并清理身份投影。</summary>
        public void UnbindRuleState()
        {
            _ruleState = null;
            _unitHandle = null;
            _unityBinding = null;
            Definition = null;
            Identity.ClearRuleIdentity();
        }

        /// <summary>
        /// 将已由规则层完成的伤害结果转换为表现反馈。
        ///
        /// 该入口只触发受伤/死亡表现，不写入规则状态或 Runtime 数值，
        /// 由调用方在规则成功后先刷新 <see cref="RefreshRuleStateProjection" />。
        /// </summary>
        /// <param name="wasKilled">规则层是否判定本次伤害致死。</param>
        public void ApplyRuleDamagePresentation(bool wasKilled)
        {
            if (!IsRuleBound) return;

            if (wasKilled)
            {
                DeathStarted?.Invoke(this);
                StateMachine.ChangeState(StateMachine.DeadState);
                return;
            }

            HurtReceived?.Invoke(this);
        }

        /// <summary>
        /// 死亡动画完成后的最终视觉清理入口。
        ///
        /// 只有单位已经处于 Dead 正式状态时才会执行，保证逻辑死亡先发生，表现对象延迟隐藏。
        /// </summary>
        public void FinalizeDeathVisualCleanup()
        {
            if (StateMachine.CurrentState != StateMachine.DeadState) return;

            if (gameObject != null)
            {
                if (_spriteRenderer != null) _spriteRenderer.enabled = false;

                if (_animator != null) _animator.enabled = false;

                if (_collider2D != null) _collider2D.enabled = false;

                gameObject.SetActive(false);
            }

            Debug.Log($"[UnitRuntime] {Identity.DisplayName} death visual cleanup finished.");
        }

        /// <summary>
        /// 确保 Identity 子组件已缓存，为空时自动补齐。
        /// </summary>
        private BFUnitIdentityRuntime EnsureIdentity()
        {
            if (_identity == null) CacheRuntimeComponents(addIfMissing: true);
            return _identity;
        }

        /// <summary>
        /// 确保 Stats 子组件已缓存，为空时自动补齐。
        /// </summary>
        private BFUnitStatsRuntime EnsureStats()
        {
            if (_stats == null) CacheRuntimeComponents(addIfMissing: true);
            return _stats;
        }

        /// <summary>
        /// 确保 Grid 子组件已缓存，为空时自动补齐。
        /// </summary>
        private BFUnitGridRuntime EnsureGrid()
        {
            if (_grid == null) CacheRuntimeComponents(addIfMissing: true);
            return _grid;
        }

        /// <summary>
        /// 确保 Combat 子组件已缓存，为空时自动补齐。
        /// </summary>
        private BFUnitCombatRuntime EnsureCombat()
        {
            if (_combat == null) CacheRuntimeComponents(addIfMissing: true);
            return _combat;
        }

        /// <summary>
        /// 确保 StateMachine 子组件已缓存并完成初始化，为空时自动补齐。
        /// </summary>
        private BFUnit_PresentationStateMachineRuntime EnsureStateMachine()
        {
            if (_stateMachine == null) CacheRuntimeComponents(addIfMissing: true);
            _stateMachine.Initialize(this);
            return _stateMachine;
        }

        /// <summary>
        /// 缓存或补齐五个运行时子组件。
        /// </summary>
        /// <param name="addIfMissing">为 true 时，缺失组件会自动 AddComponent。</param>
        private void CacheRuntimeComponents(bool addIfMissing)
        {
            _identity = GetOrAddComponent(_identity, addIfMissing);
            _stats = GetOrAddComponent(_stats, addIfMissing);
            _grid = GetOrAddComponent(_grid, addIfMissing);
            _combat = GetOrAddComponent(_combat, addIfMissing);
            _stateMachine = GetOrAddComponent(_stateMachine, addIfMissing);
        }

        /// <summary>
        /// 缓存三个可选视觉清理组件（SpriteRenderer、Animator、Collider2D）。
        /// </summary>
        private void CacheOptionalVisualComponents()
        {
            if (_spriteRenderer == null) TryGetComponent(out _spriteRenderer);
            if (_animator == null) TryGetComponent(out _animator);
            if (_collider2D == null) TryGetComponent(out _collider2D);
        }

        /// <summary>
        /// 校验五个运行时子组件是否全部存在，缺失时输出错误日志。
        /// </summary>
        private void ValidateRuntimeComponents()
        {
            if (_identity == null) Debug.LogError("[UnitRuntime] Missing BFUnitIdentityRuntime.", this);
            if (_stats == null) Debug.LogError("[UnitRuntime] Missing BFUnitStatsRuntime.", this);
            if (_grid == null) Debug.LogError("[UnitRuntime] Missing BFUnitGridRuntime.", this);
            if (_combat == null) Debug.LogError("[UnitRuntime] Missing BFUnitCombatRuntime.", this);
            if (_stateMachine == null) Debug.LogError("[UnitRuntime] Missing BFUnit_PresentationStateMachineRuntime.", this);
        }

        /// <summary>
        /// 应用 Unity 资源绑定（当前仅设置 AnimatorController）。
        /// </summary>
        /// <param name="binding">资源绑定配置，可以为 null。</param>
        private void ApplyUnityBinding(BFUnitUnityBindingSO binding)
        {
            if (binding == null) return;
            if (_animator == null || binding.AnimatorController == null) return;

            if (_animator.runtimeAnimatorController != binding.AnimatorController)
            {
                _animator.runtimeAnimatorController = binding.AnimatorController;
            }
        }

        /// <summary>
        /// 获取或添加指定类型的组件。优先复用已有引用，其次从 GameObject 上查找，
        /// 最后根据 addIfMissing 决定是否 AddComponent。
        /// </summary>
        private T GetOrAddComponent<T>(T current, bool addIfMissing) where T : Component
        {
            if (current != null) return current;
            if (TryGetComponent(out T found)) return found;
            return addIfMissing ? gameObject.AddComponent<T>() : null;
        }
    }
}
