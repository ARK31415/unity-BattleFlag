using System.Collections.Generic;
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
    /// 战斗场景根节点。装配三个 Manager（Board - UnitManager - TurnManager）、
    /// 输入控制器，按顺序初始化，提供场景级入口。
    ///
    /// 职责边界：
    /// - 负责战斗初始化流程（单位发现 - 棋盘对齐 - 注册 - 启动回合）。
    /// - 负责通过 WitUIManager 打开战斗 HUD（battle.hud key）。
    /// - 不负责回合规则、寻路计算、AI 决策、UI 内部逻辑。
    /// </summary>
    public class BFBattleRoot : MonoBehaviour
    {
        [Header("Managers")]
        [SerializeField] private BFBattleBoardManager _boardManager;
        [SerializeField] private BFBattleUnitManager _unitManager;
        [SerializeField] private BFBattleTurnManager _turnManager;
        [SerializeField] private BFBattleResolutionManager _resolutionManager;
        [SerializeField] private BFBattleUnitSpawner _unitSpawner;

        [Header("Event Channels")]
        [SerializeField] private BFTurnEventSO _turnEventChannel;
        [SerializeField] private BFBattleEventSO _battleEventChannel;
        [SerializeField] private BFUnitEventSO _unitEventChannel;

        [Header("Input / UI")]
        [SerializeField] private BFBattleInputController _inputController;
        [SerializeField] private BFBattleCameraController _cameraController;
        // WitUIManager 来自常驻场景 BFPersistent，负责打开战斗 HUD 等窗口。
        [SerializeField] private WitUIManager _uiManager;

        private void Awake()
        {
            ResolveMissingReferences();
        }

        private void Start()
        {
            InitializeBattle();
        }

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
            if (_uiManager == null) _uiManager = FindFirstObjectByType<WitUIManager>();
        }

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
            _turnManager?.StartBattle();

            // Step 6: 通过 WitUIManager 打开战斗 HUD
            OpenBattleHud();

            Debug.Log($"[BFBattleRoot] Battle initialized: {units.Count} units, " +
                      $"Board {_boardManager?.Width}x{_boardManager?.Height}");
        }

        // 通过 WitUIManager 打开 battle.hud 窗口，注入战斗所需的依赖引用。
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
