namespace BF.Game.Battle.Domain.Units
{
    /// <summary>
    /// 旧规则行动状态兼容类型。
    /// 新代码必须使用 <see cref="BFUnit_ActionState"/>。
    /// </summary>
    [System.Obsolete("Use BFUnit_ActionState instead.")]
    public enum BFUnitActionState
    {
        Idle,
        Move,
        Attack,
        Dead
    }
}
