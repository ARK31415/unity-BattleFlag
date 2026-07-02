using System.Collections.Generic;
using UnityEngine;

namespace BF.Framework.UI.Runtime
{
    /// <summary>
    /// 管理面板标识到 Prefab 的直接映射。
    /// </summary>
    public class BFUIRegistry
    {
        private readonly Dictionary<string, GameObject> _prefabs = new();

        public void Register(string panelId, GameObject prefab)
        {
            _prefabs[panelId] = prefab;
        }

        public bool TryGetPrefab(string panelId, out GameObject prefab)
        {
            return _prefabs.TryGetValue(panelId, out prefab);
        }
    }
}
