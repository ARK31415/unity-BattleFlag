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
        private readonly Dictionary<string, WitUIWindowDefinition> _definitionsByKey = new();

        /// <summary>
        /// 通过窗口 key 查找窗口定义。
        /// </summary>
        public bool TryGetWindow(string key, out WitUIWindowDefinition definition)
        {
            EnsureLookup();
            if (string.IsNullOrWhiteSpace(key))
            {
                definition = null;
                return false;
            }

            return _definitionsByKey.TryGetValue(key, out definition);
        }

        /// <summary>
        /// 校验配置中的 key、prefab、View 组件、层级和策略组合，供测试与 Editor 面板复用。
        /// </summary>
        public IReadOnlyList<string> ValidateDefinitions(WitUIRoot root = null)
        {
            var errors = new List<string>();
            var keys = new HashSet<string>();

            foreach (var definition in _definitions.Where(d => d != null))
            {
                if (string.IsNullOrWhiteSpace(definition.Key))
                    errors.Add("存在空 UI 窗口 key。");
                else if (!keys.Add(definition.Key))
                    errors.Add($"UI 窗口 key 重复: {definition.Key}");

                if (definition.Prefab == null)
                {
                    errors.Add($"UI 窗口 '{definition.Key}' 的 prefab 为空。");
                }
                else if (definition.Prefab.GetComponent<WitUIView>() == null)
                {
                    errors.Add($"UI 窗口 '{definition.Key}' 的 prefab 缺少 WitUIView 组件。");
                }

                if (root != null && root.GetLayerRoot(definition.Layer) == null)
                    errors.Add($"UI 窗口 '{definition.Key}' 的层级 {definition.Layer} 未在 WitUIRoot 中配置。");

                if (definition.Modal && definition.Layer != WitUILayer.Popup && definition.Layer != WitUILayer.Overlay)
                    errors.Add($"UI 窗口 '{definition.Key}' 的 modal 只能用于 Popup 或 Overlay 层。");

                if (definition.HasDisabledUniqueFlag)
                    errors.Add($"UI 窗口 '{definition.Key}' 配置了已禁用的 Unique=false。");
            }

            return errors;
        }

        private void OnEnable()
        {
            RebuildLookup();
        }

        private void OnValidate()
        {
            RebuildLookup();
        }

        private void EnsureLookup()
        {
            if (_definitionsByKey.Count == 0 && _definitions.Count > 0)
                RebuildLookup();
        }

        private void RebuildLookup()
        {
            _definitionsByKey.Clear();
            foreach (var definition in _definitions.Where(d => d != null))
            {
                if (string.IsNullOrWhiteSpace(definition.Key))
                    continue;
                if (_definitionsByKey.ContainsKey(definition.Key))
                    continue;

                _definitionsByKey.Add(definition.Key, definition);
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// 仅用于测试时批量设置定义列表，不在运行时代码中使用。
        /// </summary>
        public void SetTestDefinitions(IEnumerable<WitUIWindowDefinition> definitions)
        {
            _definitions = new List<WitUIWindowDefinition>(definitions);
            RebuildLookup();
        }

        /// <summary>
        /// 仅用于测试时追加单个窗口定义。
        /// </summary>
        public void AddTestDefinition(WitUIWindowDefinition definition)
        {
            _definitions.Add(definition);
            RebuildLookup();
        }
#endif
    }
}
