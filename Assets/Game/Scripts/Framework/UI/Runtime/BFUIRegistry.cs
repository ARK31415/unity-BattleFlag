using System;
using System.Collections.Generic;
using UnityEngine;

namespace BF.Framework.UI.Runtime
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class BFUIPanelAttribute : Attribute
    {
        public string PanelId { get; }
        public BFUIPanelAttribute(string panelId) { PanelId = panelId; }
    }

    [Serializable]
    public class BFUIRegistryEntry
    {
        public string panelId;
        public GameObject prefab;
    }

    [CreateAssetMenu(fileName = "BFUIRegistryConfig", menuName = "BF/UI/Registry Config")]
    public class BFUIRegistryConfig : ScriptableObject
    {
        [SerializeField] private List<BFUIRegistryEntry> _entries = new();
        public IReadOnlyList<BFUIRegistryEntry> Entries => _entries;
    }

    public class BFUIRegistry
    {
        private readonly Dictionary<string, GameObject> _prefabs = new();

        public void Register(string panelId, GameObject prefab) { _prefabs[panelId] = prefab; }

        public void Register(BFUIRegistryConfig config)
        {
            if (config == null) return;
            foreach (var entry in config.Entries)
                if (!string.IsNullOrEmpty(entry.panelId) && entry.prefab != null)
                    _prefabs[entry.panelId] = entry.prefab;
        }

        public bool TryGetPrefab(string panelId, out GameObject prefab)
            => _prefabs.TryGetValue(panelId, out prefab);

        public static string TryGetPanelId<TPanel>() where TPanel : MonoBehaviour
        {
            var attr = typeof(TPanel).GetCustomAttributes(typeof(BFUIPanelAttribute), false);
            return attr.Length > 0 ? ((BFUIPanelAttribute)attr[0]).PanelId : null;
        }
    }
}
