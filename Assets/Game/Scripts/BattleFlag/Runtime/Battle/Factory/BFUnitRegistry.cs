using System;
using System.Collections.Generic;
using BF.Game.Runtime.Battle.Units;

namespace BF.Game.Runtime.Battle.Factory
{
    /// <summary>
    /// 单个 BattleSession 内的 RuntimeId 到 Unity Runtime 索引。
    /// </summary>
    public sealed class BFUnitRegistry : IDisposable
    {
        private readonly Dictionary<string, UnitRuntime> _runtimes = new();
        private bool _isDisposed;

        /// <summary>创建绑定到指定战斗会话的注册表。</summary>
        public BFUnitRegistry(string battleId)
        {
            if (string.IsNullOrWhiteSpace(battleId))
                throw new ArgumentException("BattleId 不能为空。", nameof(battleId));

            BattleId = battleId;
        }

        /// <summary>注册表所属战斗身份。</summary>
        public string BattleId { get; }

        /// <summary>当前注册的单位数量。</summary>
        public int Count
        {
            get
            {
                EnsureNotDisposed();
                return _runtimes.Count;
            }
        }

        /// <summary>
        /// 注册一个已经完成绑定的 Runtime。
        /// </summary>
        public bool TryRegister(BFBattleUnitHandle handle, UnitRuntime runtime)
        {
            EnsureNotDisposed();
            if (!IsHandleInThisBattle(handle) || runtime == null)
                return false;
            if (!string.Equals(runtime.RuntimeId, handle.RuntimeId, StringComparison.Ordinal))
                return false;
            if (!string.Equals(runtime.BattleId, handle.BattleId, StringComparison.Ordinal))
                return false;

            return _runtimes.TryAdd(handle.RuntimeId, runtime);
        }

        /// <summary>
        /// 查询当前战斗内的 Runtime。
        /// </summary>
        public bool TryGetRuntime(BFBattleUnitHandle handle, out UnitRuntime runtime)
        {
            EnsureNotDisposed();
            if (!IsHandleInThisBattle(handle))
            {
                runtime = null;
                return false;
            }

            return _runtimes.TryGetValue(handle.RuntimeId, out runtime);
        }

        /// <summary>解除一个单位的注册关系。</summary>
        public bool TryUnregister(BFBattleUnitHandle handle)
        {
            EnsureNotDisposed();
            return IsHandleInThisBattle(handle) && _runtimes.Remove(handle.RuntimeId);
        }

        /// <summary>清理当前会话内全部注册关系。</summary>
        public void Clear()
        {
            EnsureNotDisposed();
            _runtimes.Clear();
        }

        /// <summary>幂等释放注册表持有的索引。</summary>
        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            _runtimes.Clear();
        }

        private bool IsHandleInThisBattle(BFBattleUnitHandle handle)
        {
            return handle != null && string.Equals(handle.BattleId, BattleId, StringComparison.Ordinal);
        }

        private void EnsureNotDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(BFUnitRegistry));
        }
    }
}
