using System;
using System.Collections.Generic;
using UnityEngine;

namespace BF.Game.Runtime.Battle.Data
{
    /// <summary>
    /// 单位成长表，支持按等级配置不规则数值。
    /// </summary>
    [CreateAssetMenu(fileName = "BFUnitProgressionTable", menuName = "BF/Battle/Units/Progression Table")]
    public class BFUnitProgressionTableSO : ScriptableObject
    {
        [SerializeField] private List<BFUnitProgressionEntry> _entries = new();

        public IReadOnlyList<BFUnitProgressionEntry> Entries => _entries;

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

    [Serializable]
    public struct BFUnitProgressionEntry
    {
        [SerializeField] private int _level;
        [SerializeField] private BFUnitStatBlock _stats;

        public int Level => _level;
        public BFUnitStatBlock Stats => _stats;
    }
}
