using System.Collections.Generic;
using BF.Game.Runtime.Battle.Units;

namespace BF.Game.Runtime.Battle
{
    /// <summary>
    /// 战斗上下文数据。
    /// 承载一次战斗的地图配置、单位列表、回合状态与战斗结果。
    /// 不混入 UI 引用。
    /// </summary>
    public class BFBattleContext
    {
        /// <summary>
        /// 战斗唯一标识。
        /// </summary>
        public string BattleId { get; set; } = "TestBattle";

        /// <summary>
        /// 地图网格宽度（格子数）。
        /// </summary>
        public int GridWidth { get; set; } = 10;

        /// <summary>
        /// 地图网格高度（格子数）。
        /// </summary>
        public int GridHeight { get; set; } = 8;

        /// <summary>
        /// 当前回合编号（从 1 开始）。
        /// </summary>
        public int TurnNumber { get; set; } = 0;

        /// <summary>
        /// 当前轮次编号（双方各行动一次为一轮）。
        /// </summary>
        public int RoundNumber { get; set; } = 0;

        /// <summary>
        /// 战场上所有单位。
        /// </summary>
        public List<UnitRuntime> Units { get; set; } = new();

        /// <summary>
        /// 当前活跃单位的索引。
        /// </summary>
        public int ActiveUnitIndex { get; set; } = 0;

        /// <summary>
        /// 当前活跃单位。
        /// </summary>
        public UnitRuntime ActiveUnit
        {
            get
            {
                if (Units == null || Units.Count == 0) return null;
                if (ActiveUnitIndex < 0 || ActiveUnitIndex >= Units.Count) return null;
                return Units[ActiveUnitIndex];
            }
        }

        /// <summary>
        /// 获取指定阵营的所有单位。
        /// </summary>
        public List<UnitRuntime> GetUnitsByFaction(UnitFaction faction)
        {
            return Units?.FindAll(u => u != null && u.Faction == faction) ?? new List<UnitRuntime>();
        }

        /// <summary>
        /// 获取存活单位。
        /// </summary>
        public List<UnitRuntime> GetAliveUnits()
        {
            return Units?.FindAll(u => u != null && u.IsAlive) ?? new List<UnitRuntime>();
        }

        /// <summary>
        /// 获取指定阵营的存活单位。
        /// </summary>
        public List<UnitRuntime> GetAliveUnitsByFaction(UnitFaction faction)
        {
            return Units?.FindAll(u => u != null && u.Faction == faction && u.IsAlive)
                   ?? new List<UnitRuntime>();
        }
    }
}
