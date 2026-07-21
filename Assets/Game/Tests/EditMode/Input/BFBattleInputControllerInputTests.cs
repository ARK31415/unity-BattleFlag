using System.IO;
using System.Reflection;
using BF.Game.Runtime.Battle.PlayerInput;
using BF.Game.Runtime.Input;
using NUnit.Framework;
using UnityEngine;

namespace BF.Game.Tests.EditMode.Input
{
    public sealed class BFBattleInputControllerInputTests
    {
        [Test]
        public void Source_DoesNotUseOldInputContextManagerOrStringKeys()
        {
            string path = "Assets/Game/Scripts/BattleFlag/Runtime/Battle/PlayerInput/BFBattleInputController.cs";
            string source = File.ReadAllText(path);

            Assert.That(source, Does.Not.Contain("WitInputContextManager"));
            Assert.That(source, Does.Not.Contain("WitInputActionSubscription"));
            Assert.That(source, Does.Not.Contain("BFBattleFlagInputKeys"));
            Assert.That(source, Does.Contain("BFInputManager"));
        }

        [Test]
        public void Start_RetriesInputRegistrationWhenManagerAwakeRunsAfterOnEnable()
        {
            var config = ScriptableObject.CreateInstance<BFInputConfig>();
            config.SetTestDefinitions(
                new[]
                {
                    new BFInputMapGroupDefinition(BFInputMapGroupId.BattleGameplay,
                        new[] { BFInputActionMapId.Battle })
                },
                new[]
                {
                    new BFInputProfileDefinition(BFInputProfileId.BattleHud,
                        new[] { BFInputMapGroupId.BattleGameplay })
                },
                BFInputProfileId.BattleHud);

            var managerOwner = new GameObject("BFInputManager");
            var manager = managerOwner.AddComponent<BFInputManager>();
            SetPrivateField(manager, "_config", config);
            SetPrivateField(manager, "_applyStartupProfileOnAwake", false);

            var controllerOwner = new GameObject("BFBattleInputController");
            var controller = controllerOwner.AddComponent<BFBattleInputController>();
            SetPrivateField(controller, "_inputManager", manager);

            InvokePrivate(controller, "OnEnable");
            Assert.That(GetPrivateField(controller, "_selectAction"), Is.Null);

            InvokePrivate(manager, "Awake");
            InvokePrivate(controller, "Start");

            Assert.That(GetPrivateField(controller, "_selectAction"), Is.Not.Null);

            InvokePrivate(controller, "OnDisable");
            InvokePrivate(manager, "OnDestroy");
            Object.DestroyImmediate(controllerOwner);
            Object.DestroyImmediate(managerOwner);
            Object.DestroyImmediate(config);
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
