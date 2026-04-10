using UnityEditor;
using UnityEngine;

namespace Islands.EditorTools
{
    [CustomEditor(typeof(IslandWater))]
    public sealed class IslandWaterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var water = (IslandWater)target;
            IslandShapeEditorUtility.EnsureDefaultWaterMaterial(water);

            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour(water), typeof(MonoScript), false);
            }

            EditorGUILayout.Space();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            if (GUILayout.Button("Rebuild Shoreline"))
            {
                water.QueueRebuildShoreline();
                EditorUtility.SetDirty(water);
            }
        }
    }
}
