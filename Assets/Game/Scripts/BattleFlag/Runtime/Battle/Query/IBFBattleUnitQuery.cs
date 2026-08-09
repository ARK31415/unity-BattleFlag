using System.Collections.Generic;

namespace BF.Game.Runtime.Battle.Query
{
    /// <summary>
    /// 面向表现消费者的规则状态查询合同。
    /// 返回值只能是不可变快照，不暴露 BFUnitState 或 UnitRuntime。
    /// </summary>
    public interface IBFBattleUnitQuery
    {
        /// <summary>查询合同所属的战斗身份。</summary>
        string BattleId { get; }

        /// <summary>按 RuntimeId 获取当前规则状态的表现快照。</summary>
        bool TryGetSnapshot(string runtimeId, out BFUnitViewSnapshot snapshot);

        /// <summary>获取当前会话内的全部单位快照。</summary>
        IReadOnlyList<BFUnitViewSnapshot> GetSnapshots();
    }
}
