using UnityEngine;

namespace BF.Game.Runtime.Battle.Data
{
    /// <summary>
    /// 单位定义聚合入口，只引用职责拆分后的配置资产。
    /// </summary>
    [CreateAssetMenu(fileName = "BFUnitDefinition", menuName = "BF/Battle/Units/Definition")]
    public class BFUnitDefinitionSO : ScriptableObject
    {
        [SerializeField] private BFUnitImportedConfigSO _importedConfig;
        [SerializeField] private BFUnitUnityBindingSO _unityBinding;
        [SerializeField] private BFUnitProgressionTableSO _progressionTable;

        public BFUnitImportedConfigSO ImportedConfig => _importedConfig;
        public BFUnitUnityBindingSO UnityBinding => _unityBinding;
        public BFUnitProgressionTableSO ProgressionTable => _progressionTable;
        public string UnitId => _importedConfig != null ? _importedConfig.UnitId : string.Empty;
        public string DisplayName => _importedConfig != null ? _importedConfig.DisplayName : string.Empty;

        public BFUnitStatBlock GetBaseStats()
        {
            return _importedConfig != null ? _importedConfig.BaseStats : BFUnitStatBlock.Default;
        }

        public bool TryGetProgressionStats(int level, out BFUnitStatBlock stats)
        {
            if (_progressionTable != null && _progressionTable.TryGetStatsForLevel(level, out stats))
            {
                return true;
            }

            stats = default;
            return false;
        }

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
