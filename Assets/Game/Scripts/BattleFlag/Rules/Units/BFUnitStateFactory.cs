using System;
using BF.Game.Battle.Domain.Units;

namespace BF.Game.Battle.Rules.Units
{
    /// <summary>
    /// 创建单位规则状态的纯 C# 工厂。
    /// </summary>
    public sealed class BFUnitStateFactory
    {
        /// <summary>
        /// 使用会话内生成的 RuntimeId 创建完整规则状态。
        /// </summary>
        /// <param name="runtimeId">当前 BattleSession 内唯一的运行时身份。</param>
        /// <param name="data">已完成配置解析的规则创建数据。</param>
        public BFUnitState Create(string runtimeId, BFUnitStateCreationData data)
        {
            if (string.IsNullOrWhiteSpace(runtimeId))
                throw new ArgumentException("RuntimeId 不能为空。", nameof(runtimeId));

            return new BFUnitState(
                data.ProfileId,
                runtimeId,
                data.Faction,
                data.Role,
                data.Tier,
                data.UnitLevel,
                data.Attributes,
                data.GridPosition);
        }
    }
}
