using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.Splines;

namespace Islands.EditorTools
{
    [EditorTool("Island Shape Tool", typeof(IslandShape))]
    public sealed class IslandShapeTool : EditorTool
    {
        private const float KnotHandleScale = 0.08f;
        private const float TangentHandleScale = 0.06f;
        private const float CloseLoopPixelThreshold = 14f;

        private static readonly Color OutlineColor = new Color(0.1f, 0.8f, 0.9f, 1f);
        private static readonly Color DrawingColor = new Color(1f, 0.8f, 0.2f, 1f);
        private static readonly Color KnotColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        private static readonly Color SelectedKnotColor = new Color(1f, 0.55f, 0.15f, 1f);
        private static readonly Color TangentColor = new Color(0.45f, 1f, 0.45f, 1f);

        public override void OnToolGUI(EditorWindow window)
        {
            var island = target as IslandShape;
            if (island == null)
            {
                return;
            }

            island.EnsureSetup();
            island.EnsureSplineExists();

            var currentEvent = Event.current;
            HandleKeyboardInput(island, currentEvent);

            if (island.IsDrawing && currentEvent.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            }

            DrawSplinePreview(island);
            DrawKnotHandles(island);
            DrawTangentHandles(island);
            DrawDrawingPreview(island);

            if (island.IsDrawing)
            {
                HandleDrawingInput(island, currentEvent);
            }
        }

        private static void HandleKeyboardInput(IslandShape island, Event currentEvent)
        {
            if (currentEvent.type != EventType.KeyDown)
            {
                return;
            }

            if (currentEvent.keyCode == KeyCode.Space)
            {
                if (island.IsDrawing && island.Spline != null && island.Spline.Count >= 3)
                {
                    RecordIslandChange(island, "Close Island Loop");
                    island.CloseLoop();
                    MarkDirty(island);
                    currentEvent.Use();
                }

                return;
            }

            if (currentEvent.keyCode != KeyCode.Escape || !island.IsDrawing)
            {
                return;
            }

            RecordIslandChange(island, "Cancel Island Drawing");
            island.CancelDrawing();
            MarkDirty(island);
            currentEvent.Use();
        }

        private static void HandleDrawingInput(IslandShape island, Event currentEvent)
        {
            if (currentEvent.type != EventType.MouseDown || currentEvent.alt)
            {
                return;
            }

            if (currentEvent.button == 1)
            {
                if (island.Spline == null || island.Spline.Count == 0)
                {
                    return;
                }

                RecordIslandChange(island, "Delete Island Knot");
                island.DeleteLastKnot();
                MarkDirty(island);
                currentEvent.Use();
                return;
            }

            if (currentEvent.button != 0)
            {
                return;
            }

            if (!TryGetLocalPlanePoint(island, currentEvent.mousePosition, out var localPoint))
            {
                return;
            }

            if (ShouldCloseLoop(island, currentEvent.mousePosition))
            {
                RecordIslandChange(island, "Close Island Loop");
                island.CloseLoop();
            }
            else
            {
                RecordIslandChange(island, "Add Island Knot");
                island.AddKnot(localPoint);
            }

            MarkDirty(island);
            currentEvent.Use();
        }

        private static void DrawSplinePreview(IslandShape island)
        {
            var spline = island.Spline;
            if (spline == null || spline.Count == 0)
            {
                return;
            }

            var previewPoints = SamplePreviewPoints(spline, spline.Closed);
            if (previewPoints.Count < 2)
            {
                return;
            }

            using (new Handles.DrawingScope(island.transform.localToWorldMatrix))
            {
                Handles.color = island.IsDrawing ? DrawingColor : OutlineColor;
                Handles.DrawAAPolyLine(3f, previewPoints.ToArray());
            }
        }

        private static void DrawKnotHandles(IslandShape island)
        {
            if (island.IsDrawing)
            {
                return;
            }

            var spline = island.Spline;
            if (spline == null)
            {
                return;
            }

            using (new Handles.DrawingScope(island.transform.localToWorldMatrix))
            {
                for (var knotIndex = 0; knotIndex < spline.Count; knotIndex++)
                {
                    var knot = spline[knotIndex];
                    var localPosition = ToVector3(knot.Position);
                    var worldPosition = island.transform.TransformPoint(localPosition);
                    var handleSize = HandleUtility.GetHandleSize(worldPosition) * KnotHandleScale;

                    Handles.color = knotIndex == island.SelectedKnotIndex ? SelectedKnotColor : KnotColor;
                    EditorGUI.BeginChangeCheck();
                    var movedPosition = Handles.Slider2D(
                        localPosition,
                        Vector3.up,
                        Vector3.right,
                        Vector3.forward,
                        handleSize,
                        Handles.DotHandleCap,
                        Vector2.zero);

                    if (!EditorGUI.EndChangeCheck())
                    {
                        continue;
                    }

                    RecordIslandChange(island, "Move Island Knot", false);
                    island.SetKnotPosition(knotIndex, movedPosition);
                    island.SelectedKnotIndex = knotIndex;
                    MarkDirty(island);
                }
            }
        }

        private static void DrawTangentHandles(IslandShape island)
        {
            if (island.IsDrawing)
            {
                return;
            }

            var spline = island.Spline;
            var selectedKnotIndex = island.SelectedKnotIndex;
            if (spline == null || selectedKnotIndex < 0 || selectedKnotIndex >= spline.Count)
            {
                return;
            }

            var tangentMode = spline.GetTangentMode(selectedKnotIndex);
            if (tangentMode == TangentMode.AutoSmooth || tangentMode == TangentMode.Linear)
            {
                return;
            }

            var knot = spline[selectedKnotIndex];
            var localPosition = ToVector3(knot.Position);
            var worldPosition = island.transform.TransformPoint(localPosition);
            var handleSize = HandleUtility.GetHandleSize(worldPosition) * TangentHandleScale;

            using (new Handles.DrawingScope(island.transform.localToWorldMatrix))
            {
                DrawTangentHandle(island, selectedKnotIndex, localPosition, ToVector3(knot.TangentIn), BezierTangent.In, handleSize);
                DrawTangentHandle(island, selectedKnotIndex, localPosition, ToVector3(knot.TangentOut), BezierTangent.Out, handleSize);
            }
        }

        private static void DrawTangentHandle(
            IslandShape island,
            int knotIndex,
            Vector3 knotLocalPosition,
            Vector3 tangentLocalOffset,
            BezierTangent tangent,
            float handleSize)
        {
            var tangentPosition = knotLocalPosition + tangentLocalOffset;

            Handles.color = TangentColor;
            Handles.DrawLine(knotLocalPosition, tangentPosition);

            EditorGUI.BeginChangeCheck();
            var movedTangentPosition = Handles.Slider2D(
                tangentPosition,
                Vector3.up,
                Vector3.right,
                Vector3.forward,
                handleSize,
                Handles.CircleHandleCap,
                Vector2.zero);

            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            RecordIslandChange(island, "Move Island Tangent", false);
            island.SetKnotTangent(knotIndex, tangent, movedTangentPosition - knotLocalPosition);
            MarkDirty(island);
        }

        private static void DrawDrawingPreview(IslandShape island)
        {
            if (!island.IsDrawing || island.Spline == null)
            {
                return;
            }

            if (!TryGetLocalPlanePoint(island, Event.current.mousePosition, out var localPoint))
            {
                return;
            }

            using (new Handles.DrawingScope(island.transform.localToWorldMatrix))
            {
                Handles.color = DrawingColor;

                if (island.Spline.Count > 0)
                {
                    var lastKnot = ToVector3(island.Spline[island.Spline.Count - 1].Position);
                    Handles.DrawDottedLine(lastKnot, localPoint, 4f);
                }

                if (island.Spline.Count >= 3)
                {
                    var firstKnot = ToVector3(island.Spline[0].Position);
                    var worldFirst = island.transform.TransformPoint(firstKnot);
                    var closeHandleSize = HandleUtility.GetHandleSize(worldFirst) * KnotHandleScale * 1.25f;

                    Handles.color = ShouldCloseLoop(island, Event.current.mousePosition) ? SelectedKnotColor : DrawingColor;
                    Handles.DrawWireDisc(firstKnot, Vector3.up, closeHandleSize);
                }
            }
        }

        private static bool TryGetLocalPlanePoint(IslandShape island, Vector2 guiPosition, out Vector3 localPoint)
        {
            var worldRay = HandleUtility.GUIPointToWorldRay(guiPosition);
            var plane = new Plane(island.transform.up, island.transform.position);

            if (plane.Raycast(worldRay, out var distance))
            {
                var worldPoint = worldRay.GetPoint(distance);
                localPoint = island.transform.InverseTransformPoint(worldPoint);
                localPoint.y = 0f;
                return true;
            }

            localPoint = default;
            return false;
        }

        private static bool ShouldCloseLoop(IslandShape island, Vector2 guiPosition)
        {
            if (island.Spline == null || island.Spline.Count < 3)
            {
                return false;
            }

            var firstKnot = ToVector3(island.Spline[0].Position);
            var screenPosition = HandleUtility.WorldToGUIPoint(island.transform.TransformPoint(firstKnot));
            return Vector2.Distance(screenPosition, guiPosition) <= CloseLoopPixelThreshold;
        }

        private static List<Vector3> SamplePreviewPoints(Spline spline, bool includeClosure)
        {
            var previewLoop = SamplePreviewLoop(spline);
            if (!includeClosure || previewLoop.Count == 0)
            {
                return previewLoop;
            }

            var closedPolyline = new List<Vector3>(previewLoop.Count + 1);
            closedPolyline.AddRange(previewLoop);
            closedPolyline.Add(previewLoop[0]);
            return closedPolyline;
        }

        private static List<Vector3> SamplePreviewLoop(Spline spline)
        {
            var previewPoints = new List<Vector3>();
            var curveCount = SplineUtility.GetCurveCount(spline);

            if (curveCount <= 0)
            {
                if (spline.Count == 1)
                {
                    previewPoints.Add(ToVector3(spline[0].Position));
                }

                return previewPoints;
            }

            const int stepsPerCurve = 12;
            for (var curveIndex = 0; curveIndex < curveCount; curveIndex++)
            {
                var startT = curveIndex == 0 ? 0f : SplineUtility.CurveToSplineT(spline, curveIndex);
                var endT = curveIndex == curveCount - 1
                    ? 1f
                    : SplineUtility.CurveToSplineT(spline, curveIndex + 1f);

                for (var step = 0; step <= stepsPerCurve; step++)
                {
                    if (curveIndex > 0 && step == 0)
                    {
                        continue;
                    }

                    var normalizedStep = step / (float)stepsPerCurve;
                    var t = Mathf.Lerp(startT, endT, normalizedStep);
                    previewPoints.Add(ToVector3(SplineUtility.EvaluatePosition(spline, t)));
                }
            }

            return previewPoints;
        }

        private static void RecordIslandChange(IslandShape island, string actionName, bool includeShape = true)
        {
            if (includeShape)
            {
                Undo.RecordObject(island, actionName);
            }

            if (island.SplineContainer != null)
            {
                Undo.RecordObject(island.SplineContainer, actionName);
            }
        }

        private static void MarkDirty(IslandShape island)
        {
            EditorUtility.SetDirty(island);
            if (island.SplineContainer != null)
            {
                EditorUtility.SetDirty(island.SplineContainer);
            }
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }
    }
}
