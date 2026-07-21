using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BF.Game.Runtime.Input
{
    /// <summary>
    /// BattleFlag 全局输入运行时入口，持有唯一 BFInputActions 实例，
    /// 按 Profile / Map Group 启停 Action Map，并向业务组件暴露强类型 Actions 访问。
    ///
    /// 职责边界:
    /// - 负责创建并持有唯一 BFInputActions 实例。
    /// - 负责按 BFInputConfig 中的 Map Group 和 Profile 启停 Action Map。
    /// - 暴露 Actions 给业务脚本读取，业务脚本自行订阅/退订具体 Action。
    /// - 不负责 Action 查询、业务订阅中心、改键或本地多人玩家输入。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BFInputManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private BFInputConfig _config;
        [SerializeField] private bool _applyStartupProfileOnAwake = true;

        /// <summary>全局单例，后续常驻 Persistence 场景。</summary>
        public static BFInputManager Instance { get; private set; }

        /// <summary>唯一 BFInputActions 实例，业务组件通过此属性获取强类型 Action 引用。</summary>
        public BFInputActions Actions { get; private set; }

        /// <summary>当前激活的 Profile 标识。</summary>
        public BFInputProfileId CurrentProfile { get; private set; }

        private readonly HashSet<BFInputMapGroupId> _enabledGroupIds = new();
        private readonly Dictionary<BFInputActionMapId, string> _actionMapNameCache = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[BFInputManager] 场景中存在多个 BFInputManager 实例，当前实例将被禁用。", this);
                enabled = false;
                return;
            }

            Instance = this;
            EnsureActions();

            if (_config == null)
            {
                Debug.LogWarning("[BFInputManager] BFInputConfig 未配置，输入管理器不会自动应用 Profile。", this);
                return;
            }

            if (_applyStartupProfileOnAwake)
            {
                ApplyProfile(_config.StartupProfile);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                if (Actions != null)
                {
                    if (Application.isPlaying)
                        Actions.Dispose();
                    else
                        DestroyImmediate(Actions.asset);
                }

                Actions = null;
                Instance = null;
            }
        }

        /// <summary>
        /// 将当前输入状态切换为指定 Profile 的完整快照。
        /// 先统一关闭当前已启用的所有 Map Group，再启用目标 Profile 的 Group 并应用阻塞规则。
        /// </summary>
        public bool ApplyProfile(BFInputProfileId profileId)
        {
            if (!ValidateReady()) return false;
            if (!_config.TryGetProfile(profileId, out var profile))
            {
                Debug.LogError($"[BFInputManager] 未找到 Profile: {profileId}", this);
                return false;
            }

            foreach (var groupId in new List<BFInputMapGroupId>(_enabledGroupIds))
            {
                _enabledGroupIds.Remove(groupId);
            }

            foreach (var groupId in profile.EnabledGroups)
            {
                if (!TryAddGroup(groupId))
                    return false;
            }

            ApplyBlockingRules();
            RebuildActionMapState();
            CurrentProfile = profileId;
            return true;
        }

        /// <summary>
        /// 显式启用一个 Map Group 对应的所有 Action Map。
        /// </summary>
        public bool EnableGroup(BFInputMapGroupId groupId)
        {
            if (!ValidateReady()) return false;
            if (!TryAddGroup(groupId)) return false;
            ApplyBlockingRules();
            RebuildActionMapState();
            return true;
        }

        /// <summary>
        /// 显式禁用一个 Map Group 对应的所有 Action Map。
        /// </summary>
        public bool DisableGroup(BFInputMapGroupId groupId)
        {
            if (!ValidateReady()) return false;
            if (!_config.TryGetGroup(groupId, out _))
            {
                Debug.LogWarning($"[BFInputManager] 未找到 Map Group: {groupId}", this);
                return false;
            }

            _enabledGroupIds.Remove(groupId);
            ApplyBlockingRules();
            RebuildActionMapState();
            return true;
        }

        /// <summary>
        /// 查询指定 Map Group 是否处于启用状态。
        /// </summary>
        public bool IsGroupEnabled(BFInputMapGroupId groupId)
        {
            return _enabledGroupIds.Contains(groupId);
        }

        private bool ValidateReady()
        {
            EnsureActions();

            if (_config == null)
            {
                Debug.LogError("[BFInputManager] BFInputConfig 未配置。", this);
                return false;
            }
            return true;
        }

        private void EnsureActions()
        {
            Actions ??= new BFInputActions();
        }

        private bool TryAddGroup(BFInputMapGroupId groupId)
        {
            if (!_config.TryGetGroup(groupId, out var group))
            {
                Debug.LogWarning($"[BFInputManager] 未找到 Map Group: {groupId}", this);
                return false;
            }

            _enabledGroupIds.Add(groupId);
            return true;
        }

        private void ApplyBlockingRules()
        {
            foreach (var blockingGroupId in new HashSet<BFInputMapGroupId>(_enabledGroupIds))
            {
                if (!_config.TryGetGroup(blockingGroupId, out var blockingGroup))
                    continue;

                foreach (var targetId in blockingGroup.BlockedGroups)
                    _enabledGroupIds.Remove(targetId);
            }
        }

        private void RebuildActionMapState()
        {
            HashSet<BFInputActionMapId> mapsToEnable = new();
            foreach (var groupId in _enabledGroupIds)
            {
                if (!_config.TryGetGroup(groupId, out var group))
                    continue;

                foreach (var actionMapId in group.ActionMaps)
                    mapsToEnable.Add(actionMapId);
            }

            foreach (BFInputActionMapId actionMapId in Enum.GetValues(typeof(BFInputActionMapId)))
            {
                var actionMap = Actions.asset.FindActionMap(GetActionMapName(actionMapId), false);
                if (actionMap == null)
                    continue;

                if (mapsToEnable.Contains(actionMapId))
                    actionMap.Enable();
                else
                    actionMap.Disable();
            }
        }

        private string GetActionMapName(BFInputActionMapId actionMapId)
        {
            if (!_actionMapNameCache.TryGetValue(actionMapId, out var name))
            {
                name = actionMapId switch
                {
                    BFInputActionMapId.Battle => "Battle",
                    BFInputActionMapId.BattleCamera => "BattleCamera",
                    BFInputActionMapId.Global => "Global",
                    BFInputActionMapId.UI => "UI",
                    _ => actionMapId.ToString()
                };
                _actionMapNameCache[actionMapId] = name;
            }
            return name;
        }
    }
}
