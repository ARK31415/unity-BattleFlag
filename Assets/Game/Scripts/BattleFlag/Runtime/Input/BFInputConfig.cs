using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BF.Game.Runtime.Input
{
    /// <summary>
    /// Map Group 配置定义，表达一组 Action Map 的共同启停及其阻塞关系。
    /// 仅配置 Action Map 启停分组，不配置具体 Action 或按键绑定。
    /// </summary>
    [Serializable]
    public sealed class BFInputMapGroupDefinition
    {
        [SerializeField] private BFInputMapGroupId _id;
        [SerializeField] private List<BFInputActionMapId> _actionMaps = new();
        [SerializeField] private List<BFInputMapGroupId> _blockedGroups = new();

        public BFInputMapGroupId Id => _id;
        public IReadOnlyList<BFInputActionMapId> ActionMaps => _actionMaps;
        public IReadOnlyList<BFInputMapGroupId> BlockedGroups => _blockedGroups;

        public BFInputMapGroupDefinition() { }

        public BFInputMapGroupDefinition(BFInputMapGroupId id, IEnumerable<BFInputActionMapId> actionMaps,
            IEnumerable<BFInputMapGroupId> blockedGroups = null)
        {
            _id = id;
            _actionMaps = new List<BFInputActionMapId>(actionMaps);
            _blockedGroups = blockedGroups != null ? new List<BFInputMapGroupId>(blockedGroups) : new List<BFInputMapGroupId>();
        }
    }

    /// <summary>
    /// Profile 配置定义，表达一个完整输入快照中需要启用的 Map Group 集合。
    /// Profile 切换为完整快照行为，不做增量叠加。
    /// </summary>
    [Serializable]
    public sealed class BFInputProfileDefinition
    {
        [SerializeField] private BFInputProfileId _id;
        [SerializeField] private List<BFInputMapGroupId> _enabledGroups = new();

        public BFInputProfileId Id => _id;
        public IReadOnlyList<BFInputMapGroupId> EnabledGroups => _enabledGroups;

        public BFInputProfileDefinition() { }

        public BFInputProfileDefinition(BFInputProfileId id, IEnumerable<BFInputMapGroupId> enabledGroups)
        {
            _id = id;
            _enabledGroups = new List<BFInputMapGroupId>(enabledGroups);
        }
    }

    /// <summary>
    /// BattleFlag 输入配置资产，在 Inspector 中维护 Map Group、Profile 和启动 Profile。
    /// 不配置具体 Action，不保存玩家改键数据，不保存运行时状态。
    /// </summary>
    [CreateAssetMenu(fileName = "BFInputConfig", menuName = "BF/Input/Config")]
    public sealed class BFInputConfig : ScriptableObject
    {
        [SerializeField] private List<BFInputMapGroupDefinition> _mapGroups = new();
        [SerializeField] private List<BFInputProfileDefinition> _profiles = new();
        [SerializeField] private BFInputProfileId _startupProfile = BFInputProfileId.BattleHud;

        public IReadOnlyList<BFInputMapGroupDefinition> MapGroups => _mapGroups;
        public IReadOnlyList<BFInputProfileDefinition> Profiles => _profiles;
        public BFInputProfileId StartupProfile => _startupProfile;

        /// <summary>
        /// 通过 Map Group 标识查找对应的配置定义。
        /// </summary>
        public bool TryGetGroup(BFInputMapGroupId id, out BFInputMapGroupDefinition definition)
        {
            definition = _mapGroups.FirstOrDefault(g => g.Id == id);
            return definition != null;
        }

        /// <summary>
        /// 通过 Profile 标识查找对应的配置定义。
        /// </summary>
        public bool TryGetProfile(BFInputProfileId id, out BFInputProfileDefinition definition)
        {
            definition = _profiles.FirstOrDefault(p => p.Id == id);
            return definition != null;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 仅用于测试时批量设置 Map Group 和 Profile 定义。
        /// </summary>
        public void SetTestDefinitions(IEnumerable<BFInputMapGroupDefinition> groups,
            IEnumerable<BFInputProfileDefinition> profiles, BFInputProfileId startupProfile)
        {
            _mapGroups = groups != null ? new List<BFInputMapGroupDefinition>(groups) : new List<BFInputMapGroupDefinition>();
            _profiles = profiles != null ? new List<BFInputProfileDefinition>(profiles) : new List<BFInputProfileDefinition>();
            _startupProfile = startupProfile;
        }
#endif
    }
}
