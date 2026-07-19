using BF.Game.Runtime.Battle.Managers;
using BF.Game.Runtime.Battle.Units;
using NUnit.Framework;
using Pathfinding;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.TestTools;

namespace BF.Game.Tests.EditMode.Battle
{
    /// <summary>
    /// 验证棋盘初始化、A* GridGraph 接入、单位吸附和路径查询合同。
    /// </summary>
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
        public void Start_WithoutSceneScan_ScansGraph()
        {
            var boardObject = new GameObject("Board");
            var astar = boardObject.AddComponent<AstarPath>();
            var grid = astar.data.AddGraph(typeof(GridGraph)) as GridGraph;
            Assert.That(grid, Is.Not.Null);
            grid.SetDimensions(4, 4, 1f);

            var manager = boardObject.AddComponent<BFBattleBoardManager>();
            InvokeAwake(manager);
            InvokeStart(manager);

            Assert.That(manager.enabled, Is.True);
            Assert.That(grid.isScanned, Is.True);
        }

        [Test]
        public void SnapUnitsToGrid_ScansBeforeApplyingOccupancy()
        {
            var boardObject = new GameObject("Board");
            var astar = boardObject.AddComponent<AstarPath>();
            var grid = astar.data.AddGraph(typeof(GridGraph)) as GridGraph;
            Assert.That(grid, Is.Not.Null);
            grid.SetDimensions(3, 3, 1f);
            grid.center = new Vector3(1f, 1f, 0f);
            grid.is2D = true;
            grid.collision.use2D = true;
            grid.collision.heightCheck = false;

            var manager = boardObject.AddComponent<BFBattleBoardManager>();
            InvokeAwake(manager);

            var unitObject = new GameObject("Unit");
            unitObject.transform.position = new Vector3(1f, 1f, 0f);
            var unit = unitObject.AddComponent<UnitRuntime>();

            manager.SnapUnitsToGrid(new List<UnitRuntime> { unit });

            Assert.That(grid.isScanned, Is.True);
            Assert.That(unit.Grid.GridPosition, Is.EqualTo(new Vector2Int(1, 1)));
            Assert.That(manager.IsCellOccupied(new Vector2Int(1, 1)), Is.True);
        }

        [Test]
        public void GetReachableCells_UsesActualPathCostAroundOccupiedCells()
        {
            var manager = CreateScannedBoard(5, 3);
            var start = new Vector2Int(0, 1);
            var targetBehindWall = new Vector2Int(4, 1);

            manager.OccupyCell(start, "unit");
            manager.OccupyCell(new Vector2Int(1, 1), "blocker-a");
            manager.OccupyCell(new Vector2Int(2, 1), "blocker-b");
            manager.OccupyCell(new Vector2Int(3, 1), "blocker-c");

            List<Vector2Int> shortRange = manager.GetReachableCells(start, 4, "unit");
            List<Vector2Int> longRange = manager.GetReachableCells(start, 6, "unit");
            List<Vector2Int> path = manager.FindPath(start, targetBehindWall, "unit");

            Assert.That(path, Has.Count.EqualTo(6));
            Assert.That(shortRange, Has.No.Member(targetBehindWall));
            Assert.That(longRange, Has.Member(targetBehindWall));
        }

        [Test]
        public void FindPath_ReturnsEmptyWhenTargetCellIsOccupied()
        {
            var manager = CreateScannedBoard(3, 3);
            var start = new Vector2Int(0, 1);
            var occupiedTarget = new Vector2Int(1, 1);

            manager.OccupyCell(start, "unit");
            manager.OccupyCell(occupiedTarget, "blocker");

            List<Vector2Int> path = manager.FindPath(start, occupiedTarget, "unit");

            Assert.That(path, Is.Empty);
        }

        private static void InvokeAwake(BFBattleBoardManager manager)
        {
            var awake = typeof(BFBattleBoardManager).GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(awake, Is.Not.Null);
            awake.Invoke(manager, null);
        }

        private static void InvokeStart(BFBattleBoardManager manager)
        {
            var start = typeof(BFBattleBoardManager).GetMethod(
                "Start",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(start, Is.Not.Null);
            start.Invoke(manager, null);
        }

        private static BFBattleBoardManager CreateScannedBoard(int width, int height)
        {
            var boardObject = new GameObject("Board");
            var astar = boardObject.AddComponent<AstarPath>();
            var grid = astar.data.AddGraph(typeof(GridGraph)) as GridGraph;
            Assert.That(grid, Is.Not.Null);

            grid.SetDimensions(width, height, 1f);
            grid.center = new Vector3(width * 0.5f - 0.5f, height * 0.5f - 0.5f, 0f);
            grid.is2D = true;
            grid.collision.use2D = true;
            grid.collision.heightCheck = false;
            grid.neighbours = NumNeighbours.Four;
            astar.Scan();

            var manager = boardObject.AddComponent<BFBattleBoardManager>();
            InvokeAwake(manager);
            return manager;
        }
    }
}
