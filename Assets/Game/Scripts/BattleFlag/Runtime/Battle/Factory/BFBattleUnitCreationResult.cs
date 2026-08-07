using System.Collections.Generic;
using BF.Game.Runtime.Battle.Units;

namespace BF.Game.Runtime.Battle.Factory
{
    /// <summary>单个单位创建结果。</summary>
    public sealed class BFBattleUnitCreationResult
    {
        private BFBattleUnitCreationResult(
            bool succeeded,
            BFBattleUnitHandle handle,
            UnitRuntime runtime,
            string error)
        {
            Succeeded = succeeded;
            Handle = handle;
            Runtime = runtime;
            Error = error ?? string.Empty;
        }

        /// <summary>是否创建成功。</summary>
        public bool Succeeded { get; }

        /// <summary>成功时的跨层身份句柄。</summary>
        public BFBattleUnitHandle Handle { get; }

        /// <summary>成功时创建并绑定的 Unity Runtime。</summary>
        public UnitRuntime Runtime { get; }

        /// <summary>失败时的明确错误信息。</summary>
        public string Error { get; }

        internal static BFBattleUnitCreationResult Success(
            BFBattleUnitHandle handle,
            UnitRuntime runtime)
        {
            return new BFBattleUnitCreationResult(true, handle, runtime, string.Empty);
        }

        internal static BFBattleUnitCreationResult Failure(string error)
        {
            return new BFBattleUnitCreationResult(false, null, null, error);
        }
    }

    /// <summary>Encounter 批量创建结果。</summary>
    public sealed class BFBattleEncounterCreationResult
    {
        private BFBattleEncounterCreationResult(
            bool succeeded,
            IReadOnlyList<BFBattleUnitCreationResult> units,
            string error)
        {
            Succeeded = succeeded;
            Units = units;
            Error = error ?? string.Empty;
        }

        /// <summary>是否全部创建成功。</summary>
        public bool Succeeded { get; }

        /// <summary>成功时的单位创建结果；失败时为空集合。</summary>
        public IReadOnlyList<BFBattleUnitCreationResult> Units { get; }

        /// <summary>失败时的明确错误信息。</summary>
        public string Error { get; }

        internal static BFBattleEncounterCreationResult Success(
            IReadOnlyList<BFBattleUnitCreationResult> units)
        {
            return new BFBattleEncounterCreationResult(true, units, string.Empty);
        }

        internal static BFBattleEncounterCreationResult Failure(string error)
        {
            return new BFBattleEncounterCreationResult(
                false,
                new List<BFBattleUnitCreationResult>(),
                error);
        }
    }
}
