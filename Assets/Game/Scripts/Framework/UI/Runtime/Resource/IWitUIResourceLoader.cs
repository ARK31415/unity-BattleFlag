using UnityEngine;

namespace Wit.Framework.UI
{
    /// <summary>
    /// UI 资源加载边界预留接口。本期默认仍使用 WitUIWindowDefinition.Prefab 直连。
    /// </summary>
    public interface IWitUIResourceLoader
    {
        bool TryLoad(WitUIWindowDefinition definition, out GameObject prefab, out string error);
    }
}
