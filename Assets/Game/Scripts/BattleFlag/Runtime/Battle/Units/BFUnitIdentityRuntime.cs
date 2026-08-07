using BF.Game.Battle.Domain.Units;
using BF.Game.Runtime.Battle.Data;
using UnityEngine;
using UnityEngine.Serialization;

namespace BF.Game.Runtime.Battle.Units
{
    /// <summary>
    /// 单位身份运行时组件。
    ///
    /// 职责边界：
    /// - 保存显示名、阵营和角色类型，作为外部系统读取单位身份的唯一业务入口。
    /// - 不保存 HP、AP、攻击力、格子位置或状态机数据。
    /// - 未绑定规则状态的场景手摆对象不拥有 RuntimeId。
    /// - 新创建链完成绑定后，RuntimeId 由规则状态提供，GameObject.name 仅用于层级显示和调试。
    /// </summary>
    [DisallowMultipleComponent]
    public class BFUnitIdentityRuntime : MonoBehaviour
    {
        [Header("Identity")]
        /// <summary>当前战斗实例身份；由规则状态绑定后写入。</summary>
        [FormerlySerializedAs("_unitId")]
        [SerializeField] private string _runtimeId;
        /// <summary>规则配置身份，允许多个运行时单位共享。</summary>
        [SerializeField] private string _profileId;
        /// <summary>HUD 和日志显示名。</summary>
        [SerializeField] private string _displayName = "Unit";
        /// <summary>单位所属阵营。</summary>
        [SerializeField] private UnitFaction _faction = UnitFaction.Player;
        /// <summary>单位角色类型。</summary>
        [SerializeField] private BFUnitRole _role = BFUnitRole.Warrior;

        /// <summary>当前战斗实例身份；未绑定规则状态时为空。</summary>
        public string RuntimeId => _runtimeId;

        /// <summary>配置身份；允许多个 RuntimeId 共享同一 ProfileId。</summary>
        public string ProfileId => _profileId;

        /// <summary>HUD 和日志显示名；写入空白时回退为通用名称，避免 UI 出现空文本。</summary>
        public string DisplayName
        {
            get => _displayName;
            set => _displayName = string.IsNullOrWhiteSpace(value) ? "Unit" : value;
        }

        /// <summary>单位所属阵营，用于选择、敌我判断和胜负判定。</summary>
        public UnitFaction Faction
        {
            get => _faction;
            set => _faction = value;
        }

        /// <summary>单位角色类型，当前用于区分战士、法师等测试棋子身份。</summary>
        public BFUnitRole Role
        {
            get => _role;
            set => _role = value;
        }

        /// <summary>
        /// 从单位配置和生成上下文写入运行时身份副本。
        /// </summary>
        public void InitializeFromConfig(BFUnitImportedConfigSO config, UnitFaction faction)
        {
            if (config == null) return;

            _runtimeId = null;
            _profileId = config.ProfileId;
            DisplayName = config.DisplayName;
            _faction = faction;
            _role = config.Role;
        }

        /// <summary>清除当前规则状态投影，解除绑定后释放 ProfileId 与 RuntimeId。</summary>
        public void ClearRuleIdentity()
        {
            _runtimeId = null;
            _profileId = null;
        }

        /// <summary>
        /// 将规则状态的身份投影到表现组件。
        /// RuntimeId 表示当前战斗实例，ProfileId 单独保存配置身份。
        /// </summary>
        public void InitializeFromRuleState(BFUnitState state, string displayName)
        {
            if (state == null) return;

            _runtimeId = state.RuntimeId;
            _profileId = state.ProfileId;
            DisplayName = displayName;
            _faction = ToRuntimeFaction(state.Faction);
            _role = ToRuntimeRole(state.Role);
        }

        private static UnitFaction ToRuntimeFaction(BF.Game.Battle.Domain.Events.BFUnitFaction faction)
        {
            return faction switch
            {
                BF.Game.Battle.Domain.Events.BFUnitFaction.Player => UnitFaction.Player,
                BF.Game.Battle.Domain.Events.BFUnitFaction.Enemy => UnitFaction.Enemy,
                _ => UnitFaction.None
            };
        }

        private static BF.Game.Runtime.Battle.Units.BFUnitRole ToRuntimeRole(
            BF.Game.Battle.Domain.Units.BFUnitRole role)
        {
            return role switch
            {
                BF.Game.Battle.Domain.Units.BFUnitRole.Mage => BF.Game.Runtime.Battle.Units.BFUnitRole.Mage,
                _ => BF.Game.Runtime.Battle.Units.BFUnitRole.Warrior
            };
        }
    }
}
