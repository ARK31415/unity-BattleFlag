using BF.Game.Battle.Domain.Units;
using BF.Game.Runtime.Battle.Data;
using UnityEngine;

namespace BF.Game.Runtime.Battle.Units
{
    /// <summary>
    /// 单位身份运行时组件。
    ///
    /// 职责边界：
    /// - 保存显示名、阵营和角色类型，作为外部系统读取单位身份的唯一业务入口。
    /// - 不保存 HP、AP、攻击力、格子位置或状态机数据。
    /// - 未绑定规则状态的场景手摆对象，UnitId 才回退为根 GameObject 名称。
    /// - 新创建链完成绑定后，UnitId 由规则状态的 RuntimeId 提供，GameObject.name 仅用于层级显示和调试。
    /// </summary>
    [DisallowMultipleComponent]
    public class BFUnitIdentityRuntime : MonoBehaviour
    {
        [Header("Identity")]
        /// <summary>兼容用的单位实例 ID；绑定规则状态后等同于 RuntimeId。</summary>
        [SerializeField] private string _unitId;
        /// <summary>规则配置身份，允许多个运行时单位共享。</summary>
        [SerializeField] private string _profileId;
        /// <summary>HUD 和日志显示名。</summary>
        [SerializeField] private string _displayName = "Unit";
        /// <summary>单位所属阵营。</summary>
        [SerializeField] private UnitFaction _faction = UnitFaction.Player;
        /// <summary>单位角色类型。</summary>
        [SerializeField] private BFUnitRole _role = BFUnitRole.Warrior;

        /// <summary>未绑定规则状态时回退为 GameObject 名称的兼容实例 ID。</summary>
        public string UnitId => !string.IsNullOrWhiteSpace(_unitId)
            ? _unitId
            : gameObject != null ? gameObject.name : "Unknown";

        /// <summary>配置身份；绑定规则状态后与 ProfileId 对齐。</summary>
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

            _unitId = config.UnitId;
            _profileId = config.UnitId;
            DisplayName = config.DisplayName;
            _faction = faction;
            _role = config.Role;
        }

        /// <summary>清除当前规则状态投影，解除绑定后让兼容实例 ID 回退为对象名。</summary>
        public void ClearRuleIdentity()
        {
            _unitId = null;
            _profileId = null;
        }

        /// <summary>
        /// 将规则状态的身份投影到表现组件。
        /// UnitId 在新创建链中表示 RuntimeId，ProfileId 单独保存配置身份。
        /// </summary>
        public void InitializeFromRuleState(BFUnitState state, string displayName)
        {
            if (state == null) return;

            _unitId = state.RuntimeId;
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
