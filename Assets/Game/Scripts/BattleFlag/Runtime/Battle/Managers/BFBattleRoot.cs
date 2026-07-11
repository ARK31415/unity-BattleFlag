using System.Collections.Generic;
using BF.Game.Runtime.Battle.PlayerInput;
using BF.Game.Runtime.Battle.Presentation;
using BF.Game.Runtime.Battle.Units;
using UnityEngine;

namespace BF.Game.Runtime.Battle.Managers
{
    /// <summary>
    /// 战斗场景根节点。装配三个 Manager（Board → UnitManager → TurnManager）、
    /// 输入控制器和 HUD，按顺序初始化，提供场景级入口。
    /// 不负责回合规则、寻路计算、AI 决策。
    /// </summary>
    public class BFBattleRoot : MonoBehaviour
    {
        [Header("Managers")]
        [SerializeField] private BFBattleBoardManager _boardManager;
        [SerializeField] private BFBattleUnitManager _unitManager;
        [SerializeField] private BFBattleTurnManager _turnManager;

        [Header("Input / UI")]
        [SerializeField] private BFBattleInputController _inputController;
        [SerializeField] private BFBattleHUD _hud;

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
            if (_inputController == null) _inputController = GetComponentInChildren<BFBattleInputController>();
            if (_hud == null) _hud = GetComponentInChildren<BFBattleHUD>();
        }

        private void InitializeBattle()
        {
            Debug.Log("[BFBattleRoot] Initializing battle...");

            // Step 1: 从场景中发现所有单位
            var units = new List<UnitRuntime>(
                FindObjectsByType<UnitRuntime>(FindObjectsSortMode.None));

            if (units.Count == 0)
            {
                Debug.LogError("[BFBattleRoot] No UnitRuntime found in scene!");
                return;
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

            // Step 4: 启动回合循环
            _turnManager?.StartBattle();

            Debug.Log($"[BFBattleRoot] Battle initialized: {units.Count} units, " +
                      $"Board {_boardManager?.Width}x{_boardManager?.Height}");
        }
    }
}
