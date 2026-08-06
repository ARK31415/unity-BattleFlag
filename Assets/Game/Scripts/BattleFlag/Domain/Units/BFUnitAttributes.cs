using System;

namespace BF.Game.Battle.Domain.Units
{
    /// <summary>
    /// 单位的纯规则属性容器。
    ///
    /// 职责边界：
    /// - 保存 Base、Bonus、Effective 和动态当前值。
    /// - 第一阶段只支持加法修正，并维护 HP/AP 的范围不变量。
    /// - 不保存装备、Buff 或 Unity 组件对象，也不负责攻击和技能结算。
    /// </summary>
    public sealed class BFUnitAttributes
    {
        private int _bonusMaxHP;
        private int _bonusMaxActionPoints;
        private int _bonusAttackPower;
        private int _currentHP;
        private int _remainingActionPoints;

        /// <summary>
        /// 创建单位基础属性，并将未指定的当前 HP/AP 初始化为对应最终上限。
        /// </summary>
        /// <param name="baseMaxHP">基础最大生命值白值。</param>
        /// <param name="baseMaxActionPoints">基础最大行动点白值。</param>
        /// <param name="baseAttackPower">基础攻击力白值。</param>
        /// <param name="currentHP">可选的初始当前生命值。</param>
        /// <param name="remainingActionPoints">可选的初始剩余行动点。</param>
        public BFUnitAttributes(
            int baseMaxHP,
            int baseMaxActionPoints,
            int baseAttackPower,
            int? currentHP = null,
            int? remainingActionPoints = null)
        {
            ValidateBaseValue(baseMaxHP, nameof(baseMaxHP));
            ValidateBaseValue(baseMaxActionPoints, nameof(baseMaxActionPoints));
            ValidateBaseValue(baseAttackPower, nameof(baseAttackPower));

            BaseMaxHP = baseMaxHP;
            BaseMaxActionPoints = baseMaxActionPoints;
            BaseAttackPower = baseAttackPower;
            _currentHP = Clamp(currentHP ?? EffectiveMaxHP, 0, EffectiveMaxHP);
            _remainingActionPoints = Clamp(
                remainingActionPoints ?? EffectiveMaxActionPoints,
                0,
                EffectiveMaxActionPoints);
        }

        /// <summary>最大生命基础白值。</summary>
        public int BaseMaxHP { get; }

        /// <summary>最大生命加法修正值。</summary>
        public int BonusMaxHP => _bonusMaxHP;

        /// <summary>计算得到的最终最大生命值。</summary>
        public int EffectiveMaxHP => CalculateEffectiveValue(BaseMaxHP, BonusMaxHP);

        /// <summary>当前生命值。</summary>
        public int CurrentHP => _currentHP;

        /// <summary>最大行动点基础白值。</summary>
        public int BaseMaxActionPoints { get; }

        /// <summary>最大行动点加法修正值。</summary>
        public int BonusMaxActionPoints => _bonusMaxActionPoints;

        /// <summary>计算得到的最终最大行动点。</summary>
        public int EffectiveMaxActionPoints =>
            CalculateEffectiveValue(BaseMaxActionPoints, BonusMaxActionPoints);

        /// <summary>当前回合剩余行动点。</summary>
        public int RemainingActionPoints => _remainingActionPoints;

        /// <summary>攻击力基础白值。</summary>
        public int BaseAttackPower { get; }

        /// <summary>攻击力加法修正值。</summary>
        public int BonusAttackPower => _bonusAttackPower;

        /// <summary>计算得到的最终攻击力。</summary>
        public int EffectiveAttackPower => CalculateEffectiveValue(BaseAttackPower, BonusAttackPower);

        /// <summary>当前生命值大于 0 时表示单位仍然存活。</summary>
        public bool IsAlive => CurrentHP > 0;

        /// <summary>
        /// 设置最大生命加法修正，并将当前生命值限制在新的最终上限内。
        /// 最大生命增加不会自动恢复当前生命。
        /// </summary>
        internal void SetBonusMaxHP(int value)
        {
            _bonusMaxHP = value;
            _currentHP = Clamp(_currentHP, 0, EffectiveMaxHP);
        }

        /// <summary>
        /// 设置最大行动点加法修正，并将剩余行动点限制在新的最终上限内。
        /// </summary>
        internal void SetBonusMaxActionPoints(int value)
        {
            _bonusMaxActionPoints = value;
            _remainingActionPoints = Clamp(_remainingActionPoints, 0, EffectiveMaxActionPoints);
        }

        /// <summary>设置攻击力加法修正。</summary>
        internal void SetBonusAttackPower(int value)
        {
            _bonusAttackPower = value;
        }

        /// <summary>设置当前生命值，并限制到合法范围。</summary>
        internal void SetCurrentHP(int value)
        {
            _currentHP = Clamp(value, 0, EffectiveMaxHP);
        }

        /// <summary>设置剩余行动点，并限制到合法范围。</summary>
        internal void SetRemainingActionPoints(int value)
        {
            _remainingActionPoints = Clamp(value, 0, EffectiveMaxActionPoints);
        }

        private static int CalculateEffectiveValue(int baseValue, int bonusValue)
        {
            return Math.Max(0, baseValue + bonusValue);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static void ValidateBaseValue(int value, string parameterName)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(parameterName, value, "基础属性值不能为负数。");
        }
    }
}
