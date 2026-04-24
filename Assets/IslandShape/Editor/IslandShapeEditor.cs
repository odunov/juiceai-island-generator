using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace Islands.EditorTools
{
    [CustomEditor(typeof(IslandShape))]
    public sealed class IslandShapeEditor : Editor
    {
        private const float WorkflowStatusHeight = 40f;

        private SerializedProperty depthProperty;
        private SerializedProperty spacingProperty;
        private SerializedProperty linkedWaterProperty;
        private SerializedProperty generateColliderProperty;
        private SerializedProperty minimumAreaProperty;
        private SerializedProperty duplicatePointToleranceProperty;

        private void OnEnable()
        {
            depthProperty = serializedObject.FindProperty("depth");
            spacingProperty = serializedObject.FindProperty("spacing");
            linkedWaterProperty = serializedObject.FindProperty("linkedWater");
            generateColliderProperty = serializedObject.FindProperty("generateCollider");
            minimumAreaProperty = serializedObject.FindProperty("minimumArea");
            duplicatePointToleranceProperty = serializedObject.FindProperty("duplicatePointTolerance");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawScriptReference();
            EditorGUILayout.Space();

            if (targets.Length == 1)
            {
                DrawSingleIslandInspector((IslandShape)target);
            }
            else
            {
                DrawMultiSelectionInspector();
            }

            EditorGUILayout.Space();
            DrawMeshSettings();
            EditorGUILayout.Space();
            DrawWaterSettings();
        }

        private void DrawScriptReference()
        {
            using (new EditorGUI.DisabledScope(true))
            {
                var island = target as IslandShape;
                var script = island != null ? MonoScript.FromMonoBehaviour(island) : null;
                EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), false);
            }
        }

        private void DrawMeshSettings()
        {
            EditorGUILayout.LabelField("Mesh", EditorStyles.boldLabel);
            using (var changed = new EditorGUI.ChangeCheckScope())
            {
                EditorGUILayout.PropertyField(depthProperty);
                EditorGUILayout.PropertyField(
                    spacingProperty,
                    new GUIContent("Spacing", "Controls both outline sampling density and the target triangle size for the refined top surface."));
                EditorGUILayout.PropertyField(generateColliderProperty);
                EditorGUILayout.PropertyField(minimumAreaProperty);
                EditorGUILayout.PropertyField(duplicatePointToleranceProperty);

                serializedObject.ApplyModifiedProperties();

                if (changed.changed)
                {
                    foreach (var targetObject in targets.Cast<IslandShape>())
                    {
                        targetObject.RebuildImmediate();
                        EditorUtility.SetDirty(targetObject);
                    }
                }
            }
        }

        private void DrawWaterSettings()
        {
            EditorGUILayout.LabelField("Water", EditorStyles.boldLabel);
            using (var changed = new EditorGUI.ChangeCheckScope())
            {
                EditorGUILayout.PropertyField(
                    linkedWaterProperty,
                    new GUIContent("Linked Water", "Optional IslandWater component to notify whenever this island mesh changes."));

                serializedObject.ApplyModifiedProperties();

                if (changed.changed)
                {
                    foreach (var targetObject in targets.Cast<IslandShape>())
                    {
                        targetObject.RebuildImmediate();
                        EditorUtility.SetDirty(targetObject);
                    }
                }
            }
        }

        private static void DrawSingleIslandInspector(IslandShape island)
        {
            island.EnsureSetup();
            island.EnsureSplineExists();
            IslandShapeEditorUtility.EnsureDefaultMaterial(island);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Island Shape Tool", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(GetWorkflowSummary(island), EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.Space(2f);
                DrawWorkflowStatus(island);

                EditorGUILayout.Space(4f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawStartOrFocusButton(island);

                    using (new EditorGUI.DisabledScope(island.Spline == null || island.Spline.Count < 3 || island.HasClosedSpline))
                    {
                        if (GUILayout.Button("Close Loop"))
                        {
                            Undo.RecordObject(island, "Close Island Loop");
                            if (island.SplineContainer != null)
                            {
                                Undo.RecordObject(island.SplineContainer, "Close Island Loop");
                            }

                            island.CloseLoop();
                            EditorUtility.SetDirty(island);
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Rebuild"))
                    {
                        island.RebuildImmediate();
                        EditorUtility.SetDirty(island);
                    }

                    using (new EditorGUI.DisabledScope(!island.IsDrawing && (island.Spline == null || island.Spline.Count == 0 || island.HasClosedSpline)))
                    {
                        if (GUILayout.Button("Cancel Drawing"))
                        {
                            Undo.RecordObject(island, "Cancel Island Drawing");
                            if (island.SplineContainer != null)
                            {
                                Undo.RecordObject(island.SplineContainer, "Cancel Island Drawing");
                            }

                            island.CancelDrawing();
                            EditorUtility.SetDirty(island);
                        }
                    }
                }

                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField("Shortcuts: Space closes, Right Click deletes the last knot, Esc cancels drawing.", EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.Space();
            DrawKnotInspector(island);
        }

        private static void DrawWorkflowStatus(IslandShape island)
        {
            var (message, messageType) = GetWorkflowStatus(island);
            var rect = EditorGUILayout.GetControlRect(false, WorkflowStatusHeight);
            EditorGUI.HelpBox(rect, message, messageType);
        }

        private static void DrawStartOrFocusButton(IslandShape island)
        {
            var startLabel = island.Spline.Count == 0 || !island.HasClosedSpline ? "Start Drawing" : "Edit Outline";
            if (!GUILayout.Button(startLabel))
            {
                return;
            }

            if (island.HasClosedSpline && !island.IsDrawing)
            {
                IslandShapeEditorUtility.ActivateTool(island);
                return;
            }

            Undo.RecordObject(island, "Start Island Drawing");
            if (island.SplineContainer != null)
            {
                Undo.RecordObject(island.SplineContainer, "Start Island Drawing");
            }

            island.BeginDrawing();
            IslandShapeEditorUtility.ActivateTool(island);
            EditorUtility.SetDirty(island);
        }

        private static string GetWorkflowSummary(IslandShape island)
        {
            if (island.IsDrawing)
            {
                return "Scene view drawing is active. Left click adds knots on the XZ plane, right click removes the last knot, and Space closes the outline.";
            }

            if (island.HasClosedSpline)
            {
                return "Use Edit Outline to reshape the island in the Scene view. The mesh preserves the drawn boundary, refines the top triangulation, and extrudes straight down.";
            }

            if (island.Spline != null && island.Spline.Count > 0)
            {
                return "The outline is still open. Add more knots in the Scene view, then close the loop to generate the mesh.";
            }

            return "Start drawing in the Scene view to place a new island outline from any camera angle.";
        }

        private static (string message, MessageType messageType) GetWorkflowStatus(IslandShape island)
        {
            if (island.IsDrawing)
            {
                return ("Drawing mode is active. The mesh will validate after you close the loop.", MessageType.Info);
            }

            if (!string.IsNullOrEmpty(island.LastValidationMessage))
            {
                return (island.LastValidationMessage, MessageType.Warning);
            }

            if (island.HasClosedSpline)
            {
                return ("Outline valid. Mesh updates live while you edit.", MessageType.Info);
            }

            return ("No validation yet. Close the loop to generate and validate the island mesh.", MessageType.None);
        }

        private static void DrawMultiSelectionInspector()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Island Shape Tool", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Multi-selection supports the mesh settings below. Outline editing is available when a single island is selected.", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.Space(2f);
                var rect = EditorGUILayout.GetControlRect(false, WorkflowStatusHeight);
                EditorGUI.HelpBox(rect, "Select a single island to see drawing status and validation feedback.", MessageType.None);
            }
        }

        private static void DrawKnotInspector(IslandShape island)
        {
            var spline = island.Spline;
            if (spline == null || spline.Count == 0 || island.IsDrawing)
            {
                return;
            }

            EditorGUILayout.LabelField("Selected Knot", EditorStyles.boldLabel);

            var selectedIndex = island.SelectedKnotIndex;
            if (selectedIndex < 0 || selectedIndex >= spline.Count)
            {
                selectedIndex = Mathf.Clamp(spline.Count - 1, 0, spline.Count - 1);
                island.SelectedKnotIndex = selectedIndex;
            }

            var options = Enumerable.Range(0, spline.Count).Select(index => $"Knot {index}").ToArray();
            var newSelectedIndex = EditorGUILayout.Popup("Knot", selectedIndex, options);
            if (newSelectedIndex != selectedIndex)
            {
                island.SelectedKnotIndex = newSelectedIndex;
                SceneView.RepaintAll();
            }

            var knot = spline[island.SelectedKnotIndex];
            var knotPosition = knot.Position;
            EditorGUILayout.Vector3Field("Position", new Vector3(knotPosition.x, knotPosition.y, knotPosition.z));

            var tangentMode = spline.GetTangentMode(island.SelectedKnotIndex);
            var newTangentMode = (TangentMode)EditorGUILayout.EnumPopup("Tangent Mode", tangentMode);
            if (newTangentMode != tangentMode)
            {
                Undo.RecordObject(island.SplineContainer, "Change Island Tangent Mode");
                island.SetKnotTangentMode(island.SelectedKnotIndex, newTangentMode);
                EditorUtility.SetDirty(island.SplineContainer);
                SceneView.RepaintAll();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Focus Tool"))
                {
                    IslandShapeEditorUtility.ActivateTool(island);
                }

                if (GUILayout.Button("Delete Selected Knot"))
                {
                    Undo.RecordObject(island.SplineContainer, "Delete Island Knot");
                    island.DeleteKnot(island.SelectedKnotIndex);
                    EditorUtility.SetDirty(island.SplineContainer);
                    SceneView.RepaintAll();
                }
            }
        }
    }
}
