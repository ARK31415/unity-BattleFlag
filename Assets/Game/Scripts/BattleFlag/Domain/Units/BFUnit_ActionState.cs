namespace BF.Game.Battle.Domain.Units
{
    /// <summary>
    /// 单位当前执行的规则行动状态。
    ///
    /// 该枚举只描述规则层语义，不直接对应 Animator 或具体动画。
    /// </summary>
    public enum BFUnit_ActionState
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
