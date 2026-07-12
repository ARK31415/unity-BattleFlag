using Pathfinding;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BF.Game.Editor.Battle
{
    internal static class BFSceneGridAuthoringTools
    {
        [MenuItem("BattleFlag/Align Test Grid To Map")]
        private static void AlignTestGridToMap()
        {
            var board = GameObject.Find("BFBattleBoard");
            if (board == null)
            {
                Debug.LogError("[BFSceneGridAuthoring] BFBattleBoard was not found in the open scene.");
                return;
            }

            var astar = board.GetComponent<AstarPath>();
            var grid = astar != null ? astar.data.gridGraph : null;
            if (grid == null)
            {
                Debug.LogError("[BFSceneGridAuthoring] BFBattleBoard has no GridGraph.");
                return;
            }

            Undo.RecordObject(astar, "Align Battle Grid To Map");
            grid.center = new Vector3(5f, 4f, 0f);
            grid.collision.use2D = true;
            grid.neighbours = NumNeighbours.Four;
            grid.cutCorners = false;
            astar.Scan();
            astar.data.SetData(astar.data.SerializeGraphs());

            EditorUtility.SetDirty(astar);
            EditorSceneManager.MarkSceneDirty(board.scene);
            Debug.Log("[BFSceneGridAuthoring] Test GridGraph aligned to map at center (5, 4, 0).");
        }
    }
}
