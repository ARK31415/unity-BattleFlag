using System;
using System.Collections.Generic;
using BF.Game.Battle.Domain;
using BF.Game.Battle.Domain.Units;
using BF.Game.Battle.Rules.Units;
using BF.Game.Runtime.Battle.Data;
using BF.Game.Runtime.Battle.Managers;
using BF.Game.Runtime.Battle.Units;
using UnityEngine;
using DomainBattleSession = BF.Game.Battle.Domain.BFBattleSession;
using DomainSessionState = BF.Game.Battle.Domain.BFBattleSessionState;

namespace BF.Game.Runtime.Battle.Factory
{
    /// <summary>
    /// 战斗会话级完整单位组合工厂。
    ///
    /// 工厂统一协调规则状态创建、Runtime 创建、绑定、Registry 和棋盘占用，
    /// 不区分玩家、敌人、普通、精英或 Boss 工厂。
    /// </summary>
    public sealed class BFBattleUnitFactory : IBFBattleUnitFactory
    {
        private readonly DomainBattleSession _session;
        private readonly BFUnitRegistry _registry;
        private readonly BFUnitFactoryConfigSO _factoryConfig;
        private readonly BFBattleBoardManager _boardManager;
        private readonly Transform _unitParent;
        private readonly IBFUnitRuntimeProvider _runtimeProvider;
        private readonly BFUnitDefinitionResolver _definitionResolver = new();
        private readonly BFUnitStateFactory _stateFactory = new();
        private readonly BFUnitRuntimeBinder _runtimeBinder = new();
        private readonly List<CreatedUnit> _createdUnits = new();
        private bool _isDisposed;

        /// <summary>创建一个绑定到指定 BattleSession 的单位工厂。</summary>
        public BFBattleUnitFactory(
            DomainBattleSession session,
            BFUnitRegistry registry,
            BFUnitFactoryConfigSO factoryConfig,
            BFBattleBoardManager boardManager,
            Transform unitParent,
            IBFUnitRuntimeProvider runtimeProvider = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _factoryConfig = factoryConfig ?? throw new ArgumentNullException(nameof(factoryConfig));
            _boardManager = boardManager ?? throw new ArgumentNullException(nameof(boardManager));
            _unitParent = unitParent;
            _runtimeProvider = runtimeProvider ?? new BFUnityUnitRuntimeProvider();
        }

        /// <inheritdoc />
        public BFBattleUnitCreationResult Create(BFBattleUnitCreateRequest request)
        {
            EnsureNotDisposed();
            if (request == null) return BFBattleUnitCreationResult.Failure("Unit create request is missing.");
            if (!CanCreate(out var sessionError)) return BFBattleUnitCreationResult.Failure(sessionError);
            if (!ValidatePosition(request.GridPosition, out var positionError))
                return BFBattleUnitCreationResult.Failure(positionError);

            if (!_factoryConfig.TryGetPrefab(request.Definition, out var prefab, out var prefabError))
                return BFBattleUnitCreationResult.Failure(prefabError);

            var runtimeId = _session.CreateRuntimeId();
            var handle = new BFBattleUnitHandle(_session.Context.BattleId, runtimeId);
            BFUnitState state;
            UnitRuntime runtime = null;
            var registeredInContext = false;
            var registeredInRegistry = false;
            var occupied = false;

            try
            {
                var stateData = new BFUnitStateCreationData(
                    request.ProfileId,
                    request.Faction,
                    request.Role,
                    request.Tier,
                    request.UnitLevel,
                    request.Attributes,
                    request.GridPosition);
                state = _stateFactory.Create(runtimeId, stateData);

                if (!_session.Context.TryRegisterUnit(state))
                    return BFBattleUnitCreationResult.Failure($"RuntimeId registration conflict: {runtimeId}.");
                registeredInContext = true;

                var worldPosition = (Vector3)_boardManager.CellToWorld(
                    new Vector2Int(request.GridPosition.X, request.GridPosition.Y));
                runtime = _runtimeProvider.Create(prefab, worldPosition, _unitParent, out var runtimeError);
                if (runtime == null)
                    return RollbackFailure(handle, runtimeError, state, null, registeredInContext, false, false);

                runtime.name = CreateRuntimeObjectName(request, _createdUnits.Count + 1);
                if (!_runtimeBinder.TryBind(
                        handle,
                        state,
                        runtime,
                        request.UnityBinding,
                        request.DisplayName,
                        request.Definition,
                        out var bindingError))
                {
                    return RollbackFailure(handle, bindingError, state, runtime, registeredInContext, false, false);
                }

                if (!_registry.TryRegister(handle, runtime))
                    return RollbackFailure(handle, "Runtime Registry registration failed.", state, runtime, registeredInContext, false, false);
                registeredInRegistry = true;

                if (!_boardManager.TryOccupyCell(
                        new Vector2Int(request.GridPosition.X, request.GridPosition.Y),
                        runtimeId))
                {
                    return RollbackFailure(handle, "Board cell became occupied during unit creation.", state, runtime, registeredInContext, registeredInRegistry, false);
                }
                occupied = true;

                _createdUnits.Add(new CreatedUnit(handle, runtime));
                return BFBattleUnitCreationResult.Success(handle, runtime);
            }
            catch (Exception exception)
            {
                return RollbackFailure(
                    handle,
                    $"Unit creation failed: {exception.Message}",
                    registeredInContext ? _session.Context.TryGetUnit(runtimeId, out var stateValue) ? stateValue : null : null,
                    runtime,
                    registeredInContext,
                    registeredInRegistry,
                    occupied);
            }
        }

        /// <inheritdoc />
        public BFBattleEncounterCreationResult CreateEncounter(BFBattleEncounterSO encounter)
        {
            EnsureNotDisposed();
            if (encounter == null) return BFBattleEncounterCreationResult.Failure("Encounter is missing.");
            if (string.IsNullOrWhiteSpace(encounter.EncounterId))
                return BFBattleEncounterCreationResult.Failure("EncounterId is missing.");
            if (encounter.SpawnEntries == null)
                return BFBattleEncounterCreationResult.Failure("Encounter spawn entries are missing.");
            if (encounter.SpawnEntries.Count == 0)
                return BFBattleEncounterCreationResult.Failure("Encounter has no spawn entries.");
            if (!CanCreate(out var sessionError))
                return BFBattleEncounterCreationResult.Failure(sessionError);

            var requests = new List<BFBattleUnitCreateRequest>();
            var positions = new HashSet<BFGridPosition>();
            for (var index = 0; index < encounter.SpawnEntries.Count; index++)
            {
                var entry = encounter.SpawnEntries[index];
                if (entry == null)
                    return BFBattleEncounterCreationResult.Failure(
                        $"Encounter entry at index {index} is missing.");
                if (!entry.IsEnabled) continue;

                if (!_definitionResolver.TryResolve(entry, out var request, out var resolveError))
                    return BFBattleEncounterCreationResult.Failure(resolveError);
                if (!positions.Add(request.GridPosition))
                    return BFBattleEncounterCreationResult.Failure(
                        $"Encounter contains duplicate grid position {request.GridPosition.X},{request.GridPosition.Y}.");
                if (!ValidatePosition(request.GridPosition, out var positionError))
                    return BFBattleEncounterCreationResult.Failure(positionError);

                requests.Add(request);
            }

            if (requests.Count == 0)
                return BFBattleEncounterCreationResult.Failure("Encounter has no enabled spawn entries.");

            var results = new List<BFBattleUnitCreationResult>(requests.Count);
            var createdStartIndex = _createdUnits.Count;
            for (var index = 0; index < requests.Count; index++)
            {
                var result = Create(requests[index]);
                if (result.Succeeded)
                {
                    results.Add(result);
                    continue;
                }

                RollbackCreatedUnits(createdStartIndex);
                return BFBattleEncounterCreationResult.Failure(result.Error);
            }

            return BFBattleEncounterCreationResult.Success(results);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_isDisposed) return;

            RollbackCreatedUnits();
            _isDisposed = true;
        }

        private bool CanCreate(out string error)
        {
            if (_session.State != DomainSessionState.Created &&
                _session.State != DomainSessionState.Running)
            {
                error = $"Cannot create a unit in session state {_session.State}.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool ValidatePosition(BFGridPosition position, out string error)
        {
            var cell = new Vector2Int(position.X, position.Y);
            if (!_boardManager.IsCellInBounds(cell))
            {
                error = $"Grid position {position.X},{position.Y} is outside the battle board.";
                return false;
            }

            if (_boardManager.IsCellOccupied(cell))
            {
                error = $"Grid position {position.X},{position.Y} is already occupied.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// 生成 Unity 层级中的可读对象名称。
        ///
        /// GameObject.name 只用于编辑器层级和调试显示，不承载 ProfileId、BattleId 或 RuntimeId。
        /// 规则身份仍由 BFUnitState 与 BFBattleUnitHandle 保存。
        /// </summary>
        private static string CreateRuntimeObjectName(BFBattleUnitCreateRequest request, int sequence)
        {
            return $"Unit_{request.Faction}_{request.Role}_{sequence:D2}";
        }

        private BFBattleUnitCreationResult RollbackFailure(
            BFBattleUnitHandle handle,
            string error,
            BFUnitState state,
            UnitRuntime runtime,
            bool registeredInContext,
            bool registeredInRegistry,
            bool occupied)
        {
            if (occupied && state != null)
                _boardManager.ReleaseCell(new Vector2Int(state.GridPosition.X, state.GridPosition.Y), handle.RuntimeId);
            if (registeredInRegistry)
                _registry.TryUnregister(handle);
            if (runtime != null)
            {
                _runtimeBinder.Unbind(runtime);
                _runtimeProvider.Release(runtime);
            }
            if (registeredInContext)
                _session.Context.TryRemoveUnit(handle.RuntimeId);

            return BFBattleUnitCreationResult.Failure(error);
        }

        private void RollbackCreatedUnits(int startIndex = 0)
        {
            for (var index = _createdUnits.Count - 1; index >= startIndex; index--)
            {
                var created = _createdUnits[index];
                if (_session.Context.TryGetUnit(created.Handle.RuntimeId, out var state))
                {
                    _boardManager.ReleaseCell(
                        new Vector2Int(state.GridPosition.X, state.GridPosition.Y),
                        created.Handle.RuntimeId);
                    _session.Context.TryRemoveUnit(created.Handle.RuntimeId);
                }

                _registry.TryUnregister(created.Handle);
                _runtimeBinder.Unbind(created.Runtime);
                _runtimeProvider.Release(created.Runtime);
            }

            if (startIndex == 0)
            {
                _createdUnits.Clear();
            }
            else if (startIndex < _createdUnits.Count)
            {
                _createdUnits.RemoveRange(startIndex, _createdUnits.Count - startIndex);
            }
        }

        private void EnsureNotDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(BFBattleUnitFactory));
        }

        private readonly struct CreatedUnit
        {
            public CreatedUnit(BFBattleUnitHandle handle, UnitRuntime runtime)
            {
                Handle = handle;
                Runtime = runtime;
            }

            public BFBattleUnitHandle Handle { get; }
            public UnitRuntime Runtime { get; }
        }
    }
}
