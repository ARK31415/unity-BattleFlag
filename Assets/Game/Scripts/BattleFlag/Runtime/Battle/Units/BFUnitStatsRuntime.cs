using BF.Game.Runtime.Battle.Data;
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
        [SerializeField] private int _maxHP = 20;
        [SerializeField] private int _attack = 5;
        [SerializeField] private int _attackRange = 1;
        [SerializeField] private int _attackCost = 2;
        [SerializeField] private int _maxActionPoints = 5;

        [Header("Runtime Resources")]
        [SerializeField] private int _currentHP = 20;
        [SerializeField] private int _remainingActionPoints = 5;

        /// <summary>最大 HP 白值；调整该值不会自动治疗 CurrentHP。</summary>
        public int MaxHP
        {
            get => _maxHP;
            set => _maxHP = Mathf.Max(0, value);
        }

        /// <summary>当前 HP，会被限制在 0 到 MaxHP 之间。</summary>
        public int CurrentHP
        {
            get => _currentHP;
            set => _currentHP = Mathf.Clamp(value, 0, Mathf.Max(0, _maxHP));
        }

        /// <summary>基础攻击力，当前由最小伤害公式直接读取。</summary>
        public int Attack
        {
            get => _attack;
            set => _attack = Mathf.Max(0, value);
        }

        /// <summary>曼哈顿距离口径下的攻击范围。</summary>
        public int AttackRange
        {
            get => _attackRange;
            set => _attackRange = Mathf.Max(0, value);
        }

        /// <summary>发起一次攻击需要消耗的 AP。</summary>
        public int AttackCost
        {
            get => _attackCost;
            set => _attackCost = Mathf.Max(0, value);
        }

        /// <summary>每回合可恢复到的最大 AP。</summary>
        public int MaxActionPoints
        {
            get => _maxActionPoints;
            set => _maxActionPoints = Mathf.Max(0, value);
        }

        /// <summary>当前回合剩余 AP，会被限制在 0 到 MaxActionPoints 之间。</summary>
        public int RemainingActionPoints
        {
            get => _remainingActionPoints;
            set => _remainingActionPoints = Mathf.Clamp(value, 0, Mathf.Max(0, _maxActionPoints));
        }

        public bool HasActed => _remainingActionPoints <= 0;
        public bool IsAlive => _currentHP > 0;

        /// <summary>
        /// 从配置计算结果写入干净 Base 白值。
        /// </summary>
        public void InitializeBaseStats(BFUnitStatBlock stats, bool resetResources)
        {
            MaxHP = stats.MaxHP;
            Attack = stats.Attack;
            AttackRange = stats.AttackRange;
            AttackCost = stats.AttackCost;
            MaxActionPoints = stats.MaxActionPoints;

            if (resetResources)
            {
                ResetBattleResources();
            }
        }

        /// <summary>
        /// 战斗开始时重置运行时资源。
        ///
        /// 该入口用于场景初始化，不用于属性加成变化；后续 MaxHP 变化默认不应自动影响 CurrentHP。
        /// </summary>
        public void ResetBattleResources()
        {
            _currentHP = _maxHP;
            _remainingActionPoints = _maxActionPoints;
        }

        /// <summary>回合开始时恢复 AP，HP 不在回合切换中自动变化。</summary>
        public void ResetTurnActions()
        {
            _remainingActionPoints = _maxActionPoints;
        }

        /// <summary>
        /// 消耗 AP。
        ///
        /// 负数消耗会被视为 0，避免外部调用通过负数意外恢复行动点。
        /// </summary>
        /// <param name="amount">要消耗的行动点数。</param>
        public void ConsumeActionPoints(int amount)
        {
            _remainingActionPoints = Mathf.Max(0, _remainingActionPoints - Mathf.Max(0, amount));
        }

        /// <summary>
        /// 尝试应用伤害并返回是否致死。
        ///
        /// 非正数伤害或已死亡单位不会触发 HP 变化，调用方可据此避免播放受伤表现。
        /// </summary>
        /// <param name="damage">待应用伤害值。</param>
        /// <param name="wasKilled">输出 true 表示本次伤害使单位从存活变为死亡。</param>
        /// <returns>true 表示本次确实造成了正数伤害。</returns>
        public bool TryApplyDamage(int damage, out bool wasKilled)
        {
            wasKilled = false;
            if (!IsAlive) return false;

            int appliedDamage = Mathf.Max(0, damage);
            if (appliedDamage <= 0) return false;

            _currentHP = Mathf.Max(0, _currentHP - appliedDamage);
            wasKilled = !IsAlive;
            return true;
        }
    }
}
