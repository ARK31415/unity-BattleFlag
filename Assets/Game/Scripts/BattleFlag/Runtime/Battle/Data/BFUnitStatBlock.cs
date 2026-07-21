using System;
using UnityEngine;

namespace BF.Game.Runtime.Battle.Data
{
    /// <summary>
    /// 单位基础战斗数值包。
    /// </summary>
    [Serializable]
    public struct BFUnitStatBlock
    {
        [SerializeField] private int _maxHP;
        [SerializeField] private int _attack;
        [SerializeField] private int _attackRange;
        [SerializeField] private int _attackCost;
        [SerializeField] private int _maxActionPoints;

        public BFUnitStatBlock(int maxHP, int attack, int attackRange, int attackCost, int maxActionPoints)
        {
            _maxHP = Mathf.Max(0, maxHP);
            _attack = Mathf.Max(0, attack);
            _attackRange = Mathf.Max(0, attackRange);
            _attackCost = Mathf.Max(0, attackCost);
            _maxActionPoints = Mathf.Max(0, maxActionPoints);
        }

        public static BFUnitStatBlock Default => new(20, 5, 1, 2, 5);

        public int MaxHP => _maxHP;
        public int Attack => _attack;
        public int AttackRange => _attackRange;
        public int AttackCost => _attackCost;
        public int MaxActionPoints => _maxActionPoints;
    }
}
