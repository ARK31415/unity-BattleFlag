using System;
using System.Collections.Generic;
using BF.Game.Battle.Domain.Events;
using BF.Game.Battle.Domain.Units;

namespace BF.Game.Battle.Domain
{
    /// <summary>
    /// 一次战斗的纯规则状态容器。
    ///
    /// 职责边界：
    /// - 保存 BattleId、单位规则状态、阶段、回合、轮次和战斗结果。
    /// - 通过 RuntimeId 管理单位，不持有 UnitRuntime 或其他 Unity 对象。
    /// - 对外提供只读查询，规则层内部通过受控入口修改状态。
    /// </summary>
    public sealed class BFBattleContext
    {
        private readonly Dictionary<string, BFUnitState> _units = new();
        private bool _isDisposed;

        /// <summary>创建一个新的纯规则战斗上下文。</summary>
        /// <param name="battleId">本场战斗的唯一身份。</param>
        public BFBattleContext(string battleId)
        {
            if (string.IsNullOrWhiteSpace(battleId))
                throw new ArgumentException("BattleId 不能为空。", nameof(battleId));

            BattleId = battleId;
            CurrentPhase = BFBattlePhase.None;
        }

        /// <summary>本场战斗的唯一身份。</summary>
        public string BattleId { get; }

        /// <summary>当前战斗阶段。</summary>
        public BFBattlePhase CurrentPhase { get; private set; }

        /// <summary>当前回合编号。</summary>
        public int TurnNumber { get; private set; }

        /// <summary>当前轮次编号。</summary>
        public int RoundNumber { get; private set; }

        /// <summary>当前战斗结果；战斗未完成时为空。</summary>
        public BattleResult Result { get; private set; }

        /// <summary>
        /// 只读单位集合视图。
        /// 集合本身不能通过该属性增删，单位状态修改仍由规则层内部入口负责。
        /// </summary>
        public IReadOnlyDictionary<string, BFUnitState> Units
        {
            get
            {
                EnsureNotDisposed();
                return _units;
            }
        }

        /// <summary>按 RuntimeId 查询单位规则状态。</summary>
        /// <param name="runtimeId">当前战斗内的运行时单位身份。</param>
        /// <param name="unit">查询到的单位规则状态。</param>
        /// <returns>true 表示查询成功，false 表示不存在。</returns>
        public bool TryGetUnit(string runtimeId, out BFUnitState unit)
        {
            EnsureNotDisposed();
            if (string.IsNullOrWhiteSpace(runtimeId))
            {
                unit = null;
                return false;
            }

            return _units.TryGetValue(runtimeId, out unit);
        }

        /// <summary>注册一个单位规则状态。</summary>
        /// <param name="unit">要注册的单位规则状态。</param>
        /// <returns>true 表示注册成功，false 表示为空或 RuntimeId 重复。</returns>
        public bool TryRegisterUnit(BFUnitState unit)
        {
            EnsureNotDisposed();
            if (unit == null) return false;
            return _units.TryAdd(unit.RuntimeId, unit);
        }

        /// <summary>按 RuntimeId 移除单位规则状态。</summary>
        /// <param name="runtimeId">要移除的运行时单位身份。</param>
        /// <returns>true 表示移除成功。</returns>
        public bool TryRemoveUnit(string runtimeId)
        {
            EnsureNotDisposed();
            if (string.IsNullOrWhiteSpace(runtimeId)) return false;
            return _units.Remove(runtimeId);
        }

        /// <summary>更新当前阶段。</summary>
        internal void SetCurrentPhase(BFBattlePhase phase)
        {
            EnsureNotDisposed();
            CurrentPhase = phase;
        }

        /// <summary>更新当前回合编号。</summary>
        internal void SetTurnNumber(int turnNumber)
        {
            EnsureNotDisposed();
            ValidateNonNegative(turnNumber, nameof(turnNumber));
            TurnNumber = turnNumber;
        }

        /// <summary>更新当前轮次编号。</summary>
        internal void SetRoundNumber(int roundNumber)
        {
            EnsureNotDisposed();
            ValidateNonNegative(roundNumber, nameof(roundNumber));
            RoundNumber = roundNumber;
        }

        /// <summary>写入已经完成计算的战斗结果。</summary>
        internal void SetResult(BattleResult result)
        {
            EnsureNotDisposed();
            Result = result ?? throw new ArgumentNullException(nameof(result));
        }

        /// <summary>释放 Context 持有的规则状态。</summary>
        internal void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            _units.Clear();
            Result = null;
        }

        private static void ValidateNonNegative(int value, string parameterName)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(parameterName, value, "编号不能为负数。");
        }

        private void EnsureNotDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(BFBattleContext));
        }
    }
}
