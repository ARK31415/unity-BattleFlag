using System;
using System.Collections.Generic;
using BF.Game.Runtime.Battle.Units;

namespace BF.Game.Runtime.Battle.Factory
{
    /// <summary>
    /// 单个 BattleSession 内的 RuntimeId 到 Unity Runtime 索引。
    /// </summary>
    public sealed class BFUnitRegistry : IBFBattleRuntimeLookup, IDisposable
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

        /// <summary>当前会话已注册的 Runtime 集合，供适配层查询和协调器遍历。</summary>
        public IReadOnlyCollection<UnitRuntime> Runtimes
        {
            get
            {
                EnsureNotDisposed();
                return _runtimes.Values;
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

            if (!_runtimes.TryAdd(handle.RuntimeId, runtime))
                return false;

            runtime.Disabled += HandleRuntimeDisabled;
            return true;
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

            if (!_runtimes.TryGetValue(handle.RuntimeId, out runtime))
                return false;

            if (runtime == null || !runtime.gameObject.activeInHierarchy)
            {
                RemoveRuntime(handle.RuntimeId, runtime);
                runtime = null;
                return false;
            }

            return true;
        }

        /// <summary>按 RuntimeId 查询当前会话内的 Runtime。</summary>
        public bool TryGetRuntime(string runtimeId, out UnitRuntime runtime)
        {
            EnsureNotDisposed();
            if (string.IsNullOrWhiteSpace(runtimeId))
            {
                runtime = null;
                return false;
            }

            if (!_runtimes.TryGetValue(runtimeId, out runtime))
                return false;

            if (runtime == null || !runtime.gameObject.activeInHierarchy)
            {
                RemoveRuntime(runtimeId, runtime);
                runtime = null;
                return false;
            }

            return true;
        }

        /// <summary>解除一个单位的注册关系。</summary>
        public bool TryUnregister(BFBattleUnitHandle handle)
        {
            EnsureNotDisposed();
            if (!IsHandleInThisBattle(handle) ||
                !_runtimes.TryGetValue(handle.RuntimeId, out var runtime))
                return false;

            RemoveRuntime(handle.RuntimeId, runtime);
            return true;
        }

        /// <summary>清理当前会话内全部注册关系。</summary>
        public void Clear()
        {
            EnsureNotDisposed();
            foreach (var runtime in _runtimes.Values)
            {
                if (runtime != null)
                    runtime.Disabled -= HandleRuntimeDisabled;
            }

            _runtimes.Clear();
        }

        /// <summary>幂等释放注册表持有的索引。</summary>
        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            foreach (var runtime in _runtimes.Values)
            {
                if (runtime != null)
                    runtime.Disabled -= HandleRuntimeDisabled;
            }

            _runtimes.Clear();
        }

        private bool IsHandleInThisBattle(BFBattleUnitHandle handle)
        {
            return handle != null && string.Equals(handle.BattleId, BattleId, StringComparison.Ordinal);
        }

        private void HandleRuntimeDisabled(UnitRuntime runtime)
        {
            if (runtime == null || string.IsNullOrWhiteSpace(runtime.RuntimeId))
                return;

            if (_runtimes.TryGetValue(runtime.RuntimeId, out var registered) &&
                ReferenceEquals(registered, runtime))
            {
                RemoveRuntime(runtime.RuntimeId, runtime);
            }
        }

        private void RemoveRuntime(string runtimeId, UnitRuntime runtime)
        {
            if (runtime != null)
                runtime.Disabled -= HandleRuntimeDisabled;

            _runtimes.Remove(runtimeId);
        }

        private void EnsureNotDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(BFUnitRegistry));
        }
    }
}
