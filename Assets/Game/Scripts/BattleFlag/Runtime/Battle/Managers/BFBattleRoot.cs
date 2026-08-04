using System.Collections;
using System.Collections.Generic;
using BF.Game.Battle.Domain.Events;
using BF.Game.Runtime.Battle.Cameras;
using BF.Game.Runtime.Battle.Events;
using BF.Game.Runtime.Battle.PlayerInput;
using BF.Game.Runtime.Battle.Units;
using BF.Game.Runtime.UI.Battle;
using UnityEngine;
using Wit.Framework.UI;

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
        /// <summary>单位生成器。</summary>
        [SerializeField] private BFBattleUnitSpawner _unitSpawner;

        [Header("Event Channels")]
        /// <summary>回合事件通道。</summary>
        [SerializeField] private BFTurnEventSO _turnEventChannel;
        /// <summary>战斗事件通道。</summary>
        [SerializeField] private BFBattleEventSO _battleEventChannel;
        /// <summary>单位事件通道。</summary>
        [SerializeField] private BFUnitEventSO _unitEventChannel;

        private BFBattleSession _battleSession;
        private BFBattleEventToSOAdapter _battleEventAdapter;

        /// <summary>
        /// 当前战斗场景持有的战斗会话。
        /// </summary>
        public BFBattleSession BattleSession => _battleSession;

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
            if (_unitSpawner == null) _unitSpawner = GetComponentInChildren<BFBattleUnitSpawner>();
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
        /// 战斗初始化主流程（6 步）：
        /// 1. 数据驱动生成单位（如果配置了 Spawner）
        /// 2. 从场景中发现所有 UnitRuntime
        /// 3. 初始化各单位（缓存子组件 + 重置战斗资源）
        /// 4. 棋盘对齐单位
        /// 5. 注册单位到 UnitManager
        /// 6. 启动回合循环 + 打开 HUD
        /// </summary>
        private void InitializeBattle()
        {
            Debug.Log("[BFBattleRoot] Initializing battle...");

            if (_unitSpawner != null && _unitSpawner.HasSpawnConfig)
            {
                _unitSpawner.TrySpawnConfiguredEncounter(_boardManager, out _);
            }

            // Step 1: 从场景中发现所有单位
            var units = new List<UnitRuntime>(
                FindObjectsByType<UnitRuntime>(FindObjectsSortMode.None));

            if (units.Count == 0)
            {
                Debug.LogError("[BFBattleRoot] No UnitRuntime found in scene!");
                return;
            }

            // 单位根先完成子组件缓存和战斗资源初始化，再交给棋盘和管理器读取。
            // 这样 Board、UnitManager、HUD 和 Presenter 后续访问 Identity/Stats/Grid 时不会遇到半初始化对象。
            foreach (var unit in units)
            {
                unit.InitializeRuntime();
                unit.BeginBattle();
            }

            // Step 2: 棋盘对齐单位
            if (_boardManager != null)
            {
                _boardManager.SnapUnitsToGrid(units);
            }

            // Step 3: 注册单位到 UnitManager
            foreach (var unit in units)
            {
                _unitManager?.RegisterUnit(unit);
            }

            // Step 4: 确保结算层能访问 UnitManager
            if (_resolutionManager != null && _unitManager != null)
            {
                _resolutionManager.SetUnitManager(_unitManager);
            }

            // Step 5: 启动回合循环
            var battleContext = new BFBattleContext
            {
                BattleId = "BattleTest",
                GridWidth = _boardManager != null ? _boardManager.Width : 10,
                GridHeight = _boardManager != null ? _boardManager.Height : 8,
                Units = units,
                TurnNumber = 0,
                RoundNumber = 0
            };
            _battleSession = new BFBattleSession(battleContext);
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
