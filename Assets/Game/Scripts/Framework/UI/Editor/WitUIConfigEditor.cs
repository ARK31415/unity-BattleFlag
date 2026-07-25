using UnityEditor;
using UnityEngine;

namespace Wit.Framework.UI.Editor
{
    /// <summary>
    /// WitUIConfig 编辑器校验面板，用于提前暴露 key、prefab、View 和策略配置错误。
    /// </summary>
    [CustomEditor(typeof(WitUIConfig))]
    public sealed class WitUIConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var config = (WitUIConfig)target;
            var errors = config.ValidateDefinitions();
            if (errors.Count == 0)
            {
                EditorGUILayout.HelpBox("WitUIConfig 校验通过。", MessageType.Info);
                return;
            }

            foreach (string error in errors)
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }
        }
    }
}
