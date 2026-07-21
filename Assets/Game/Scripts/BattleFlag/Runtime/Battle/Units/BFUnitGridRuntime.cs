using UnityEngine;

namespace BF.Game.Runtime.Battle.Units
{
    /// <summary>
    /// 单位格子位置运行时组件。
    ///
    /// 职责边界：
    /// - 保存当前格子和出生格语义，供棋盘、移动、AI 和表现层读取。
    /// - 不负责寻路、格子占用表或全局棋盘状态，这些仍归 BFBattleBoardManager。
    /// - 第一次写入 GridPosition 时会自动记录出生格，避免场景初始化后丢失出生点语义。
    /// </summary>
    [DisallowMultipleComponent]
    public class BFUnitGridRuntime : MonoBehaviour
    {
        [Header("Grid")]
        [SerializeField] private Vector2Int _gridPosition;
        [SerializeField] private Vector2Int _spawnGridPosition;
        [SerializeField] private bool _hasSpawnGridPosition;

        /// <summary>单位当前所在格子；写入时会在首次设置时捕获出生格。</summary>
        public Vector2Int GridPosition
        {
            get => _gridPosition;
            set => SetGridPosition(value);
        }

        /// <summary>单位出生格；若尚未显式捕获，则回退为当前格子。</summary>
        public Vector2Int SpawnGridPosition => _hasSpawnGridPosition ? _spawnGridPosition : _gridPosition;

        /// <summary>
        /// 设置当前格子。
        ///
        /// 第一次设置会同时记录 SpawnGridPosition，后续移动只更新当前格，不覆盖出生格。
        /// </summary>
        /// <param name="gridPosition">新的棋盘格坐标。</param>
        public void SetGridPosition(Vector2Int gridPosition)
        {
            _gridPosition = gridPosition;
            if (_hasSpawnGridPosition) return;

            _spawnGridPosition = gridPosition;
            _hasSpawnGridPosition = true;
        }

        /// <summary>
        /// 显式记录出生格。
        ///
        /// 用于后续数据驱动或场景校验需要先于移动流程确定出生点的情况。
        /// </summary>
        /// <param name="gridPosition">要作为出生格保存的棋盘格坐标。</param>
        public void CaptureSpawnGridPosition(Vector2Int gridPosition)
        {
            _spawnGridPosition = gridPosition;
            _hasSpawnGridPosition = true;
        }

        /// <summary>
        /// 数据驱动生成时显式写入出生格和当前格。
        /// </summary>
        public void InitializeSpawnPosition(Vector2Int gridPosition)
        {
            _gridPosition = gridPosition;
            _spawnGridPosition = gridPosition;
            _hasSpawnGridPosition = true;
        }
    }
}
