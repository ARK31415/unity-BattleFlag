namespace BF.Game.Battle.Domain.Units
{
    /// <summary>
    /// 单位当前执行的规则行动状态。
    ///
    /// 该枚举描述规则层语义，不直接对应 Animator 状态；表现层可以根据它选择具体的动画和特效。
    /// </summary>
    public enum BFUnitActionState
    {
        /// <summary>当前没有执行规则行动。</summary>
        Idle,

        /// <summary>正在执行移动规则行动。</summary>
        Move,

        /// <summary>正在执行攻击规则行动。</summary>
        Attack,

        /// <summary>单位已经死亡，属于终止状态。</summary>
        Dead
    }
}
