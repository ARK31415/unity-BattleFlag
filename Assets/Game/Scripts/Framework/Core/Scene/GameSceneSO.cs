using UnityEngine;

namespace BF.Framework.Core.Scene
{
    /// <summary>
    /// 场景资产引用与加载配置。
    /// 替代裸字符串场景名，作为场景切换的统一配置入口。
    /// </summary>
    [CreateAssetMenu(fileName = "GameSceneSO", menuName = "BF/Scene/Game Scene SO")]
    public class GameSceneSO : ScriptableObject
    {
        public enum SceneType
        {
            Boot,
            MainMenu,
            Battle,
            SystemValidation,
            Other
        }

        public enum LoadMode
        {
            Single,
            Additive
        }

        /// <summary>
        /// 场景资产引用（在 Inspector 中拖入场景资产）。
        /// </summary>
        [SerializeField] private Object _sceneAsset;

        /// <summary>
        /// 场景完整路径，由编辑器脚本在 OnValidate 中自动填充。
        /// </summary>
        [SerializeField, HideInInspector]
        private string _scenePath;

        [SerializeField] private SceneType _sceneType = SceneType.Other;
        [SerializeField] private LoadMode _loadMode = LoadMode.Single;
        [SerializeField] private bool _setActiveOnLoad = true;

        public string ScenePath => _scenePath;
        public SceneType Type => _sceneType;
        public LoadMode Mode => _loadMode;
        public bool SetActiveOnLoad => _setActiveOnLoad;

        /// <summary>
        /// 场景名称（从路径中解析）。
        /// </summary>
        public string SceneName
        {
            get
            {
                if (string.IsNullOrEmpty(_scenePath))
                    return string.Empty;
                return System.IO.Path.GetFileNameWithoutExtension(_scenePath);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_sceneAsset != null)
            {
                _scenePath = UnityEditor.AssetDatabase.GetAssetPath(_sceneAsset);
                if (!_scenePath.EndsWith(".unity"))
                {
                    _scenePath = string.Empty;
                    Debug.LogWarning($"GameSceneSO [{name}]: 引用的资产不是场景文件。");
                }
            }
            else
            {
                _scenePath = string.Empty;
            }
        }
#endif
    }
}
