using BF.Game.Battle.Domain.Units;
using BF.Game.Battle.Domain.Events;

namespace BF.Game.Runtime.Battle.Query
{
    /// <summary>
    /// 单位规则状态到表现层的不可变投影。
    ///
    /// HP、AP、攻击力、位置、行动状态和存活状态全部来自 BFUnitState；
    /// DisplayName 只作为配置展示文本附带进入快照，不构成规则状态来源。
    /// </summary>
    public readonly struct BFUnitViewSnapshot
    {
        public BFUnitViewSnapshot(
            string battleId,
            string profileId,
            string runtimeId,
            string displayName,
            BFUnitFaction faction,
            BFUnitRole role,
            BFUnitTier tier,
            int unitLevel,
            int currentHP,
            int maxHP,
            int attack,
            int attackRange,
            int attackCost,
            int remainingActionPoints,
            int maxActionPoints,
            BFGridPosition gridPosition,
            BFUnit_ActionState actionState,
            bool isAlive)
        {
            BattleId = battleId;
            ProfileId = profileId;
            RuntimeId = runtimeId;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Unit" : displayName;
            Faction = faction;
            Role = role;
            Tier = tier;
            UnitLevel = unitLevel;
            CurrentHP = currentHP;
            MaxHP = maxHP;
            Attack = attack;
            AttackRange = attackRange;
            AttackCost = attackCost;
            RemainingActionPoints = remainingActionPoints;
            MaxActionPoints = maxActionPoints;
            GridPosition = gridPosition;
            ActionState = actionState;
            IsAlive = isAlive;
        }

        public string BattleId { get; }
        public string ProfileId { get; }
        public string RuntimeId { get; }
        public string DisplayName { get; }
        public BFUnitFaction Faction { get; }
        public BFUnitRole Role { get; }
        public BFUnitTier Tier { get; }
        public int UnitLevel { get; }
        public int CurrentHP { get; }
        public int MaxHP { get; }
        public int Attack { get; }
        public int AttackRange { get; }
        public int AttackCost { get; }
        public int RemainingActionPoints { get; }
        public int MaxActionPoints { get; }
        public BFGridPosition GridPosition { get; }
        public BFUnit_ActionState ActionState { get; }
        public bool IsAlive { get; }
        public bool HasActed => RemainingActionPoints <= 0;
    }
}
