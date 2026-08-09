using BF.Game.Battle.Domain.Units;
using UnityEngine;

namespace BF.Game.Runtime.Battle.Units
{
    /// <summary>
    /// 单位数值运行时组件。
    ///
    /// 职责边界：
    /// - 保存当前战斗用的基础白值、HP 和 AP 运行时资源。
    /// - 负责扣血、消耗 AP、回合 AP 重置和存活判断。
    /// - 不负责选择目标、播放动画、改变格子占用或切换正式逻辑状态。
    ///
    /// Base 白值保持干净，只应由人工表格、外部配置、等级或关卡覆盖写入。
    /// 装备、全局加成和临时战斗加成后续应作为独立修正来源参与最终值计算。
    /// </summary>
    [DisallowMultipleComponent]
    public class BFUnitStatsRuntime : MonoBehaviour
    {
        [Header("Base Stats")]
        /// <summary>最大 HP 白值（默认 20）。</summary>
        [SerializeField] private int _maxHP = 20;
        /// <summary>攻击力白值（默认 5）。</summary>
        [SerializeField] private int _attack = 5;
        /// <summary>攻击范围，曼哈顿距离口径（默认 1）。</summary>
        [SerializeField] private int _attackRange = 1;
        /// <summary>发起一次攻击消耗的 AP（默认 2）。</summary>
        [SerializeField] private int _attackCost = 2;
        /// <summary>每回合最大 AP（默认 5）。</summary>
        [SerializeField] private int _maxActionPoints = 5;

        [Header("Runtime Resources")]
        /// <summary>当前 HP 运行时资源。</summary>
        [SerializeField] private int _currentHP = 20;
        /// <summary>当前剩余 AP 运行时资源。</summary>
        [SerializeField] private int _remainingActionPoints = 5;

        /// <summary>最大 HP 白值；调整该值不会自动治疗 CurrentHP。</summary>
        public int MaxHP
        {
            get => _maxHP;
            set => _maxHP = Mathf.Max(0, value);
        }

        /// <summary>当前 HP 运行时投影值。</summary>
        public int CurrentHP => _currentHP;

        /// <summary>基础攻击力投影。</summary>
        public int Attack
        {
            get => _attack;
            set => _attack = Mathf.Max(0, value);
        }

        /// <summary>曼哈顿距离口径下的攻击范围投影。</summary>
        public int AttackRange
        {
            get => _attackRange;
            set => _attackRange = Mathf.Max(0, value);
        }

        /// <summary>发起一次攻击需要消耗的 AP 投影。</summary>
        public int AttackCost
        {
            get => _attackCost;
            set => _attackCost = Mathf.Max(0, value);
        }

        /// <summary>每回合可恢复到的最大 AP 投影。</summary>
        public int MaxActionPoints
        {
            get => _maxActionPoints;
            set => _maxActionPoints = Mathf.Max(0, value);
        }

        /// <summary>当前回合剩余 AP 运行时投影值。</summary>
        public int RemainingActionPoints => _remainingActionPoints;

        /// <summary>本回合是否已行动（AP 耗尽）。</summary>
        public bool HasActed => _remainingActionPoints <= 0;
        /// <summary>是否存活（HP > 0）。</summary>
        public bool IsAlive => _currentHP > 0;

        /// <summary>
        /// 从规则属性投影当前有效属性和 HP/AP 资源。
        /// 该方法只写入 Unity 表现投影，不会反向修改规则状态。
        /// </summary>
        public void InitializeFromRuleState(BFUnitAttributes attributes)
        {
            if (attributes == null) return;

            MaxHP = attributes.EffectiveMaxHP;
            Attack = attributes.EffectiveAttackPower;
            AttackRange = attributes.EffectiveAttackRange;
            AttackCost = attributes.EffectiveAttackCost;
            MaxActionPoints = attributes.EffectiveMaxActionPoints;
            _currentHP = attributes.CurrentHP;
            _remainingActionPoints = attributes.RemainingActionPoints;
        }
    }
}
