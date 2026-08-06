using System;
using BF.Game.Battle.Domain.Events;

namespace BF.Game.Battle.Domain.Units
{
    /// <summary>
    /// 一个战斗单位的整体规则状态。
    ///
    /// 职责边界：
    /// - 保存 ProfileId、RuntimeId、阵营、角色、层级、属性、网格位置和行动状态。
    /// - 为适配层提供只读规则数据，不保存 Unity Runtime 或表现对象。
    /// - 行动状态变更由规则层受控入口执行，Dead 状态不可恢复。
    /// </summary>
    public sealed class BFUnitState
    {
        /// <summary>
        /// 创建一个单位规则状态。
        /// </summary>
        /// <param name="profileId">单位配置身份。</param>
        /// <param name="runtimeId">当前 BattleSession 内唯一的运行时身份。</param>
        /// <param name="faction">单位阵营。</param>
        /// <param name="role">单位战斗角色。</param>
        /// <param name="tier">单位层级或品质。</param>
        /// <param name="attributes">单位规则属性。</param>
        /// <param name="gridPosition">单位规则网格位置。</param>
        public BFUnitState(
            string profileId,
            string runtimeId,
            BFUnitFaction faction,
            BFUnitRole role,
            BFUnitTier tier,
            BFUnitAttributes attributes,
            BFGridPosition gridPosition)
        {
            ValidateIdentity(profileId, nameof(profileId));
            ValidateIdentity(runtimeId, nameof(runtimeId));
            Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));

            ProfileId = profileId;
            RuntimeId = runtimeId;
            Faction = faction;
            Role = role;
            Tier = tier;
            GridPosition = gridPosition;
            ActionState = BFUnitActionState.Idle;
        }

        /// <summary>单位配置身份。</summary>
        public string ProfileId { get; }

        /// <summary>当前 BattleSession 内的运行时单位身份。</summary>
        public string RuntimeId { get; }

        /// <summary>单位所属阵营。</summary>
        public BFUnitFaction Faction { get; }

        /// <summary>单位战斗角色。</summary>
        public BFUnitRole Role { get; }

        /// <summary>单位层级或品质。</summary>
        public BFUnitTier Tier { get; }

        /// <summary>单位规则属性。</summary>
        public BFUnitAttributes Attributes { get; }

        /// <summary>单位当前规则网格位置。</summary>
        public BFGridPosition GridPosition { get; private set; }

        /// <summary>单位当前规则行动状态。</summary>
        public BFUnitActionState ActionState { get; private set; }

        /// <summary>根据属性中的当前生命值推导单位是否存活。</summary>
        public bool IsAlive => Attributes.IsAlive;

        /// <summary>
        /// 尝试切换行动状态。
        ///
        /// Dead 只能在当前生命值为 0 时进入，进入 Dead 后不能恢复到其他状态。
        /// </summary>
        /// <param name="nextState">目标规则行动状态。</param>
        /// <returns>true 表示切换成功，false 表示违反当前状态不变量。</returns>
        internal bool TryChangeActionState(BFUnitActionState nextState)
        {
            if (ActionState == BFUnitActionState.Dead)
                return false;

            if (nextState == BFUnitActionState.Dead)
            {
                if (IsAlive) return false;

                ActionState = nextState;
                return true;
            }

            if (!IsAlive)
                return false;

            ActionState = nextState;
            return true;
        }

        /// <summary>更新规则网格位置，由规则移动流程调用。</summary>
        internal void SetGridPosition(BFGridPosition gridPosition)
        {
            GridPosition = gridPosition;
        }

        private static void ValidateIdentity(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("单位身份不能为空。", parameterName);
        }
    }
}
