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
        private SerializedProperty terraceCountProperty;
        private SerializedProperty terraceStepWidthProperty;
        private SerializedProperty terraceWidthBiasProperty;
        private SerializedProperty terraceDepthBiasProperty;
        private SerializedProperty terraceSoftnessProperty;
        private SerializedProperty spacingProperty;
        private SerializedProperty coastBandWidthProperty;
        private SerializedProperty generateColliderProperty;
        private SerializedProperty minimumAreaProperty;
        private SerializedProperty duplicatePointToleranceProperty;

        private void OnEnable()
        {
            depthProperty = serializedObject.FindProperty("depth");
            terraceCountProperty = serializedObject.FindProperty("terraceCount");
            terraceStepWidthProperty = serializedObject.FindProperty("terraceStepWidth");
            terraceWidthBiasProperty = serializedObject.FindProperty("terraceWidthBias");
            terraceDepthBiasProperty = serializedObject.FindProperty("terraceDepthBias");
            terraceSoftnessProperty = serializedObject.FindProperty("terraceSoftness");
            spacingProperty = serializedObject.FindProperty("spacing");
            coastBandWidthProperty = serializedObject.FindProperty("coastBandWidth");
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
                EditorGUILayout.PropertyField(terraceCountProperty);
                EditorGUILayout.PropertyField(terraceStepWidthProperty);
                EditorGUILayout.PropertyField(terraceWidthBiasProperty);
                EditorGUILayout.PropertyField(terraceDepthBiasProperty);
                EditorGUILayout.PropertyField(terraceSoftnessProperty);
                EditorGUILayout.PropertyField(
                    spacingProperty,
                    new GUIContent("Spacing", "Target world-space distance between base silhouette points along the island outline."));
                EditorGUILayout.PropertyField(
                    coastBandWidthProperty,
                    new GUIContent("Coast Band Width", "Default width of the structured top-surface coast band before local edge-zone adjustments."));
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
                    DrawEdgeZoneModeButton(island);

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
            EditorGUILayout.Space();
            DrawEdgeZoneInspector(island);
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
                island.ToolMode = IslandToolMode.Outline;
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

        private static void DrawEdgeZoneModeButton(IslandShape island)
        {
            using (new EditorGUI.DisabledScope(!island.HasClosedSpline))
            {
                if (!GUILayout.Button("Edit Edge Zones"))
                {
                    return;
                }

                island.ToolMode = IslandToolMode.EdgeZones;
                if (island.EdgeZones.Count > 0 && island.SelectedEdgeZoneIndex < 0)
                {
                    island.SelectedEdgeZoneIndex = 0;
                }

                IslandShapeEditorUtility.ActivateTool(island);
                EditorUtility.SetDirty(island);
            }
        }

        private static string GetWorkflowSummary(IslandShape island)
        {
            if (island.IsDrawing)
            {
                return "Scene view drawing is active. Left click adds knots on the XZ plane, right click removes the last knot, and Space closes the outline.";
            }

            if (island.HasClosedSpline)
            {
                if (island.ToolMode == IslandToolMode.EdgeZones)
                {
                    return "Use Edit Edge Zones to shape the derived coastline. Drag zone center and arc handles in the Scene view, then tweak zone values below.";
                }

                return "Use Focus Tool to edit the outline in the Scene view. Drag knots or tangents to reshape the island.";
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

            if (island.HasClosedSpline && island.ToolMode == IslandToolMode.EdgeZones)
            {
                return ("Edge-zone mode is active. The mesh is showing the sculpted coastline and coast-band topology.", MessageType.Info);
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
                EditorGUILayout.LabelField("Multi-selection supports the mesh settings below. Knot and tangent editing are available when a single island is selected.", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.Space(2f);
                var rect = EditorGUILayout.GetControlRect(false, WorkflowStatusHeight);
                EditorGUI.HelpBox(rect, "Select a single island to see drawing status and validation feedback.", MessageType.None);
            }
        }

        private static void DrawKnotInspector(IslandShape island)
        {
            var spline = island.Spline;
            if (spline == null || spline.Count == 0 || island.ToolMode == IslandToolMode.EdgeZones)
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

        private static void DrawEdgeZoneInspector(IslandShape island)
        {
            if (!island.HasClosedSpline)
            {
                return;
            }

            EditorGUILayout.LabelField("Edge Zones", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Shape broad coastline character without touching the macro spline. Zones affect silhouette offset, coast-band width, and local terrace feel.", EditorStyles.wordWrappedMiniLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Add Zone"))
                    {
                        Undo.RecordObject(island, "Add Edge Zone");
                        island.AddEdgeZone();
                        EditorUtility.SetDirty(island);
                        IslandShapeEditorUtility.ActivateTool(island);
                    }

                    using (new EditorGUI.DisabledScope(island.EdgeZones.Count == 0 || island.SelectedEdgeZoneIndex < 0))
                    {
                        if (GUILayout.Button("Remove Selected"))
                        {
                            Undo.RecordObject(island, "Remove Edge Zone");
                            island.RemoveEdgeZone(island.SelectedEdgeZoneIndex);
                            EditorUtility.SetDirty(island);
                        }
                    }
                }

                if (island.EdgeZones.Count == 0)
                {
                    EditorGUILayout.Space(2f);
                    EditorGUILayout.HelpBox("No edge zones yet. Add one to start sculpting coastline sections.", MessageType.None);
                    return;
                }

                DrawEdgeZoneList(island);
                EditorGUILayout.Space(4f);
                DrawSelectedEdgeZoneDetails(island);
            }
        }

        private static void DrawEdgeZoneList(IslandShape island)
        {
            for (var zoneIndex = 0; zoneIndex < island.EdgeZones.Count; zoneIndex++)
            {
                var edgeZone = island.GetEdgeZone(zoneIndex);
                using (new EditorGUILayout.HorizontalScope())
                {
                    var label = $"{zoneIndex + 1}. {edgeZone.PresetLabel}";
                    if (GUILayout.Toggle(zoneIndex == island.SelectedEdgeZoneIndex, label, "Button"))
                    {
                        if (island.SelectedEdgeZoneIndex != zoneIndex)
                        {
                            island.SelectedEdgeZoneIndex = zoneIndex;
                            island.ToolMode = IslandToolMode.EdgeZones;
                            SceneView.RepaintAll();
                        }
                    }

                    using (new EditorGUI.DisabledScope(zoneIndex == 0))
                    {
                        if (GUILayout.Button("Up", GUILayout.Width(40f)))
                        {
                            Undo.RecordObject(island, "Move Edge Zone");
                            island.MoveEdgeZone(zoneIndex, zoneIndex - 1);
                            EditorUtility.SetDirty(island);
                            return;
                        }
                    }

                    using (new EditorGUI.DisabledScope(zoneIndex >= island.EdgeZones.Count - 1))
                    {
                        if (GUILayout.Button("Down", GUILayout.Width(52f)))
                        {
                            Undo.RecordObject(island, "Move Edge Zone");
                            island.MoveEdgeZone(zoneIndex, zoneIndex + 1);
                            EditorUtility.SetDirty(island);
                            return;
                        }
                    }
                }
            }
        }

        private static void DrawSelectedEdgeZoneDetails(IslandShape island)
        {
            var selectedZoneIndex = Mathf.Clamp(island.SelectedEdgeZoneIndex, 0, island.EdgeZones.Count - 1);
            island.SelectedEdgeZoneIndex = selectedZoneIndex;

            var edgeZone = island.GetEdgeZone(selectedZoneIndex);
            using (var changed = new EditorGUI.ChangeCheckScope())
            {
                edgeZone.CenterNormalized = EditorGUILayout.Slider("Center", edgeZone.CenterNormalized, 0f, 1f);
                edgeZone.SpanNormalized = EditorGUILayout.Slider("Span", edgeZone.SpanNormalized, 0.02f, 1f);
                edgeZone.SilhouetteOffset = EditorGUILayout.FloatField("Silhouette Offset", edgeZone.SilhouetteOffset);
                edgeZone.CoastBandWidthDelta = EditorGUILayout.FloatField("Coast Width Delta", edgeZone.CoastBandWidthDelta);
                edgeZone.TerraceWidthScale = EditorGUILayout.FloatField("Terrace Width Scale", edgeZone.TerraceWidthScale);
                edgeZone.TerraceSoftnessScale = EditorGUILayout.FloatField("Terrace Softness Scale", edgeZone.TerraceSoftnessScale);

                if (changed.changed)
                {
                    Undo.RecordObject(island, "Edit Edge Zone");
                    island.SetEdgeZone(selectedZoneIndex, edgeZone);
                    EditorUtility.SetDirty(island);
                }
            }

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Presets", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawPresetButton(island, selectedZoneIndex, IslandEdgeZonePreset.Neutral);
                DrawPresetButton(island, selectedZoneIndex, IslandEdgeZonePreset.Beach);
                DrawPresetButton(island, selectedZoneIndex, IslandEdgeZonePreset.Cliff);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawPresetButton(island, selectedZoneIndex, IslandEdgeZonePreset.Cove);
                DrawPresetButton(island, selectedZoneIndex, IslandEdgeZonePreset.Point);
            }
        }

        private static void DrawPresetButton(IslandShape island, int selectedZoneIndex, IslandEdgeZonePreset preset)
        {
            if (!GUILayout.Button(preset.ToString()))
            {
                return;
            }

            Undo.RecordObject(island, "Apply Edge Zone Preset");
            island.ApplyEdgeZonePreset(selectedZoneIndex, preset);
            EditorUtility.SetDirty(island);
        }
    }
}

