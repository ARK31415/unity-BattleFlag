using System;
using System.Collections;
using System.Collections.Generic;
using BF.Game.Battle.Domain;
using BF.Game.Battle.Domain.Units;
using BF.Game.Battle.Domain.Events;
using BF.Game.Battle.Rules.Battle;
using BF.Game.Battle.Rules.Units;
using BF.Game.Runtime.Battle.Events;
using BF.Game.Runtime.Battle.Presentation;
using BF.Game.Runtime.Battle.Units;
using UnityEngine;
using DomainBattleResult = BF.Game.Battle.Domain.BattleResult;
using DomainBattleSession = BF.Game.Battle.Domain.BFBattleSession;
using DomainSessionState = BF.Game.Battle.Domain.BFBattleSessionState;

namespace BF.Game.Runtime.Battle.Managers
{
    /// <summary>
    /// 单位管理器。维护单位列表、玩家选择、移动/攻击命令、敌方 AI 行动与胜负判定。
    /// </summary>
    public class BFBattleUnitManager : MonoBehaviour
    {
        [Header("Event Channels")]
        [SerializeField] private BFUnitEventSO _unitEventChannel;

        [Header("Dependencies")]
        [SerializeField] private BFBattleBoardManager _boardManager;
        [SerializeField] private BFBattleTurnManager _turnManager;
        [SerializeField] private BFBattleResolutionManager _resolutionManager;

        [Header("Movement")]
        [SerializeField] private float _secondsPerMoveCell = 0.2f;

        private Coroutine _activeMoveCoroutine;
        private Coroutine _enemyTurnCoroutine;
        private UnitRuntime _activeMovingUnit;
        private bool _isActionLocked;
        private DomainBattleSession _battleSession;
        private BFUnitStateRules _unitStateRules;
        private BFBattleProgressRules _battleProgressRules;
        private bool _boardSyncFaulted;

        /// <summary>战场上所有单位。</summary>
        public List<UnitRuntime> AllUnits { get; private set; } = new();

        /// <summary>当前选中的玩家单位。</summary>
        public UnitRuntime SelectedUnit { get; private set; }

        /// <summary>战斗结果。</summary>
        public BattleResult Result { get; private set; }

        /// <summary>是否有移动或行动表现正在执行。</summary>
        public bool IsActionLocked => _isActionLocked;

        /// <summary>
        /// 棋盘占用与规则位置失去一致性时进入故障状态。
        /// 故障状态会阻断新的战斗动作，直到会话结束或完成显式恢复。
        /// </summary>
        public bool IsBoardSyncFaulted => _boardSyncFaulted;

        /// <summary>
        /// 指示单位管理器是否已经绑定战斗会话。
        /// </summary>
        public bool HasBattleSession => _battleSession != null;

        /// <summary>当前单位被选中时触发的兼容性回调。</summary>
        public event Action<UnitRuntime> OnUnitSelected;

        /// <summary>当前单位被取消选中时触发的兼容性回调。</summary>
        public event Action<UnitRuntime> OnUnitDeselected;

        /// <summary>单位完成移动表现时触发的兼容性回调。</summary>
        public event Action<UnitRuntime> OnUnitMoveCompleted;

        /// <summary>战斗结果确定时触发的旧 C# 回调；新代码应优先订阅战斗完成领域事件。</summary>
        public event Action<BattleResult> OnBattleEnded;

        /// <summary>
        /// 将单位管理器绑定到一个战斗会话。
        ///
        /// 同一个管理器可以重复绑定同一会话，但不能改绑到其他会话。
        /// </summary>
        /// <param name="session">要绑定的战斗会话。</param>
        /// <exception cref="InvalidOperationException">当管理器已经绑定其他会话时抛出。</exception>
        public void SetBattleSession(DomainBattleSession session)
        {
            if (_battleSession != null && _battleSession != session)
                throw new InvalidOperationException("BFBattleUnitManager is already attached to another battle session.");

            _battleSession = session;
            _unitStateRules = session == null ? null : new BFUnitStateRules(session.Context);
            _battleProgressRules = session == null ? null : new BFBattleProgressRules(session);
            if (session == null)
            {
                _boardSyncFaulted = false;
            }
        }

        /// <summary>
        /// 测试辅助：注入棋盘管理器引用。
        /// 正式场景由 Inspector 序列化字段提供，运行期不通过全局查找建立棋盘依赖。
        /// </summary>
        internal void SetBoardForTest(BFBattleBoardManager boardManager)
        {
            _boardManager = boardManager;
        }

        /// <summary>
        /// 在外部完成棋盘修复后重新验证规则位置与棋盘占用。
        /// 验证通过才解除故障状态；该方法不会修改规则状态，也不会自动重建棋盘。
        /// </summary>
        public bool TryRecoverBoardSync()
        {
            if (!_boardSyncFaulted) return true;
            if (_boardManager == null) return false;

            var expectedOccupants = new Dictionary<Vector2Int, string>();
            foreach (var unit in AllUnits)
            {
                if (unit == null || !unit.IsRuleBound) return false;
                if (!unit.RuleState.IsAlive) continue;

                var cell = new Vector2Int(
                    unit.RuleState.GridPosition.X,
                    unit.RuleState.GridPosition.Y);
                if (string.IsNullOrWhiteSpace(unit.RuntimeId) ||
                    !expectedOccupants.TryAdd(cell, unit.RuntimeId))
                    return false;
            }

            if (!_boardManager.HasExactUnitOccupancy(expectedOccupants))
                return false;

            _boardSyncFaulted = false;
            return true;
        }

        // 组件禁用时停止正在进行的协程，并把移动中的单位恢复到当前格子世界坐标。
        // 攻击命中前的禁用只清理未完成攻击上下文，不消耗 AP、不造成伤害、不发布成功事实；
        // 已经完成的规则结算不受影响，也不会回滚。
        private void OnDisable()
        {
            CleanupInterruptedActions();
        }

        private void OnDestroy()
        {
            foreach (var unit in AllUnits)
            {
                if (unit != null)
                    unit.Disabled -= HandleUnitDisabled;
            }
        }

        /// <summary>
        /// 清理被中断的移动与攻击表现，恢复规则行动状态。
        ///
        /// 命中前中断只清理 Combat 上下文与规则 Attack 状态，不消耗 AP、不造成伤害；
        /// 命中后已经离开 Attack 状态的结算不会被回滚。该方法同时用于组件禁用回调
        /// 与测试验证，属于适配层清理责任（Spec 3.2 6.6）。
        /// </summary>
        internal void CleanupInterruptedActions()
        {
            if (_activeMoveCoroutine != null)
            {
                StopCoroutine(_activeMoveCoroutine);
                _activeMoveCoroutine = null;
            }

            if (_enemyTurnCoroutine != null)
            {
                StopCoroutine(_enemyTurnCoroutine);
                _enemyTurnCoroutine = null;
            }

            RestoreInterruptedMove();

            var sessionAlive = _battleSession != null &&
                               _battleSession.State != DomainSessionState.Disposed;
            foreach (var unit in AllUnits)
            {
                if (unit == null) continue;

                unit.Combat.ClearQueuedAttack();
                _resolutionManager?.ClearPendingAttack(unit);

                // 仍在规则 Attack 状态的单位属于命中前中断：恢复为可行动状态。
                // 命中后结算已经离开 Attack 状态，这里不会回滚已提交的 AP、伤害或死亡结果。
                if (sessionAlive && _unitStateRules != null && unit.IsRuleBound &&
                    unit.RuleState.ActionState == BFUnit_ActionState.Attack)
                {
                    _unitStateRules.TryChangeActionState(unit.RuntimeId, BFUnit_ActionState.Idle);
                    unit.RefreshRuleStateProjection();
                }
            }

            _isActionLocked = false;
        }

        /// <summary>
        /// 将场景中的单位根注册到战斗单位列表。
        /// 未绑定规则状态的单位不能注册到正式战斗。
        /// </summary>
        /// <param name="unit">已经完成 UnitRuntime 初始化并绑定规则状态的单位根。</param>
        public void RegisterUnit(UnitRuntime unit)
        {
            if (unit == null || AllUnits.Contains(unit)) return;
            if (_battleSession == null || _unitStateRules == null)
            {
                Debug.LogWarning($"[BFBattleUnitManager] 拒绝注册未绑定 BattleSession 的单位：{unit.name}");
                return;
            }
            if (!unit.IsRuleBound)
            {
                Debug.LogWarning($"[BFBattleUnitManager] 拒绝注册未绑定规则状态的单位：{unit.name}");
                return;
            }

            // UnitRuntime 只作为根对象入表；身份和阵营等业务信息从 Identity 子组件读取。
            AllUnits.Add(unit);
            unit.Disabled += HandleUnitDisabled;
            unit.GetComponent<BFUnitAnimationPresenter>()?.ApplyInitialFacing();
            Debug.Log($"[BFBattleUnitManager] Registered: {unit.Identity.DisplayName} ({unit.Identity.Faction})");
        }

        /// <summary>
        /// 获取指定阵营中仍可参与战斗的单位根列表。
        /// </summary>
        /// <param name="faction">要筛选的阵营。</param>
        /// <returns>阵营匹配且 Stats.IsAlive 为 true 的单位列表。</returns>
        public List<UnitRuntime> GetAliveUnitsByFaction(UnitFaction faction)
        {
            var result = new List<UnitRuntime>();
            foreach (var unit in AllUnits)
            {
                if (unit != null && unit.Identity.Faction == faction && unit.Stats.IsAlive)
                {
                    result.Add(unit);
                }
            }

            return result;
        }

        /// <summary>
        /// 在新回合开始时重置所有单位的回合资源。
        /// 正式战斗单位必须绑定规则状态；未绑定单位不能参与正式回合流程。
        /// </summary>
        public void ResetAllUnitsForNewTurn()
        {
            foreach (var unit in AllUnits)
            {
                if (unit == null || !unit.IsRuleBound) continue;

                if (_unitStateRules.TryResetTurnResources(unit.RuntimeId))
                {
                    unit.RefreshRuleStateProjection();
                }
            }
        }

        /// <summary>
        /// 尝试选中一个玩家单位。
        /// </summary>
        /// <param name="unit">玩家点击或输入命中的单位根。</param>
        /// <returns>true 表示单位已成为当前选中单位。</returns>
        public bool TrySelectUnit(UnitRuntime unit)
        {
            if (_isActionLocked || _boardSyncFaulted) return false;
            // 正式战斗只接受绑定规则状态且仍存活的单位。
            if (unit == null || !unit.IsRuleBound || !unit.Stats.IsAlive) return false;
            if (_turnManager != null && _turnManager.CurrentPhase != BattlePhase.PlayerTurn) return false;

            DeselectUnit();
            SelectedUnit = unit;
            OnUnitSelected?.Invoke(unit);

            _unitEventChannel?.Raise(new BFUnitEventData
            {
                UnitId = unit.RuntimeId,
                EventType = "Selected"
            });

            Debug.Log($"[BFBattleUnitManager] Selected: {unit.Identity.DisplayName}, AP: {unit.Stats.RemainingActionPoints}");
            return true;
        }

        /// <summary>
        /// 取消当前选中单位。
        ///
        /// 动作锁定期间不会取消选择，避免移动或攻击表现中途丢失上下文。
        /// </summary>
        public void DeselectUnit()
        {
            if (_isActionLocked) return;
            if (SelectedUnit == null) return;

            var old = SelectedUnit;
            SelectedUnit = null;
            OnUnitDeselected?.Invoke(old);
            _unitEventChannel?.Raise(new BFUnitEventData
            {
                UnitId = old.RuntimeId,
                EventType = "Deselected"
            });
        }

        /// <summary>
        /// 尝试让当前选中单位移动到目标格。
        /// </summary>
        /// <param name="targetCell">目标棋盘格坐标。</param>
        /// <returns>true 表示移动协程已启动。</returns>
        public bool TryMoveUnit(Vector2Int targetCell)
        {
            if (_isActionLocked || _boardSyncFaulted) return false;
            if (SelectedUnit == null || !SelectedUnit.IsRuleBound || SelectedUnit.Stats.HasActed) return false;
            if (SelectedUnit.Identity.Faction != UnitFaction.Player) return false;
            if (_turnManager != null && _turnManager.CurrentPhase != BattlePhase.PlayerTurn) return false;
            if (!TryGetMovePath(SelectedUnit, targetCell, out var path)) return false;

            _activeMoveCoroutine = StartCoroutine(MoveUnitAlongPathCoroutine(
                SelectedUnit,
                path,
                refreshPlayerLegalActions: true,
                clearSelectionWhenActed: true,
                manageActionLock: true));

            return true;
        }

        /// <summary>
        /// 尝试让当前选中单位攻击目标。
        /// </summary>
        /// <param name="target">攻击目标单位根。</param>
        /// <returns>true 表示攻击表现和待结算上下文已启动。</returns>
        public bool TryAttack(UnitRuntime target)
        {
            if (_isActionLocked || _boardSyncFaulted) return false;
            if (SelectedUnit == null || !SelectedUnit.IsRuleBound || SelectedUnit.Stats.HasActed) return false;
            if (SelectedUnit.Identity.Faction != UnitFaction.Player) return false;
            if (target == null || !target.IsRuleBound || !target.Stats.IsAlive || target.Identity.Faction == SelectedUnit.Identity.Faction) return false;
            if (_turnManager != null && _turnManager.CurrentPhase != BattlePhase.PlayerTurn) return false;

            if (!TryStartAttack(SelectedUnit, target, out var attackCost))
            {
                return false;
            }

            _isActionLocked = true;
            RaiseUnitActionEvent(SelectedUnit, "Attacked", target.RuntimeId, attackCost);
            StartCoroutine(WaitForAttackToFinishCoroutine(SelectedUnit));
            Debug.Log($"[BFBattleUnitManager] {SelectedUnit.Identity.DisplayName} 发起攻击 -> {target.Identity.DisplayName}, AP 剩余: {SelectedUnit.Stats.RemainingActionPoints}");
            return true;
        }

        /// <summary>
        /// 让当前选中单位执行单位级等待。
        /// 该入口只消耗该单位本回合剩余 AP，并刷新玩家合法行动状态，不等同于结束整个玩家回合。
        /// </summary>
        /// <returns>true 表示等待命令已生效。</returns>
        public bool TryWaitSelectedUnit()
        {
            if (_isActionLocked || _boardSyncFaulted) return false;
            if (SelectedUnit == null || !SelectedUnit.IsRuleBound || SelectedUnit.Stats.HasActed) return false;
            if (SelectedUnit.Identity.Faction != UnitFaction.Player) return false;
            if (_turnManager != null && _turnManager.CurrentPhase != BattlePhase.PlayerTurn) return false;

            var waitedUnit = SelectedUnit;

            // Wait 作为规则命令一次性结算剩余 AP；AP 为 0 或规则拒绝时返回明确失败，不修改任何状态。
            var waitResult = _unitStateRules.TryWait(new WaitRequest(waitedUnit.RuntimeId));
            if (!waitResult.Succeeded)
            {
                Debug.LogWarning($"[BFBattleUnitManager] 规则拒绝等待命令：{waitResult.FailureReason}");
                return false;
            }

            waitedUnit.RefreshRuleStateProjection();

            // 等待事实在规则提交完成后发布，由 SO 适配器单向转发。
            _battleSession.Publish(new BFUnitWaitedEvent(
                _battleSession.Context.BattleId,
                waitedUnit.RuntimeId,
                _battleSession.Context.TurnNumber));
            DeselectUnitIgnoringLock();
            _turnManager?.RefreshPlayerLegalActions();
            Debug.Log($"[BFBattleUnitManager] {waitedUnit.Identity.DisplayName} 等待并结束本单位行动。");
            return true;
        }

        /// <summary>
        /// 获取当前选中单位在剩余 AP 内可到达的格子。
        /// </summary>
        /// <returns>可移动目标格列表；无选中单位或棋盘缺失时返回空列表。</returns>
        public List<Vector2Int> GetReachableCellsForSelected()
        {
            if (SelectedUnit == null || !SelectedUnit.IsRuleBound || _boardManager == null)
                return new List<Vector2Int>();

            return _boardManager.GetReachableCells(
                SelectedUnit.Grid.GridPosition,
                SelectedUnit.RuleState.Attributes.RemainingActionPoints,
                SelectedUnit.RuntimeId);
        }

        /// <summary>
        /// 获取当前选中单位可以攻击的敌方目标。
        /// </summary>
        /// <returns>处于攻击范围内且仍存活的敌方单位根列表。</returns>
        public List<UnitRuntime> GetAttackableTargets()
        {
            var targets = new List<UnitRuntime>();
            if (SelectedUnit == null || !SelectedUnit.IsRuleBound) return targets;

            var attackRange = SelectedUnit.RuleState.Attributes.EffectiveAttackRange;
            foreach (var unit in AllUnits)
            {
                if (unit == null || !unit.IsRuleBound || !unit.Stats.IsAlive || unit == SelectedUnit || unit.Identity.Faction == SelectedUnit.Identity.Faction)
                    continue;

                int distance = GetManhattanDistance(unit.Grid.GridPosition, SelectedUnit.Grid.GridPosition);
                if (distance <= attackRange)
                {
                    targets.Add(unit);
                }
            }

            return targets;
        }

        /// <summary>
        /// 判断玩家阵营是否仍有移动或攻击可执行。
        /// </summary>
        /// <returns>true 表示至少一个玩家单位还有合法行动。</returns>
        public bool PlayerHasLegalAction()
        {
            var players = GetAliveUnitsByFaction(UnitFaction.Player);
            if (players.Count == 0) return false;

            var enemies = GetAliveUnitsByFaction(UnitFaction.Enemy);
            if (enemies.Count == 0) return false;

            foreach (var unit in players)
            {
                if (!unit.IsRuleBound) continue;
                var ruleAttributes = unit.RuleState.Attributes;
                if (ruleAttributes.RemainingActionPoints <= 0) continue;

                var reachable = _boardManager.GetReachableCells(
                    unit.Grid.GridPosition,
                    ruleAttributes.RemainingActionPoints,
                    unit.RuntimeId);
                if (reachable.Count > 0) return true;

                if (ruleAttributes.RemainingActionPoints < ruleAttributes.EffectiveAttackCost) continue;
                foreach (var enemy in enemies)
                {
                    int distance = GetManhattanDistance(enemy.Grid.GridPosition, unit.Grid.GridPosition);
                    if (distance <= ruleAttributes.EffectiveAttackRange) return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 检查双方存活单位并在一方全灭时产生战斗结果。
        /// </summary>
        public void CheckBattleEndCondition()
        {
            if (Result != null && Result.HasResult) return;
            if (_battleSession == null || _battleProgressRules == null)
            {
                Debug.LogWarning("[BFBattleUnitManager] Cannot evaluate battle end without a BattleSession.");
                return;
            }

            bool playerAlive = GetAliveUnitsByFaction(UnitFaction.Player).Count > 0;
            bool enemyAlive = GetAliveUnitsByFaction(UnitFaction.Enemy).Count > 0;

            if (!playerAlive)
            {
                Result = BattleResult.Defeat(
                    _battleSession.Context.BattleId,
                    _turnManager != null ? _turnManager.TurnNumber : 0);
                _turnManager?.TransitionToResolution();
                CompleteBattleSession();
                OnBattleEnded?.Invoke(Result);
            }
            else if (!enemyAlive)
            {
                Result = BattleResult.Victory(
                    _battleSession.Context.BattleId,
                    _turnManager != null ? _turnManager.TurnNumber : 0);
                _turnManager?.TransitionToResolution();
                CompleteBattleSession();
                OnBattleEnded?.Invoke(Result);
            }
        }

        /// <summary>
        /// 启动敌方回合 AI 表现协程。
        ///
        /// 若已有敌方回合或动作锁正在进行，本次调用会被忽略。
        /// </summary>
        public void ExecuteEnemyTurn()
        {
            if (_boardSyncFaulted || _enemyTurnCoroutine != null || _isActionLocked) return;
            _enemyTurnCoroutine = StartCoroutine(ExecuteEnemyTurnCoroutine());
        }

        /// <summary>
        /// 处理结算层返回的攻击结果，发布攻击结算与单位击败领域事实，并收尾攻击生命周期。
        ///
        /// 领域事实统一由 BattleSession 发布；SO 事件由适配层单向转发。
        /// </summary>
        /// <param name="result">结算层生成的攻击结果。</param>
        public void HandleAttackResolved(BF.Game.Runtime.Battle.Commands.BFAttackResolveResult result)
        {
            if (!result.Succeeded || result.Attacker == null || result.Target == null) return;

            result.Attacker.Combat.ClearQueuedAttack();
            _unitStateRules.TryChangeActionState(
                result.Attacker.RuntimeId,
                BFUnit_ActionState.Idle);
            result.Attacker.RefreshRuleStateProjection();

            if (result.Attacker.Stats.IsAlive)
            {
                result.Attacker.StateMachine.ChangeState(result.Attacker.StateMachine.IdleState);
            }

            if (SelectedUnit != null && SelectedUnit.Stats.HasActed)
            {
                DeselectUnitIgnoringLock();
            }

            _isActionLocked = _enemyTurnCoroutine != null;
            _turnManager?.RefreshPlayerLegalActions();

            _battleSession.Publish(new BFAttackResolvedEvent(
                _battleSession.Context.BattleId,
                result.Attacker.RuntimeId,
                result.Target.RuntimeId,
                result.FinalDamage,
                result.TargetRemainingHp,
                result.TargetWasKilled,
                _battleSession.Context.TurnNumber));

            if (result.TargetWasKilled)
            {
                _battleSession.Publish(new BFUnitDefeatedEvent(
                    _battleSession.Context.BattleId,
                    result.Target.RuntimeId,
                    ToDomainFaction(result.Target.Identity.Faction),
                    result.Attacker.RuntimeId,
                    _battleSession.Context.TurnNumber));
            }

            Debug.Log($"[BFBattleUnitManager] 攻击结算完成：{result.Attacker.Identity.DisplayName} -> {result.Target.Identity.DisplayName}, 伤害 {result.FinalDamage}, 目标剩余 HP {result.TargetRemainingHp}");
            CheckBattleEndCondition();
        }

        /// <summary>
        /// 收尾一次未产生有效结果的攻击。
        /// 规则拒绝、目标在命中帧前死亡、超时或对象禁用时，不发布攻击成功事实，
        /// 但必须释放 Combat、表现状态和输入动作锁；攻击开始阶段未消耗 AP，因此无需回滚资源。
        /// </summary>
        internal void HandleAttackResolutionFailed(UnitRuntime attacker)
        {
            if (attacker == null) return;

            attacker.Combat.ClearQueuedAttack();
            if (_unitStateRules.TryChangeActionState(
                    attacker.RuntimeId,
                    BFUnit_ActionState.Idle))
            {
                attacker.RefreshRuleStateProjection();
            }

            if (attacker.Stats.IsAlive)
            {
                attacker.StateMachine.ChangeState(attacker.StateMachine.IdleState);
            }

            _isActionLocked = _enemyTurnCoroutine != null;
            _turnManager?.RefreshPlayerLegalActions();
        }

        private void CompleteBattleSession()
        {
            if (_battleSession == null || _battleSession.State != DomainSessionState.Running)
                return;

            var domainResult = Result.IsPlayerVictory
                ? DomainBattleResult.Victory(Result.BattleId, Result.TotalTurns)
                : DomainBattleResult.Defeat(Result.BattleId, Result.TotalTurns);

            if (_battleProgressRules != null)
            {
                _battleProgressRules.CompleteBattle(domainResult);
            }
        }

        private static BFUnitFaction ToDomainFaction(UnitFaction faction)
        {
            return faction switch
            {
                UnitFaction.Player => BFUnitFaction.Player,
                UnitFaction.Enemy => BFUnitFaction.Enemy,
                _ => BFUnitFaction.None
            };
        }

        private IEnumerator ExecuteEnemyTurnCoroutine()
        {
            _isActionLocked = true;

            var enemies = GetAliveUnitsByFaction(UnitFaction.Enemy);
            var players = GetAliveUnitsByFaction(UnitFaction.Player);

            if (enemies.Count == 0 || players.Count == 0)
            {
                CheckBattleEndCondition();
                FinishEnemyTurn();
                yield break;
            }

            foreach (var enemy in enemies)
            {
                if (_boardSyncFaulted) break;
                if (enemy == null || !enemy.Stats.IsAlive) continue;

                var nearest = FindNearestPlayer(enemy, players);
                if (nearest == null) break;

                if (TryStartAttack(enemy, nearest, out _))
                {
                    yield return WaitForAttackToFinishCoroutine(enemy);
                    if (_boardSyncFaulted) break;
                    continue;
                }

                var reachable = _boardManager.GetReachableCells(
                    enemy.Grid.GridPosition,
                    enemy.Stats.RemainingActionPoints,
                    enemy.RuntimeId);
                if (reachable.Count > 0)
                {
                    var best = FindBestReachableCell(reachable, nearest.Grid.GridPosition);
                    var path = _boardManager.FindPath(enemy.Grid.GridPosition, best, enemy.RuntimeId);
                    if (path.Count > 0)
                    {
                        yield return MoveUnitAlongPathCoroutine(
                            enemy,
                            path,
                            refreshPlayerLegalActions: false,
                            clearSelectionWhenActed: false,
                            manageActionLock: false);
                        if (_boardSyncFaulted) break;
                    }
                }

                if (_boardSyncFaulted) break;
                if (enemy.Stats.IsAlive && nearest.Stats.IsAlive && TryStartAttack(enemy, nearest, out _))
                {
                    yield return WaitForAttackToFinishCoroutine(enemy);
                }
            }

            CheckBattleEndCondition();
            FinishEnemyTurn();
        }

        private IEnumerator MoveUnitAlongPathCoroutine(
            UnitRuntime unit,
            List<Vector2Int> path,
            bool refreshPlayerLegalActions,
            bool clearSelectionWhenActed,
            bool manageActionLock)
        {
            if (unit == null || path == null || path.Count == 0) yield break;

            if (manageActionLock)
            {
                _isActionLocked = true;
            }

            // 路径表现由 UnitManager 逐格驱动；正式战斗单位必须绑定规则状态。
            var startCell = unit.Grid.GridPosition;
            if (!unit.IsRuleBound ||
                !_unitStateRules.TryChangeActionState(unit.RuntimeId, BFUnit_ActionState.Move))
            {
                RestoreMovePresentation(unit, startCell);
                if (manageActionLock)
                {
                    _isActionLocked = false;
                    _activeMoveCoroutine = null;
                }

                yield break;
            }

            var presenter = unit.GetComponent<BFUnitAnimationPresenter>();
            _activeMovingUnit = unit;
            unit.StateMachine.MoveState.SetTarget(path[^1]);
            unit.StateMachine.ChangeState(unit.StateMachine.MoveState);

            var previousCell = startCell;
            for (int i = 0; i < path.Count; i++)
            {
                if (unit == null || !unit.Stats.IsAlive || !unit.gameObject.activeInHierarchy) break;

                var nextCell = path[i];
                presenter?.FaceMovementStep(previousCell, nextCell);

                Vector3 fromWorld = unit.transform.position;
                Vector3 toWorld = (Vector3)_boardManager.CellToWorld(nextCell);
                float elapsed = 0f;

                while (elapsed < _secondsPerMoveCell)
                {
                    if (unit == null || !unit.Stats.IsAlive || !unit.gameObject.activeInHierarchy) break;

                    elapsed += Time.deltaTime;
                    float t = _secondsPerMoveCell <= 0f ? 1f : Mathf.Clamp01(elapsed / _secondsPerMoveCell);
                    unit.transform.position = Vector3.Lerp(fromWorld, toWorld, t);
                    yield return null;
                }

                if (unit == null || !unit.Stats.IsAlive || !unit.gameObject.activeInHierarchy) break;

                unit.transform.position = toWorld;
                previousCell = nextCell;
            }

            bool completed = unit != null && unit.Stats.IsAlive && unit.gameObject.activeInHierarchy && previousCell == path[^1];
            if (completed)
            {
                if (!CompleteMove(
                        unit,
                        startCell,
                        previousCell,
                        path.Count,
                        refreshPlayerLegalActions,
                        clearSelectionWhenActed,
                        out var boardSyncFailed))
                {
                    // 规则拒绝移动：恢复起点表现，位置与 AP 保持原值。
                    RestoreMovePresentation(unit, startCell);
                }
                else if (boardSyncFailed)
                {
                    _activeMovingUnit = null;
                    if (manageActionLock)
                    {
                        _isActionLocked = false;
                        _activeMoveCoroutine = null;
                    }
                    yield break;
                }
            }
            else if (unit != null && unit.Stats.IsAlive)
            {
                RestoreMovePresentation(unit, startCell);
            }

            if (manageActionLock)
            {
                _isActionLocked = false;
                _activeMoveCoroutine = null;
            }

            if (_activeMovingUnit == unit)
            {
                _activeMovingUnit = null;
            }
        }

        private IEnumerator WaitForAttackToFinishCoroutine(UnitRuntime unit)
        {
            const float timeoutSeconds = 5f;
            float elapsed = 0f;

            while (unit != null
                   && unit.Stats.IsAlive
                   && unit.gameObject.activeInHierarchy
                   && (unit.StateMachine.CurrentState is BFUnit_PresentationAttackState || unit.Combat.HasQueuedAttack || (_resolutionManager != null && _resolutionManager.HasPendingAttack(unit)))
                   && elapsed < timeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (elapsed >= timeoutSeconds)
            {
                Debug.LogWarning($"[BFBattleUnitManager] {unit.Identity.DisplayName} 攻击表现等待超时，请检查动画事件。");
                _resolutionManager?.ClearPendingAttack(unit);
                HandleAttackResolutionFailed(unit);
            }
            else if (unit != null && unit.IsRuleBound &&
                     unit.RuleState.ActionState == BFUnit_ActionState.Attack)
            {
                // 未超时但等待条件退出（如单位在命中前被禁用）：属于命中前中断，
                // 清理未完成攻击上下文并恢复规则行动状态，不消耗 AP、不造成伤害。
                _resolutionManager?.ClearPendingAttack(unit);
                HandleAttackResolutionFailed(unit);
            }
        }

        private bool TryGetMovePath(UnitRuntime unit, Vector2Int targetCell, out List<Vector2Int> path)
        {
            path = null;
            if (unit == null || !unit.IsRuleBound || _boardManager == null) return false;

            var remainingActionPoints = unit.RuleState.Attributes.RemainingActionPoints;
            path = _boardManager.FindPath(unit.Grid.GridPosition, targetCell, unit.RuntimeId);
            if (path.Count == 0)
            {
                Debug.LogWarning($"[BFBattleUnitManager] 目标格子 {targetCell} 不可达。");
                return false;
            }

            if (path.Count > remainingActionPoints)
            {
                Debug.LogWarning($"[BFBattleUnitManager] 路径成本 {path.Count} 超过剩余 AP {remainingActionPoints}");
                return false;
            }

            return true;
        }

        private bool TryStartAttack(UnitRuntime attacker, UnitRuntime target, out int cost)
        {
            cost = 0;
            if (_boardSyncFaulted || attacker == null || target == null || !attacker.IsRuleBound || !target.IsRuleBound) return false;
            if (!attacker.Stats.IsAlive || !target.Stats.IsAlive) return false;

            // 攻击数据只读规则属性；范围、阵营、资源和状态校验由规则入口统一完成。
            var attackerAttributes = attacker.RuleState.Attributes;
            cost = attackerAttributes.EffectiveAttackCost;
            if (cost <= 0 || attackerAttributes.RemainingActionPoints < cost) return false;

            if (_resolutionManager == null)
            {
                Debug.LogError("[BFBattleUnitManager] ResolutionManager 未绑定。");
                return false;
            }

            if (!_resolutionManager.TryQueueAttack(attacker, target))
            {
                Debug.LogWarning("[BFBattleUnitManager] 攻击登记失败。");
                return false;
            }

            if (!attacker.Combat.BeginQueuedAttack(target))
            {
                _resolutionManager.ClearPendingAttack(attacker);
                return false;
            }

            // 攻击开始阶段只锁定规则行动状态，不消耗 AP；AP 在命中结算时提交。
            var attackResult = _unitStateRules.TryStartAttack(
                new AttackRequest(attacker.RuntimeId, target.RuntimeId, cost));
            if (!attackResult.Succeeded)
            {
                Debug.LogWarning($"[BFBattleUnitManager] 规则拒绝攻击：{attackResult.FailureReason}");
                _resolutionManager.ClearPendingAttack(attacker);
                attacker.Combat.ClearQueuedAttack();
                return false;
            }

            attacker.RefreshRuleStateProjection();

            attacker.StateMachine.AttackState.SetTarget(target);
            attacker.StateMachine.ChangeState(attacker.StateMachine.AttackState);
            return true;
        }

        /// <summary>
        /// 提交一次移动：规则位置与 AP 由规则入口一次性提交，随后同步棋盘与表现。
        ///
        /// 返回 true 表示规则移动已经提交；<paramref name="boardSyncFailed" /> 指示棋盘同步是否失败。
        /// 规则提交后的棋盘同步失败属于适配层严重错误：规则结果不回滚，移动领域事实仍然发布，
        /// 但本次行动不报告为表现成功，也不继续后续表现流程。
        /// </summary>
        internal bool CompleteMove(
            UnitRuntime unit,
            Vector2Int startCell,
            Vector2Int targetCell,
            int moveCost,
            bool refreshPlayerLegalActions,
            bool clearSelectionWhenActed,
            out bool boardSyncFailed)
        {
            boardSyncFailed = false;

            var moveResult = _unitStateRules.TryMove(
                new MoveRequest(
                    unit.RuntimeId,
                    new BFGridPosition(targetCell.x, targetCell.y),
                    moveCost));
            if (!moveResult.Succeeded)
            {
                Debug.LogWarning($"[BFBattleUnitManager] 规则移动被拒绝：{unit.Identity.DisplayName}，{moveResult.FailureReason}");
                return false;
            }

            unit.RefreshRuleStateProjection();

            // 规则位置和 AP 已经成功提交；这里仅同步棋盘适配与世界坐标表现。
            if (!_boardManager.TryMoveOccupancy(startCell, targetCell, unit.RuntimeId))
            {
                Debug.LogError(
                    $"[BFBattleUnitManager] 棋盘占用同步失败：{unit.Identity.DisplayName} 目标格 {targetCell} 不可占用。规则位置与 AP 已提交，停止本次表现流程。");
                unit.transform.position = (Vector3)_boardManager.CellToWorld(targetCell);
                unit.StateMachine.ChangeState(unit.StateMachine.IdleState);

                // 规则事实仍然有效：移动领域事件正常发布一次，表现成功通知不再发出。
                _battleSession.Publish(new BFUnitMovedEvent(
                    _battleSession.Context.BattleId,
                    unit.RuntimeId,
                    moveResult.FromGridPosition.Value,
                    moveResult.ToGridPosition.Value,
                    moveCost,
                    _battleSession.Context.TurnNumber));
                MarkBoardSyncFault();
                boardSyncFailed = true;
                return true;
            }

            unit.transform.position = (Vector3)_boardManager.CellToWorld(targetCell);
            unit.StateMachine.ChangeState(unit.StateMachine.IdleState);

            // 移动事实在规则提交完成后发布，由 SO 适配器单向转发。
            _battleSession.Publish(new BFUnitMovedEvent(
                _battleSession.Context.BattleId,
                unit.RuntimeId,
                moveResult.FromGridPosition.Value,
                moveResult.ToGridPosition.Value,
                moveCost,
                _battleSession.Context.TurnNumber));
            Debug.Log($"[BFBattleUnitManager] {unit.Identity.DisplayName} moved {moveCost} cells to {targetCell}, AP left: {unit.Stats.RemainingActionPoints}");

            if (clearSelectionWhenActed && SelectedUnit == unit && unit.Stats.HasActed)
            {
                DeselectUnitIgnoringLock();
            }

            if (refreshPlayerLegalActions)
            {
                _turnManager?.RefreshPlayerLegalActions();
            }

            OnUnitMoveCompleted?.Invoke(unit);
            return true;
        }

        /// <summary>
        /// 恢复未提交移动的表现状态。
        /// 规则状态未提交时，Transform 和表现状态必须回到移动开始位置；
        /// 棋盘占用尚未切换，因此这里不反向修改 BoardManager。
        /// </summary>
        private void RestoreMovePresentation(UnitRuntime unit, Vector2Int startCell)
        {
            if (unit == null) return;

            if (unit.IsRuleBound && _unitStateRules != null)
            {
                if (_unitStateRules.TryChangeActionState(
                        unit.RuntimeId,
                        BFUnit_ActionState.Idle))
                {
                    unit.RefreshRuleStateProjection();
                }
            }

            if (unit.Stats.IsAlive)
            {
                if (_boardManager != null && unit.gameObject.activeInHierarchy)
                {
                    unit.transform.position = (Vector3)_boardManager.CellToWorld(startCell);
                }

                unit.StateMachine.ChangeState(unit.StateMachine.IdleState);
            }
        }

        private void RestoreInterruptedMove()
        {
            var unit = _activeMovingUnit;
            _activeMovingUnit = null;
            if (unit == null || _boardManager == null) return;
            if (!unit.Stats.IsAlive) return;

            if (unit.IsRuleBound && _unitStateRules != null
                && _unitStateRules.TryChangeActionState(
                    unit.RuntimeId,
                    BFUnit_ActionState.Idle))
            {
                unit.RefreshRuleStateProjection();
            }

            unit.transform.position = (Vector3)_boardManager.CellToWorld(unit.Grid.GridPosition);
            unit.StateMachine.ChangeState(unit.StateMachine.IdleState);
        }

        private void FinishEnemyTurn()
        {
            _enemyTurnCoroutine = null;
            _isActionLocked = false;

            if (!_boardSyncFaulted && (Result == null || !Result.HasResult))
            {
                _turnManager?.EndTurn();
            }
        }

        private void HandleUnitDisabled(UnitRuntime unit)
        {
            if (unit == null) return;

            if (_activeMovingUnit == unit && _activeMoveCoroutine != null)
            {
                StopCoroutine(_activeMoveCoroutine);
                _activeMoveCoroutine = null;
                RestoreInterruptedMove();
            }

            unit.Combat.ClearQueuedAttack();
            _resolutionManager?.ClearPendingAttack(unit);

            if (_battleSession == null || _unitStateRules == null || !unit.IsRuleBound)
                return;

            if (unit.RuleState.ActionState == BFUnit_ActionState.Attack &&
                _unitStateRules.TryChangeActionState(unit.RuntimeId, BFUnit_ActionState.Idle))
            {
                unit.RefreshRuleStateProjection();
            }

            if (_enemyTurnCoroutine == null)
                _isActionLocked = false;
        }

        private void MarkBoardSyncFault()
        {
            if (_boardSyncFaulted) return;

            _boardSyncFaulted = true;
        }

        private UnitRuntime FindNearestPlayer(UnitRuntime enemy, List<UnitRuntime> players)
        {
            UnitRuntime nearest = null;
            float minDistance = float.MaxValue;

            foreach (var player in players)
            {
                if (player == null || !player.Stats.IsAlive) continue;

                float distance = Vector2Int.Distance(enemy.Grid.GridPosition, player.Grid.GridPosition);
                if (distance >= minDistance) continue;

                minDistance = distance;
                nearest = player;
            }

            return nearest;
        }

        private Vector2Int FindBestReachableCell(List<Vector2Int> reachable, Vector2Int target)
        {
            Vector2Int best = reachable[0];
            float bestDistance = float.MaxValue;

            foreach (var cell in reachable)
            {
                float distance = Vector2Int.Distance(cell, target);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = cell;
            }

            return best;
        }

        private void RaiseUnitActionEvent(UnitRuntime unit, string eventType, string targetId, int value)
        {
            _unitEventChannel?.Raise(new BFUnitEventData
            {
                UnitId = unit.RuntimeId,
                EventType = eventType,
                TargetId = targetId,
                Value = value
            });
        }

        private void DeselectUnitIgnoringLock()
        {
            if (SelectedUnit == null) return;

            var old = SelectedUnit;
            SelectedUnit = null;
            OnUnitDeselected?.Invoke(old);
            _unitEventChannel?.Raise(new BFUnitEventData
            {
                UnitId = old.RuntimeId,
                EventType = "Deselected"
            });
        }

        private static int GetManhattanDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }
    }
}
