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
    }
}
