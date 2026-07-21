using BF.Game.Runtime.Battle.Units;
using UnityEngine;

namespace BF.Game.Runtime.Battle.Data
{
    /// <summary>
    /// 单位生成时由关卡或 Battle Setup 提供的运行时初始化上下文。
    /// </summary>
    public readonly struct BFUnitSpawnContext
    {
        public BFUnitSpawnContext(Vector2Int gridPosition, UnitFaction faction)
        {
            GridPosition = gridPosition;
            Faction = faction;
        }

        public Vector2Int GridPosition { get; }
        public UnitFaction Faction { get; }
    }
}
