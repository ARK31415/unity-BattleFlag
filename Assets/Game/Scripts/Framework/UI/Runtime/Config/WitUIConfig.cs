using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wit.Framework.UI
{
    /// <summary>
    /// UI 窗口配置资产，保存所有窗口 key、prefab、层级和策略定义。
    /// 该类仅作为配置数据容器，不保存运行时窗口状态。
    /// </summary>
    [CreateAssetMenu(fileName = "WitUIConfig", menuName = "Wit/UI/Config")]
    public sealed class WitUIConfig : ScriptableObject
    {
        [SerializeField] private List<WitUIWindowDefinition> _definitions = new();

        /// <summary>
        /// 通过窗口 key 查找窗口定义。
        /// </summary>
        public bool TryGetWindow(string key, out WitUIWindowDefinition definition)
        {
            definition = _definitions.FirstOrDefault(d => d.Key == key);
            return definition != null;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 仅用于测试时批量设置定义列表，不在运行时代码中使用。
        /// </summary>
        public void SetTestDefinitions(IEnumerable<WitUIWindowDefinition> definitions)
        {
            _definitions = new List<WitUIWindowDefinition>(definitions);
        }

        /// <summary>
        /// 仅用于测试时追加单个窗口定义。
        /// </summary>
        public void AddTestDefinition(WitUIWindowDefinition definition)
        {
            _definitions.Add(definition);
        }
#endif
    }
}
