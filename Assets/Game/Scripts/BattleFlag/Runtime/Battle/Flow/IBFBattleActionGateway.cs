using UnityEngine;

namespace BF.Game.Runtime.Battle.Flow
{
    /// <summary>
    /// 战斗行动的适配层入口。
    ///
    /// 外部消费者只提交 RuntimeId 和行动参数，不持有或传递 UnitRuntime。
    /// 具体的规则校验、Runtime 解析和表现生命周期由 BFBattleActionCoordinator 完成。
    /// </summary>
    public interface IBFBattleActionGateway
    {
        /// <summary>提交指定单位的移动请求。</summary>
        bool TryMove(string runtimeId, Vector2Int targetCell);

        /// <summary>提交指定攻击者对指定目标的攻击请求。</summary>
        bool TryAttack(string attackerRuntimeId, string targetRuntimeId);

        /// <summary>提交指定单位的等待请求。</summary>
        bool TryWait(string runtimeId);
    }
}
