using BF.Game.Runtime.Battle.Presentation;
using BF.Game.Runtime.Battle.Units;
using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace BF.Game.Tests.EditMode.Battle
{
    /// <summary>
    /// 验证单位动画表现层的朝向规则，确保表现逻辑只读取身份和格子语义。
    /// </summary>
    public class BFUnitAnimationPresenterTests
    {
        [TearDown]
        public void TearDown()
        {
            foreach (var presenter in Object.FindObjectsByType<BFUnitAnimationPresenter>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(presenter.gameObject);
            }
        }

        [Test]
        public void ApplyInitialFacing_FlipsEnemyUnitsLeftByDefault()
        {
            var spriteRenderer = CreatePresenter(UnitFaction.Enemy, out var presenter);
            spriteRenderer.flipX = false;

            presenter.ApplyInitialFacing();

            Assert.That(spriteRenderer.flipX, Is.True);
        }

        [Test]
        public void FaceMovementStep_FlipsOnlyWhenMovingHorizontally()
        {
            var spriteRenderer = CreatePresenter(UnitFaction.Player, out var presenter);

            presenter.FaceMovementStep(new Vector2Int(1, 1), new Vector2Int(2, 1));
            Assert.That(spriteRenderer.flipX, Is.False);

            presenter.FaceMovementStep(new Vector2Int(2, 1), new Vector2Int(1, 1));
            Assert.That(spriteRenderer.flipX, Is.True);

            presenter.FaceMovementStep(new Vector2Int(1, 1), new Vector2Int(1, 2));
            Assert.That(spriteRenderer.flipX, Is.True);
        }

        [Test]
        public void FaceTarget_UsesTargetHorizontalPosition()
        {
            var spriteRenderer = CreatePresenter(UnitFaction.Player, out var presenter);

            presenter.FaceTarget(new Vector2Int(2, 1), new Vector2Int(0, 1));
            Assert.That(spriteRenderer.flipX, Is.True);

            presenter.FaceTarget(new Vector2Int(2, 1), new Vector2Int(4, 1));
            Assert.That(spriteRenderer.flipX, Is.False);
        }

        private static SpriteRenderer CreatePresenter(UnitFaction faction, out BFUnitAnimationPresenter presenter)
        {
            var gameObject = new GameObject("Unit");
            var runtime = gameObject.AddComponent<UnitRuntime>();
            runtime.Identity.Faction = faction;

            gameObject.AddComponent<Animator>();
            var spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            presenter = gameObject.AddComponent<BFUnitAnimationPresenter>();
            InvokeAwake(presenter);

            return spriteRenderer;
        }

        private static void InvokeAwake(BFUnitAnimationPresenter presenter)
        {
            var awake = typeof(BFUnitAnimationPresenter).GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(awake, Is.Not.Null);
            awake.Invoke(presenter, null);
        }
    }
}
