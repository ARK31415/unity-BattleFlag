using System;
using BF.Game.Runtime.Battle.Data;

namespace BF.Game.Runtime.Battle.Factory
{
    /// <summary>完整单位创建的统一对外合同。</summary>
    public interface IBFBattleUnitFactory : IDisposable
    {
        /// <summary>创建一个通用单位。</summary>
        BFBattleUnitCreationResult Create(BFBattleUnitCreateRequest request);

        /// <summary>从 Encounter 创建全部启用单位。</summary>
        BFBattleEncounterCreationResult CreateEncounter(BFBattleEncounterSO encounter);
    }
}
