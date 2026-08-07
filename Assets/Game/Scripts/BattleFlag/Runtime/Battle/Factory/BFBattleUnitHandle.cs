using System;

namespace BF.Game.Runtime.Battle.Factory
{
    /// <summary>
    /// 跨规则、适配和表现边界传递的单位身份句柄。
    /// 句柄只保存 BattleId 与 RuntimeId，不持有可变状态或 Unity 对象。
    /// </summary>
    public sealed class BFBattleUnitHandle
    {
        /// <summary>创建一个单位身份句柄。</summary>
        public BFBattleUnitHandle(string battleId, string runtimeId)
        {
            if (string.IsNullOrWhiteSpace(battleId))
                throw new ArgumentException("BattleId 不能为空。", nameof(battleId));
            if (string.IsNullOrWhiteSpace(runtimeId))
                throw new ArgumentException("RuntimeId 不能为空。", nameof(runtimeId));

            BattleId = battleId;
            RuntimeId = runtimeId;
        }

        /// <summary>所属战斗会话身份。</summary>
        public string BattleId { get; }

        /// <summary>当前战斗内的运行时单位身份。</summary>
        public string RuntimeId { get; }

        /// <inheritdoc />
        public override string ToString() => $"{BattleId}/{RuntimeId}";
    }
}
