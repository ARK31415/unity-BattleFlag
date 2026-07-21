using BF.Game.Runtime.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BF.Game.Tests.EditMode.Input
{
    public sealed class BFInputManagerTests
    {
        [Test]
        public void Awake_CreatesActionsInstance()
        {
            using TestInputManager testInput = new(applyStartupProfile: false);

            Assert.That(testInput.Manager.Actions, Is.Not.Null);
        }

        [Test]
        public void ApplyProfile_BattleHud_EnablesBattleGameplayHudAndGlobalMaps()
        {
            using TestInputManager testInput = new(applyStartupProfile: false);

            bool applied = testInput.Manager.ApplyProfile(BFInputProfileId.BattleHud);

            Assert.That(applied, Is.True);
            Assert.That(testInput.Manager.CurrentProfile, Is.EqualTo(BFInputProfileId.BattleHud));
            Assert.That(testInput.Manager.IsGroupEnabled(BFInputMapGroupId.BattleGameplay), Is.True);
            Assert.That(testInput.Manager.IsGroupEnabled(BFInputMapGroupId.HudUi), Is.True);
            AssertMapEnabled(testInput.Manager, "Battle", true);
            AssertMapEnabled(testInput.Manager, "BattleCamera", true);
            AssertMapEnabled(testInput.Manager, "Global", true);
            AssertMapEnabled(testInput.Manager, "UI", true);
        }

        [Test]
        public void ApplyProfile_ModalUi_BlocksBattleGameplayButKeepsUiAndGlobal()
        {
            using TestInputManager testInput = new(applyStartupProfile: false);
            testInput.Manager.ApplyProfile(BFInputProfileId.BattleHud);

            bool applied = testInput.Manager.ApplyProfile(BFInputProfileId.ModalUi);

            Assert.That(applied, Is.True);
            Assert.That(testInput.Manager.IsGroupEnabled(BFInputMapGroupId.BattleGameplay), Is.False);
            Assert.That(testInput.Manager.IsGroupEnabled(BFInputMapGroupId.ModalUi), Is.True);
            AssertMapEnabled(testInput.Manager, "Battle", false);
            AssertMapEnabled(testInput.Manager, "BattleCamera", false);
            AssertMapEnabled(testInput.Manager, "Global", true);
            AssertMapEnabled(testInput.Manager, "UI", true);
        }

        [Test]
        public void EnableGroup_ModalUiOnTopOfBattleHud_BlocksBattleButDoesNotDisableSharedGlobal()
        {
            using TestInputManager testInput = new(applyStartupProfile: false);
            testInput.Manager.ApplyProfile(BFInputProfileId.BattleHud);

            bool enabled = testInput.Manager.EnableGroup(BFInputMapGroupId.ModalUi);

            Assert.That(enabled, Is.True);
            Assert.That(testInput.Manager.IsGroupEnabled(BFInputMapGroupId.BattleGameplay), Is.False);
            Assert.That(testInput.Manager.IsGroupEnabled(BFInputMapGroupId.ModalUi), Is.True);
            AssertMapEnabled(testInput.Manager, "Battle", false);
            AssertMapEnabled(testInput.Manager, "BattleCamera", false);
            AssertMapEnabled(testInput.Manager, "Global", true);
            AssertMapEnabled(testInput.Manager, "UI", true);
        }

        [Test]
        public void DisableGroup_BattleGameplay_DisablesBattleAndCameraMaps()
        {
            using TestInputManager testInput = new(applyStartupProfile: false);
            testInput.Manager.ApplyProfile(BFInputProfileId.BattleHud);

            bool disabled = testInput.Manager.DisableGroup(BFInputMapGroupId.BattleGameplay);

            Assert.That(disabled, Is.True);
            Assert.That(testInput.Manager.IsGroupEnabled(BFInputMapGroupId.BattleGameplay), Is.False);
            AssertMapEnabled(testInput.Manager, "Battle", false);
            AssertMapEnabled(testInput.Manager, "BattleCamera", false);
            AssertMapEnabled(testInput.Manager, "Global", false);
            AssertMapEnabled(testInput.Manager, "UI", true);
        }

        [Test]
        public void Awake_DuplicateManager_DisablesDuplicate()
        {
            using TestInputManager primary = new(applyStartupProfile: false);
            var duplicateOwner = new GameObject("DuplicateBFInputManager");
            duplicateOwner.SetActive(false);
            var duplicate = duplicateOwner.AddComponent<BFInputManager>();

            LogAssert.Expect(LogType.Error, "[BFInputManager] 场景中存在多个 BFInputManager 实例，当前实例将被禁用。");

            InvokePrivate(duplicate, "Awake");

            Assert.That(BFInputManager.Instance, Is.SameAs(primary.Manager));
            Assert.That(duplicate.enabled, Is.False);
            Object.DestroyImmediate(duplicateOwner);
        }

        private static void AssertMapEnabled(BFInputManager manager, string mapName, bool expected)
        {
            var map = manager.Actions.asset.FindActionMap(mapName, false);
            Assert.That(map, Is.Not.Null);
            Assert.That(map.enabled, Is.EqualTo(expected), mapName);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }

        private static object InvokePrivate(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(
                methodName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(target, args);
        }

        private sealed class TestInputManager : System.IDisposable
        {
            private readonly GameObject _owner;
            private readonly BFInputConfig _config;

            public TestInputManager(bool applyStartupProfile)
            {
                _config = ScriptableObject.CreateInstance<BFInputConfig>();
                _config.SetTestDefinitions(
                    new[]
                    {
                        new BFInputMapGroupDefinition(
                            BFInputMapGroupId.BattleGameplay,
                            new[] { BFInputActionMapId.Battle, BFInputActionMapId.BattleCamera, BFInputActionMapId.Global }),
                        new BFInputMapGroupDefinition(
                            BFInputMapGroupId.HudUi,
                            new[] { BFInputActionMapId.UI }),
                        new BFInputMapGroupDefinition(
                            BFInputMapGroupId.ModalUi,
                            new[] { BFInputActionMapId.UI, BFInputActionMapId.Global },
                            new[] { BFInputMapGroupId.BattleGameplay })
                    },
                    new[]
                    {
                        new BFInputProfileDefinition(
                            BFInputProfileId.BattleHud,
                            new[] { BFInputMapGroupId.BattleGameplay, BFInputMapGroupId.HudUi }),
                        new BFInputProfileDefinition(
                            BFInputProfileId.ModalUi,
                            new[] { BFInputMapGroupId.ModalUi })
                    },
                    BFInputProfileId.BattleHud);

                _owner = new GameObject("BFInputManager");
                _owner.SetActive(false);
                Manager = _owner.AddComponent<BFInputManager>();
                SetPrivateField(Manager, "_config", _config);
                SetPrivateField(Manager, "_applyStartupProfileOnAwake", applyStartupProfile);
                InvokePrivate(Manager, "Awake");
            }

            public BFInputManager Manager { get; }

            public void Dispose()
            {
                if (Manager != null)
                    InvokePrivate(Manager, "OnDestroy");
                if (_owner != null)
                    Object.DestroyImmediate(_owner);
                if (_config != null)
                    Object.DestroyImmediate(_config);
            }
        }
    }
}
