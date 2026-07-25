using System.Reflection;
using BF.Game.Runtime.Battle;
using BF.Game.Runtime.UI.Battle;
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
        public void BattleHudView_OpenDoesNotScanSceneCanvasesForButton()
        {
            _uiRootCanvas = CreateCanvas("Canvas");
            _battleCanvas = CreateCanvas("BattleCanvas");
            CreateButton("EndTurnButton", _battleCanvas.transform);

            _hudOwner = new GameObject("BFBattleRoot");
            var hud = _hudOwner.AddComponent<BattleHudView>();
            var definition = new Wit.Framework.UI.WitUIWindowDefinition(
                "battle.hud",
                _hudOwner,
                Wit.Framework.UI.WitUILayer.HUD,
                Wit.Framework.UI.WitUICachePolicy.DestroyOnClose,
                true,
                false);

            hud.Open("battle.hud", new BattleHudContext(), definition);

            Assert.That(GetPrivateField(hud, "_endTurnButton"), Is.Null);
        }

        [Test]
        public void BattleResultPopupView_OpenAndReopenRefreshesResultText()
        {
            _hudOwner = new GameObject("BattleResultPopup");
            var popup = _hudOwner.AddComponent<BattleResultPopupView>();
            var resultText = CreateText("ResultText", _hudOwner.transform);
            SetPrivateField(popup, "_resultText", resultText);
            var definition = new Wit.Framework.UI.WitUIWindowDefinition(
                "battle.result",
                _hudOwner,
                Wit.Framework.UI.WitUILayer.Popup,
                Wit.Framework.UI.WitUICachePolicy.CacheOnClose,
                true,
                true);

            popup.Open("battle.result", new BattleResultContext(BattleResult.Victory("test", 3)), definition);
            popup.Reopen(new BattleResultContext(BattleResult.Defeat("test", 4)));

            Assert.That(resultText.text, Is.EqualTo("DEFEAT"));
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

        private static Text CreateText(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go.AddComponent<Text>();
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return field.GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }
    }
}
