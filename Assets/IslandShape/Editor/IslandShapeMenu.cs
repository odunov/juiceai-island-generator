using UnityEditor;
using UnityEngine;

namespace Islands.EditorTools
{
    public static class IslandShapeMenu
    {
        [MenuItem("GameObject/3D Object/Island Shape", false, 11)]
        private static void CreateIslandShape(MenuCommand menuCommand)
        {
            var gameObject = new GameObject("Island Shape");
            GameObjectUtility.SetParentAndAlign(gameObject, menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create Island Shape");

            var islandShape = Undo.AddComponent<IslandShape>(gameObject);
            islandShape.BeginDrawing();
            IslandShapeEditorUtility.EnsureDefaultMaterial(islandShape);
            IslandShapeEditorUtility.ActivateTool(islandShape);
        }

        [MenuItem("GameObject/3D Object/Island Water", false, 12)]
        private static void CreateIslandWater(MenuCommand menuCommand)
        {
            var gameObject = new GameObject("Island Water");
            GameObjectUtility.SetParentAndAlign(gameObject, menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create Island Water");

            var islandWater = Undo.AddComponent<IslandWater>(gameObject);
            IslandShapeEditorUtility.EnsureDefaultWaterMaterial(islandWater);
            Selection.activeGameObject = gameObject;
        }
    }
}
