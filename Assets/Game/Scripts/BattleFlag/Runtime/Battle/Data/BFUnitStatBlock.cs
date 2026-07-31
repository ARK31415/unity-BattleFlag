using System;
using UnityEngine;

namespace BF.Game.Runtime.Battle.Data
{
    /// <summary>
    /// 单位基础战斗数值包（可序列化的值类型角色属性）。
    /// 用于保存一组干净的基础白值，不含运行时 HP/AP 资源。
    /// </summary>
    [Serializable]
    public struct BFUnitStatBlock
    {
        /// <summary>最大生命白值。</summary>
        [SerializeField] private int _maxHP;
        /// <summary>攻击力白值。</summary>
        [SerializeField] private int _attack;
        /// <summary>攻击范围（曼哈顿距离口径）。</summary>
        [SerializeField] private int _attackRange;
        /// <summary>发起一次攻击需要消耗的 AP。</summary>
        [SerializeField] private int _attackCost;
        /// <summary>每回合最大行动点数。</summary>
        [SerializeField] private int _maxActionPoints;

        /// <summary>
        /// 创建一组战斗数值白值，所有参数自动钳制到非负。
        /// </summary>
        public BFUnitStatBlock(int maxHP, int attack, int attackRange, int attackCost, int maxActionPoints)
        {
            _maxHP = Mathf.Max(0, maxHP);
            _attack = Mathf.Max(0, attack);
            _attackRange = Mathf.Max(0, attackRange);
            _attackCost = Mathf.Max(0, attackCost);
            _maxActionPoints = Mathf.Max(0, maxActionPoints);
        }

        /// <summary>默认测试属性包（HP:20 / ATK:5 / Range:1 / Cost:2 / AP:5）。</summary>
        public static BFUnitStatBlock Default => new(20, 5, 1, 2, 5);

        /// <summary>最大生命白值。</summary>
        public int MaxHP => _maxHP;
        /// <summary>攻击力白值。</summary>
        public int Attack => _attack;
        /// <summary>攻击范围（曼哈顿距离口径）。</summary>
        public int AttackRange => _attackRange;
        /// <summary>发起一次攻击需要消耗的 AP。</summary>
        public int AttackCost => _attackCost;
        /// <summary>每回合最大行动点数。</summary>
        public int MaxActionPoints => _maxActionPoints;
    }
}
