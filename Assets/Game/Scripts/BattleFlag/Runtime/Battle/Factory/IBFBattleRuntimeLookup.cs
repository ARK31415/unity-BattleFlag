using System.Collections.Generic;
using BF.Game.Runtime.Battle.Units;

namespace BF.Game.Runtime.Battle.Factory
{
    /// <summary>
    /// 适配层内部的 Runtime 查询合同。
    ///
    /// 该接口只负责将当前 BattleSession 内的 RuntimeId 映射到 Unity Runtime，
    /// 不向规则层写入数据，也不应注入表现层 Widget 作为业务状态来源。
    /// </summary>
    public interface IBFBattleRuntimeLookup
    {
        /// <summary>查询合同所属的战斗身份。</summary>
        string BattleId { get; }

        /// <summary>当前会话已注册的 Runtime 集合。</summary>
        IReadOnlyCollection<UnitRuntime> Runtimes { get; }

        /// <summary>按 RuntimeId 查询当前会话内的 Unity Runtime。</summary>
        bool TryGetRuntime(string runtimeId, out UnitRuntime runtime);
    }
}
