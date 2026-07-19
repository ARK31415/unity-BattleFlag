using System;
using System.Collections.Generic;
using UnityEngine;

namespace BF.Game.Runtime.Battle.Units
{
    /// <summary>
    /// 移动能力注入接口。
    /// 由网格/寻路系统实现，注入到单位状态类中使用。
    /// 状态类不应各自重复实现移动规则。
    /// </summary>
    public interface IMovementHandler
    {
        /// <summary>
        /// 计算从当前位置可达的目标格子列表。
        /// </summary>
        /// <param name="unitPosition">单位当前格子坐标</param>
        /// <param name="moveRange">移动范围（格子数）</param>
        /// <param name="unitId">单位 ID（用于排除自身占用）</param>
        /// <returns>可达格子坐标列表</returns>
        List<Vector2Int> GetReachableCells(Vector2Int unitPosition, int moveRange, string unitId);

        /// <summary>
        /// 验证目标格子是否可合法移动。
        /// </summary>
        /// <param name="from">起始格子</param>
        /// <param name="to">目标格子</param>
        /// <param name="moveRange">移动范围</param>
        /// <param name="unitId">单位 ID</param>
        /// <returns>是否可合法移动</returns>
        bool IsCellReachable(Vector2Int from, Vector2Int to, int moveRange, string unitId);

        /// <summary>
        /// 获取从起点到终点的路径（格子坐标序列）。
        /// </summary>
        List<Vector2Int> FindPath(Vector2Int from, Vector2Int to, string unitId);

        /// <summary>
        /// 当单位移动到目标格子时调用，更新占用状态。
        /// </summary>
        void OccupyCell(Vector2Int cell, string unitId);

        /// <summary>
        /// 当单位离开格子时调用，释放占用状态。
        /// </summary>
        void ReleaseCell(Vector2Int cell, string unitId);
    }
}
