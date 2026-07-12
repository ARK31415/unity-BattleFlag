using BF.Game.Runtime.Battle.Managers;
using NUnit.Framework;
using Pathfinding;
using System.Reflection;
using UnityEngine;
using UnityEngine.TestTools;

namespace BF.Game.Tests.EditMode.Battle
{
    public class BFBattleBoardManagerTests
    {
        [TearDown]
        public void TearDown()
        {
            foreach (var manager in Object.FindObjectsByType<BFBattleBoardManager>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(manager.gameObject);
            }

            foreach (var astar in Object.FindObjectsByType<AstarPath>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(astar.gameObject);
            }
        }

        [Test]
        public void Awake_PreservesSceneAuthoredGridSettings()
        {
            var boardObject = new GameObject("Board");
            var astar = boardObject.AddComponent<AstarPath>();
            var grid = astar.data.AddGraph(typeof(GridGraph)) as GridGraph;
            Assert.That(grid, Is.Not.Null);

            grid.SetDimensions(7, 6, 0.75f);
            grid.center = new Vector3(13f, 9f, 0f);
            astar.Scan();

            var manager = boardObject.AddComponent<BFBattleBoardManager>();
            InvokeAwake(manager);

            Assert.That(manager.Grid, Is.SameAs(grid));
            Assert.That(manager.Width, Is.EqualTo(7));
            Assert.That(manager.Height, Is.EqualTo(6));
            Assert.That(grid.center, Is.EqualTo(new Vector3(13f, 9f, 0f)));
            Assert.That(grid.nodeSize, Is.EqualTo(0.75f));
        }

        [Test]
        public void Awake_WithoutAstarPath_DisablesManagerWithoutCreatingFallback()
        {
            LogAssert.Expect(LogType.Error, "[BFBattleBoardManager] Scene is missing an AstarPath component.");
            var boardObject = new GameObject("Board");

            var manager = boardObject.AddComponent<BFBattleBoardManager>();
            InvokeAwake(manager);

            Assert.That(manager.enabled, Is.False);
            Assert.That(Object.FindAnyObjectByType<AstarPath>(), Is.Null);
        }

        [Test]
        public void Awake_WithoutGridGraph_DisablesManagerWithoutCreatingFallback()
        {
            LogAssert.Expect(LogType.Error, "[BFBattleBoardManager] Scene AstarPath has no GridGraph.");
            var boardObject = new GameObject("Board");
            var astar = boardObject.AddComponent<AstarPath>();

            var manager = boardObject.AddComponent<BFBattleBoardManager>();
            InvokeAwake(manager);

            Assert.That(manager.enabled, Is.False);
            Assert.That(astar.data.gridGraph, Is.Null);
        }

        [Test]
        public void Awake_WithoutSceneScan_DisablesManagerWithoutScanningGraph()
        {
            LogAssert.Expect(LogType.Error, "[BFBattleBoardManager] Scene GridGraph is not scanned. Enable A* scene scanning and save the scene graph.");
            var boardObject = new GameObject("Board");
            var astar = boardObject.AddComponent<AstarPath>();
            var grid = astar.data.AddGraph(typeof(GridGraph)) as GridGraph;
            Assert.That(grid, Is.Not.Null);

            var manager = boardObject.AddComponent<BFBattleBoardManager>();
            InvokeAwake(manager);

            Assert.That(manager.enabled, Is.False);
            Assert.That(grid.isScanned, Is.False);
        }

        private static void InvokeAwake(BFBattleBoardManager manager)
        {
            var awake = typeof(BFBattleBoardManager).GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(awake, Is.Not.Null);
            awake.Invoke(manager, null);
        }
    }
}
