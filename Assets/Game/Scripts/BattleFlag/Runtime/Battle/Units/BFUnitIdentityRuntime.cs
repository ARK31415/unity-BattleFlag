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
    /// - 当前 UnitId 沿用根 GameObject 名称，后续数据驱动阶段可再替换为配置 ID。
    /// </summary>
    [DisallowMultipleComponent]
    public class BFUnitIdentityRuntime : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string _unitId;
        [SerializeField] private string _displayName = "Unit";
        [SerializeField] private UnitFaction _faction = UnitFaction.Player;
        [SerializeField] private BFUnitRole _role = BFUnitRole.Warrior;

        /// <summary>场景手摆阶段的单位实例 ID，当前由 GameObject 名称提供。</summary>
        public string UnitId => !string.IsNullOrWhiteSpace(_unitId)
            ? _unitId
            : gameObject != null ? gameObject.name : "Unknown";

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
            DisplayName = config.DisplayName;
            _faction = faction;
            _role = config.Role;
        }
    }
}
