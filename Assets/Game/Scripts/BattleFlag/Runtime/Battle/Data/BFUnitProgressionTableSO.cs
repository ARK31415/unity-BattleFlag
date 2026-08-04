using System;
using System.Collections.Generic;
using UnityEngine;

namespace BF.Game.Runtime.Battle.Data
{
    /// <summary>
    /// 单位成长表（ScriptableObject），支持按等级配置不规则数值。
    /// 只负责按等级查找 BFUnitStatBlock，不负责 Unity 资源、关卡覆盖或运行时状态。
    /// </summary>
    [CreateAssetMenu(fileName = "BFUnitProgressionTable", menuName = "BF/Battle/Units/Progression Table")]
    public class BFUnitProgressionTableSO : ScriptableObject
    {
        /// <summary>成长条目列表。</summary>
        [SerializeField] private List<BFUnitProgressionEntry> _entries = new();

        /// <summary>成长条目只读列表。</summary>
        public IReadOnlyList<BFUnitProgressionEntry> Entries => _entries;

        /// <summary>
        /// 按等级查找成长属性。
        /// </summary>
        /// <param name="level">目标等级。</param>
        /// <param name="stats">查找到的属性包。</param>
        /// <returns>true 表示找到该等级配置。</returns>
        public bool TryGetStatsForLevel(int level, out BFUnitStatBlock stats)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Level != level) continue;

                stats = _entries[i].Stats;
                return true;
            }

            stats = default;
            return false;
        }
    }

    /// <summary>
    /// 单条成长条目：等级到属性包的映射。
    /// </summary>
    [Serializable]
    public struct BFUnitProgressionEntry
    {
        /// <summary>等级。</summary>
        [SerializeField] private int _level;
        /// <summary>该等级对应的属性包。</summary>
        [SerializeField] private BFUnitStatBlock _stats;

        /// <summary>等级。</summary>
        public int Level => _level;
        /// <summary>该等级对应的属性包。</summary>
        public BFUnitStatBlock Stats => _stats;
    }
}
