using UnityEngine;

namespace BF.Framework.UI.Runtime
{
    /// <summary>
    /// 第一版采用 Prefab 直连加载，后续可替换为 Addressables。
    /// </summary>
    public class BFUILoader
    {
        private readonly BFUIRegistry _registry;

        public BFUILoader(BFUIRegistry registry)
        {
            _registry = registry;
        }

        public GameObject Load(string panelId, Transform parent = null)
        {
            return _registry != null && _registry.TryGetPrefab(panelId, out GameObject prefab)
                ? Object.Instantiate(prefab, parent)
                : null;
        }
    }
}
