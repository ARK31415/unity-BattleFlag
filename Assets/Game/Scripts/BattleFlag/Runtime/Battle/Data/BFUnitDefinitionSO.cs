using UnityEngine;

namespace BF.Game.Runtime.Battle.Data
{
    /// <summary>
    /// 单位定义聚合入口（ScriptableObject），引用 Config、Binding、Progression 三个职责拆分的配置资产。
    /// 不复制策划字段，只暴露只读便捷入口。
    /// </summary>
    [CreateAssetMenu(fileName = "BFUnitDefinition", menuName = "BF/Battle/Units/Definition")]
    public class BFUnitDefinitionSO : ScriptableObject
    {
        /// <summary>策划基础配置（身份、阵营、白值、技能 ID）。</summary>
        [SerializeField] private BFUnitImportedConfigSO _importedConfig;
        /// <summary>Unity 资源绑定（Animator、动画键、VFX/SFX、可选 Override Prefab）。</summary>
        [SerializeField] private BFUnitUnityBindingSO _unityBinding;
        /// <summary>成长表（按等级查找属性）。</summary>
        [SerializeField] private BFUnitProgressionTableSO _progressionTable;

        /// <summary>策划基础配置。</summary>
        public BFUnitImportedConfigSO ImportedConfig => _importedConfig;
        /// <summary>Unity 资源绑定。</summary>
        public BFUnitUnityBindingSO UnityBinding => _unityBinding;
        /// <summary>成长表。</summary>
        public BFUnitProgressionTableSO ProgressionTable => _progressionTable;
        /// <summary>单位 ID，从 ImportedConfig 读取。</summary>
        public string UnitId => _importedConfig != null ? _importedConfig.UnitId : string.Empty;
        /// <summary>显示名，从 ImportedConfig 读取。</summary>
        public string DisplayName => _importedConfig != null ? _importedConfig.DisplayName : string.Empty;

        /// <summary>
        /// 从 Imported Config 读取基础战斗白值。
        /// </summary>
        public BFUnitStatBlock GetBaseStats()
        {
            return _importedConfig != null ? _importedConfig.BaseStats : BFUnitStatBlock.Default;
        }

        /// <summary>
        /// 从 Progression Table 读取等级属性。
        /// </summary>
        /// <param name="level">目标等级。</param>
        /// <param name="stats">查找到的属性包。</param>
        /// <returns>true 表示找到该等级配置。</returns>
        public bool TryGetProgressionStats(int level, out BFUnitStatBlock stats)
        {
            if (_progressionTable != null && _progressionTable.TryGetStatsForLevel(level, out stats))
            {
                return true;
            }

            stats = default;
            return false;
        }

        /// <summary>
        /// 校验 Config 和 Binding 是否已配置。
        /// </summary>
        /// <param name="error">校验失败时输出错误信息。</param>
        /// <returns>true 表示配置完整有效。</returns>
        public bool ValidateConfiguration(out string error)
        {
            if (_importedConfig == null)
            {
                error = $"{name} missing BFUnitImportedConfigSO.";
                return false;
            }

            if (_unityBinding == null)
            {
                error = $"{name} missing BFUnitUnityBindingSO.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
