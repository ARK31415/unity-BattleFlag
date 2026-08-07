using System;
using System.Collections;
using System.Collections.Generic;
using BF.Game.Battle.Domain.Events;
using BF.Game.Runtime.Battle.Cameras;
using BF.Game.Runtime.Battle.Data;
using BF.Game.Runtime.Battle.Events;
using BF.Game.Runtime.Battle.Factory;
using BF.Game.Runtime.Battle.PlayerInput;
using BF.Game.Runtime.Battle.Units;
using BF.Game.Runtime.UI.Battle;
using UnityEngine;
using Wit.Framework.UI;
using DomainBattleContext = BF.Game.Battle.Domain.BFBattleContext;
using DomainBattleSession = BF.Game.Battle.Domain.BFBattleSession;

namespace BF.Game.Runtime.Battle.Managers
{
    /// <summary>
    /// 战斗场景根节点（MonoBehaviour）。装配三个 Manager（Board - UnitManager - TurnManager）、
    /// 输入控制器，按顺序初始化，提供场景级入口。
    ///
    /// 职责边界：
    /// - 负责战斗初始化流程（单位发现 - 棋盘对齐 - 注册 - 启动回合）。
    /// - 负责通过 WitUIManager 打开战斗 HUD（battle.hud key）。
    /// - 不负责回合规则、寻路计算、AI 决策、UI 内部逻辑。
    /// </summary>
    public class BFBattleRoot : MonoBehaviour
    {
        private const int UiManagerResolveMaxFrames = 60;

        [Header("Managers")]
        /// <summary>棋盘管理器。</summary>
        [SerializeField] private BFBattleBoardManager _boardManager;
        /// <summary>单位管理器。</summary>
        [SerializeField] private BFBattleUnitManager _unitManager;
        /// <summary>回合管理器。</summary>
        [SerializeField] private BFBattleTurnManager _turnManager;
        /// <summary>战斗结算管理器。</summary>
        [SerializeField] private BFBattleResolutionManager _resolutionManager;
        [Header("Battle Creation")]
        /// <summary>默认战斗单位唯一创建来源。</summary>
        [SerializeField] private BFBattleEncounterSO _encounter;
        /// <summary>单位 Runtime Prefab 选择配置。</summary>
        [SerializeField] private BFUnitFactoryConfigSO _factoryConfig;

        [Header("Event Channels")]
        /// <summary>回合事件通道。</summary>
        [SerializeField] private BFTurnEventSO _turnEventChannel;
        /// <summary>战斗事件通道。</summary>
        [SerializeField] private BFBattleEventSO _battleEventChannel;
        /// <summary>单位事件通道。</summary>
        [SerializeField] private BFUnitEventSO _unitEventChannel;

        private DomainBattleSession _battleSession;
        private BFUnitRegistry _unitRegistry;
        private BFBattleUnitFactory _unitFactory;
        private BFBattleEventToSOAdapter _battleEventAdapter;

        /// <summary>
        /// 当前战斗场景持有的战斗会话。
        /// </summary>
        public DomainBattleSession BattleSession => _battleSession;

        /// <summary>当前战斗会话的 Runtime 注册表。</summary>
        public BFUnitRegistry UnitRegistry => _unitRegistry;

        /// <summary>当前战斗会话的单位组合工厂。</summary>
        public BFBattleUnitFactory UnitFactory => _unitFactory;

        /// <summary>当前战斗场景使用的棋盘管理器。</summary>
        public BFBattleBoardManager BoardManager => _boardManager;

        /// <summary>当前战斗场景使用的单位管理器。</summary>
        public BFBattleUnitManager UnitManager => _unitManager;

        /// <summary>当前战斗场景使用的回合管理器。</summary>
        public BFBattleTurnManager TurnManager => _turnManager;

        /// <summary>当前战斗场景使用的结算管理器。</summary>
        public BFBattleResolutionManager ResolutionManager => _resolutionManager;

        /// <summary>当前战斗场景使用的战斗 SO 事件通道。</summary>
        public BFBattleEventSO BattleEventChannel => _battleEventChannel;

        /// <summary>当前战斗场景使用的回合 SO 事件通道。</summary>
        public BFTurnEventSO TurnEventChannel => _turnEventChannel;

        /// <summary>当前战斗场景使用的单位 SO 事件通道。</summary>
        public BFUnitEventSO UnitEventChannel => _unitEventChannel;

        [Header("Input / UI")]
        /// <summary>输入控制器。</summary>
        [SerializeField] private BFBattleInputController _inputController;
        /// <summary>摄像机控制器。</summary>
        [SerializeField] private BFBattleCameraController _cameraController;
        /// <summary>WitUIManager 来自常驻场景 BFPersistent，负责打开战斗 HUD 等窗口。</summary>
        [SerializeField] private WitUIManager _uiManager;

        /// <summary>
        /// Awake 中自动发现缺失的子组件引用，便于场景构建时减少手动拖拽。
        /// </summary>
        private void Awake()
        {
            ResolveMissingReferences();
        }

        /// <summary>
        /// Start 中执行完整的战斗初始化流程。
        /// </summary>
        private void Start()
        {
            InitializeBattle();
        }

        /// <summary>
        /// 自动发现缺失的子组件引用。
        /// 优先从子节点查找，跨场景则用 FindFirstObjectByType。
        /// </summary>
        private void ResolveMissingReferences()
        {
            if (_boardManager == null) _boardManager = GetComponentInChildren<BFBattleBoardManager>();
            if (_unitManager == null) _unitManager = GetComponentInChildren<BFBattleUnitManager>();
            if (_turnManager == null) _turnManager = GetComponentInChildren<BFBattleTurnManager>();
            if (_resolutionManager == null) _resolutionManager = GetComponentInChildren<BFBattleResolutionManager>();
            if (_inputController == null) _inputController = GetComponentInChildren<BFBattleInputController>();
            if (_cameraController == null) _cameraController = FindFirstObjectByType<BFBattleCameraController>();
            // WitUIManager 位于常驻场景 BFPersistent，通过 FindFirstObjectByType 跨场景查找。
            // 包含暂时未激活的对象，避免常驻场景初始化阶段漏检。
            if (_uiManager == null)
            {
                _uiManager = FindFirstObjectByType<WitUIManager>(FindObjectsInactive.Include);
            }
        }

        /// <summary>
        /// 战斗初始化主流程：创建 Domain Session，使用 Factory 生成并绑定单位，
        /// 再把 Factory 结果注入棋盘、UnitManager 和事件适配器。
        /// </summary>
        private void InitializeBattle()
        {
            Debug.Log("[BFBattleRoot] Initializing battle...");

            if (_encounter == null || _factoryConfig == null || _boardManager == null)
            {
                Debug.LogError(
                    "[BFBattleRoot] Encounter、FactoryConfig 或 BoardManager 缺失，无法初始化战斗。",
                    this);
                return;
            }

            if (string.IsNullOrWhiteSpace(_encounter.EncounterId))
            {
                Debug.LogError("[BFBattleRoot] EncounterId 缺失，无法创建 BattleSession。", this);
                return;
            }

            // EncounterId 是配置身份，BattleId 是本次运行的会话身份，不能直接复用。
            var battleId = $"{_encounter.EncounterId}_{Guid.NewGuid():N}";
            var battleContext = new DomainBattleContext(battleId);
            _battleSession = new DomainBattleSession(battleContext);
            _unitRegistry = new BFUnitRegistry(battleContext.BattleId);
            _unitFactory = new BFBattleUnitFactory(
                _battleSession,
                _unitRegistry,
                _factoryConfig,
                _boardManager,
                transform);

            var creationResult = _unitFactory.CreateEncounter(_encounter);
            if (!creationResult.Succeeded)
            {
                Debug.LogError($"[BFBattleRoot] Unit creation failed: {creationResult.Error}", this);
                DisposeBattleServices();
                return;
            }

            var units = new List<UnitRuntime>(creationResult.Units.Count);
            foreach (var createdUnit in creationResult.Units)
            {
                var unit = createdUnit.Runtime;
                unit.InitializeRuntime();
                unit.BeginBattle();
                unit.MovementHandler = _boardManager;
                units.Add(unit);
            }

            // Factory 已经使用 Encounter 的规则坐标完成棋盘占用；这里仅注入结果列表。
            foreach (var unit in units)
            {
                _unitManager?.RegisterUnit(unit);
            }

            // Step 4: 确保结算层能访问 UnitManager
            if (_resolutionManager != null && _unitManager != null)
            {
                _resolutionManager.SetUnitManager(_unitManager);
            }

            _turnManager?.SetBattleSession(_battleSession);
            _unitManager?.SetBattleSession(_battleSession);
            _battleEventAdapter = new BFBattleEventToSOAdapter(
                _battleSession,
                _battleEventChannel,
                _turnEventChannel,
                _unitEventChannel);
            _battleSession.Start();
            _battleSession.Publish(new BFBattleStartedEvent(battleContext.BattleId));

            _turnManager?.StartBattle();

            // Step 6: 等待常驻 UI 场景就绪后打开战斗 HUD。
            // BFPersistent 可能比战斗场景晚一帧完成加载，不能只在 Awake 中查找一次。
            StartCoroutine(OpenBattleHudWhenReady());

            Debug.Log($"[BFBattleRoot] Battle initialized: {units.Count} units, " +
                      $"Board {_boardManager?.Width}x{_boardManager?.Height}");
        }

        /// <summary>
        /// 销毁战斗根节点时，先解除 SO 适配订阅，再释放战斗会话。
        /// </summary>
        private void OnDestroy()
        {
            _battleEventAdapter?.Dispose();
            _battleEventAdapter = null;
            DisposeBattleServices();
        }

        /// <summary>
        /// 以工厂、Registry、Session 的所有权顺序释放当前战斗资源。
        /// </summary>
        private void DisposeBattleServices()
        {
            _unitFactory?.Dispose();
            _unitFactory = null;
            _unitRegistry?.Dispose();
            _unitRegistry = null;
            _battleSession?.Dispose();
            _battleSession = null;
        }

        private IEnumerator OpenBattleHudWhenReady()
        {
            for (var frame = 0; frame < UiManagerResolveMaxFrames; frame++)
            {
                ResolveMissingReferences();
                if (_uiManager != null)
                {
                    OpenBattleHud();
                    yield break;
                }

                yield return null;
            }

            Debug.LogWarning(
                "[BFBattleRoot] 未找到 WitUIManager，无法打开 HUD。请确认 BFPersistent 已在运行时加载，且场景中的 Canvas 已启用。");
        }

        /// <summary>
        /// 通过 WitUIManager 打开 battle.hud 窗口，注入战斗所需的依赖引用。
        /// </summary>
        private void OpenBattleHud()
        {
            if (_uiManager == null)
            {
                Debug.LogWarning("[BFBattleRoot] WitUIManager 未配置，跳过 HUD 打开。");
                return;
            }

            // 收集事件通道引用（从场景中查找 SO 资产）。
            var context = new BattleHudContext
            {
                TurnEventChannel = _turnEventChannel,
                BattleEventChannel = _battleEventChannel,
                UnitEventChannel = _unitEventChannel,
                TurnManager = _turnManager,
                UnitManager = _unitManager,
                InputController = _inputController,
                CameraFocusLock = _cameraController,
                UIManager = _uiManager
            };

            var result = _uiManager.Open("battle.hud", context);
            if (!result.Succeeded)
                Debug.LogWarning($"[BFBattleRoot] 战斗 HUD 打开失败: {result.Error}");
        }
    }
}
