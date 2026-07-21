using System;
using UnityEngine;

namespace Wit.Framework.UI
{
    /// <summary>
    /// 单个 UI 窗口的配置定义，包含 public key、prefab 引用、层级、缓存策略和模态策略。
    /// 该类作为配置数据容器，不保存运行时状态。
    /// </summary>
    [Serializable]
    public sealed class WitUIWindowDefinition
    {
        [SerializeField] private string _key;
        [SerializeField] private GameObject _prefab;
        [SerializeField] private WitUILayer _layer;
        [SerializeField] private WitUICachePolicy _cachePolicy;
        [SerializeField] private bool _unique;
        [SerializeField] private bool _modal;

        public string Key => _key;
        public GameObject Prefab => _prefab;
        public WitUILayer Layer => _layer;
        public WitUICachePolicy CachePolicy => _cachePolicy;
        public bool Unique => _unique;
        public bool Modal => _modal;

        public WitUIWindowDefinition()
        {
            _key = string.Empty;
            _layer = WitUILayer.Screen;
            _cachePolicy = WitUICachePolicy.DestroyOnClose;
            _unique = true;
        }

        public WitUIWindowDefinition(string key, GameObject prefab, WitUILayer layer,
            WitUICachePolicy cachePolicy, bool unique, bool modal)
        {
            _key = key;
            _prefab = prefab;
            _layer = layer;
            _cachePolicy = cachePolicy;
            _unique = unique;
            _modal = modal;
        }
    }
}
