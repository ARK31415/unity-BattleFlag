using System.Reflection;
using BF.Game.Runtime.Battle.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace BF.Game.Tests.EditMode.Battle
{
    public sealed class BFBattleHUDTests
    {
        private GameObject _uiRootCanvas;
        private GameObject _battleCanvas;
        private GameObject _hudOwner;

        [TearDown]
        public void TearDown()
        {
            if (_hudOwner != null)
                Object.DestroyImmediate(_hudOwner);
            if (_battleCanvas != null)
                Object.DestroyImmediate(_battleCanvas);
            if (_uiRootCanvas != null)
                Object.DestroyImmediate(_uiRootCanvas);
        }

        [Test]
        public void AutoResolveReferences_FindsEndTurnButtonWhenFirstCanvasIsNotBattleCanvas()
        {
            _uiRootCanvas = CreateCanvas("Canvas");
            _battleCanvas = CreateCanvas("BattleCanvas");
            var endTurnButton = CreateButton("EndTurnButton", _battleCanvas.transform);

            _hudOwner = new GameObject("BFBattleRoot");
            var hud = _hudOwner.AddComponent<BFBattleHUD>();

            InvokePrivate(hud, "AutoResolveReferences");

            Assert.That(GetPrivateField(hud, "_endTurnButton"), Is.SameAs(endTurnButton));
        }

        private static GameObject CreateCanvas(string name)
        {
            var go = new GameObject(name);
            go.AddComponent<RectTransform>();
            go.AddComponent<Canvas>();
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        private static Button CreateButton(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            go.AddComponent<CanvasRenderer>();
            go.AddComponent<Image>();
            return go.AddComponent<Button>();
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return field.GetValue(target);
        }

        private static object InvokePrivate(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(target, args);
        }
    }
}
