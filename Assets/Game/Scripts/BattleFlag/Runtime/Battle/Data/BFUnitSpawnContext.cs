using BF.Game.Runtime.Battle.Units;
using UnityEngine;

namespace BF.Game.Runtime.Battle.Data
{
    /// <summary>
    /// 单位生成时由关卡或 Battle Setup 提供的运行时初始化上下文（只读值类型结构体）。
    /// 保存出生格与最终阵营。
    /// </summary>
    public readonly struct BFUnitSpawnContext
    {
        /// <summary>
        /// 创建生成上下文。
        /// </summary>
        /// <param name="gridPosition">单位出生格坐标。</param>
        /// <param name="faction">经 Encounter 覆盖解析后的最终阵营。</param>
        public BFUnitSpawnContext(Vector2Int gridPosition, UnitFaction faction)
        {
            GridPosition = gridPosition;
            Faction = faction;
        }

        /// <summary>单位出生格坐标。</summary>
        public Vector2Int GridPosition { get; }
        /// <summary>最终阵营。</summary>
        public UnitFaction Faction { get; }
    }
}
