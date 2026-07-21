using BF.Game.Runtime.Input;
using NUnit.Framework;
using UnityEngine;

namespace BF.Game.Tests.EditMode.Input
{
    public sealed class BFInputConfigTests
    {
        [Test]
        public void TryGetGroup_BattleGameplay_ReturnsCorrectMaps()
        {
            var config = ScriptableObject.CreateInstance<BFInputConfig>();
            config.SetTestDefinitions(
                new[]
                {
                    new BFInputMapGroupDefinition(BFInputMapGroupId.BattleGameplay,
                        new[] { BFInputActionMapId.Battle, BFInputActionMapId.BattleCamera, BFInputActionMapId.Global }),
                    new BFInputMapGroupDefinition(BFInputMapGroupId.HudUi,
                        new[] { BFInputActionMapId.UI }),
                    new BFInputMapGroupDefinition(BFInputMapGroupId.ModalUi,
                        new[] { BFInputActionMapId.UI, BFInputActionMapId.Global },
                        new[] { BFInputMapGroupId.BattleGameplay })
                },
                new[]
                {
                    new BFInputProfileDefinition(BFInputProfileId.BattleHud,
                        new[] { BFInputMapGroupId.BattleGameplay, BFInputMapGroupId.HudUi }),
                    new BFInputProfileDefinition(BFInputProfileId.ModalUi,
                        new[] { BFInputMapGroupId.ModalUi })
                },
                BFInputProfileId.BattleHud);

            Assert.That(config.TryGetGroup(BFInputMapGroupId.BattleGameplay, out var g), Is.True);
            Assert.That(g.ActionMaps, Contains.Item(BFInputActionMapId.Battle));
            Assert.That(g.ActionMaps, Contains.Item(BFInputActionMapId.BattleCamera));
            Assert.That(g.ActionMaps, Contains.Item(BFInputActionMapId.Global));

            Object.DestroyImmediate(config);
        }

        [Test]
        public void TryGetGroup_MissingId_ReturnsFalse()
        {
            var config = ScriptableObject.CreateInstance<BFInputConfig>();
            config.SetTestDefinitions(
                new[] { new BFInputMapGroupDefinition(BFInputMapGroupId.HudUi, new[] { BFInputActionMapId.UI }) },
                null, BFInputProfileId.BattleHud);

            Assert.That(config.TryGetGroup(BFInputMapGroupId.BattleGameplay, out _), Is.False);

            Object.DestroyImmediate(config);
        }

        [Test]
        public void TryGetProfile_ModalUi_ReturnsEnabledGroups()
        {
            var config = ScriptableObject.CreateInstance<BFInputConfig>();
            config.SetTestDefinitions(
                new[]
                {
                    new BFInputMapGroupDefinition(BFInputMapGroupId.ModalUi,
                        new[] { BFInputActionMapId.UI, BFInputActionMapId.Global },
                        new[] { BFInputMapGroupId.BattleGameplay })
                },
                new[]
                {
                    new BFInputProfileDefinition(BFInputProfileId.ModalUi,
                        new[] { BFInputMapGroupId.ModalUi })
                },
                BFInputProfileId.BattleHud);

            Assert.That(config.TryGetProfile(BFInputProfileId.ModalUi, out var p), Is.True);
            Assert.That(p.EnabledGroups, Contains.Item(BFInputMapGroupId.ModalUi));

            Object.DestroyImmediate(config);
        }

        [Test]
        public void TryGetProfile_MissingId_ReturnsFalse()
        {
            var config = ScriptableObject.CreateInstance<BFInputConfig>();

            Assert.That(config.TryGetProfile(BFInputProfileId.ModalUi, out _), Is.False);

            Object.DestroyImmediate(config);
        }

        [Test]
        public void StartupProfile_ReturnsConfiguredValue()
        {
            var config = ScriptableObject.CreateInstance<BFInputConfig>();
            config.SetTestDefinitions(null, null, BFInputProfileId.BattleHud);

            Assert.That(config.StartupProfile, Is.EqualTo(BFInputProfileId.BattleHud));

            Object.DestroyImmediate(config);
        }
    }
}
