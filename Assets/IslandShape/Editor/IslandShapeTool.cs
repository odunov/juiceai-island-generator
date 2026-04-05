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
        private const float EdgeZoneHandleScale = 0.09f;
        private const float EdgeZoneSpanHandleScale = 0.07f;
        private const float CloseLoopPixelThreshold = 14f;
        private static readonly Color OutlineColor = new Color(0.1f, 0.8f, 0.9f, 1f);
        private static readonly Color BaseOutlineColor = new Color(0.1f, 0.55f, 0.7f, 0.45f);
        private static readonly Color DrawingColor = new Color(1f, 0.8f, 0.2f, 1f);
        private static readonly Color KnotColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        private static readonly Color SelectedKnotColor = new Color(1f, 0.55f, 0.15f, 1f);
        private static readonly Color TangentColor = new Color(0.45f, 1f, 0.45f, 1f);
        private static readonly Color EdgeZoneColor = new Color(1f, 0.76f, 0.26f, 0.9f);
        private static readonly Color SelectedEdgeZoneColor = new Color(1f, 0.4f, 0.18f, 1f);
        private static readonly Color EdgeZoneArcColor = new Color(1f, 0.88f, 0.4f, 0.9f);

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

            if ((island.IsDrawing || IsEdgeZoneModeActive(island)) && currentEvent.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            }

            if (IsEdgeZoneModeActive(island))
            {
                DrawEdgeZoneMode(island);
                return;
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

        private static bool IsEdgeZoneModeActive(IslandShape island)
        {
            return !island.IsDrawing && island.HasClosedSpline && island.ToolMode == IslandToolMode.EdgeZones;
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

        private static void DrawEdgeZoneMode(IslandShape island)
        {
            var spline = island.Spline;
            if (spline == null || spline.Count < 3)
            {
                return;
            }

            var baseLoop = SamplePreviewLoop(spline);
            if (baseLoop.Count < 3)
            {
                return;
            }

            var detailLoop = BuildDetailPreviewLoop(baseLoop, island.EdgeZones);
            using (new Handles.DrawingScope(island.transform.localToWorldMatrix))
            {
                Handles.color = BaseOutlineColor;
                DrawLoop(baseLoop, 2f);

                Handles.color = OutlineColor;
                DrawLoop(detailLoop, 3f);

                DrawEdgeZoneSelectionButtons(island, detailLoop);
                DrawSelectedEdgeZoneHandles(island, detailLoop);
            }
        }

        private static void DrawEdgeZoneSelectionButtons(IslandShape island, IReadOnlyList<Vector3> detailLoop)
        {
            for (var zoneIndex = 0; zoneIndex < island.EdgeZones.Count; zoneIndex++)
            {
                var edgeZone = island.GetEdgeZone(zoneIndex);
                var centerPoint = GetPointOnLoop(detailLoop, edgeZone.CenterNormalized);
                var worldPoint = island.transform.TransformPoint(centerPoint);
                var handleSize = HandleUtility.GetHandleSize(worldPoint) * EdgeZoneHandleScale;

                DrawEdgeZoneArc(detailLoop, edgeZone, zoneIndex == island.SelectedEdgeZoneIndex);

                Handles.color = zoneIndex == island.SelectedEdgeZoneIndex ? SelectedEdgeZoneColor : EdgeZoneColor;
                if (!Handles.Button(centerPoint, Quaternion.identity, handleSize, handleSize * 1.15f, Handles.DotHandleCap))
                {
                    continue;
                }

                island.SelectedEdgeZoneIndex = zoneIndex;
                island.ToolMode = IslandToolMode.EdgeZones;
                SceneView.RepaintAll();
            }
        }

        private static void DrawSelectedEdgeZoneHandles(IslandShape island, IReadOnlyList<Vector3> detailLoop)
        {
            if (island.EdgeZones.Count == 0)
            {
                return;
            }

            var selectedZoneIndex = Mathf.Clamp(island.SelectedEdgeZoneIndex, 0, island.EdgeZones.Count - 1);
            island.SelectedEdgeZoneIndex = selectedZoneIndex;

            var edgeZone = island.GetEdgeZone(selectedZoneIndex);
            var centerPoint = GetPointOnLoop(detailLoop, edgeZone.CenterNormalized);
            var startNormalized = Mathf.Repeat(edgeZone.CenterNormalized - (edgeZone.SpanNormalized * 0.5f), 1f);
            var endNormalized = Mathf.Repeat(edgeZone.CenterNormalized + (edgeZone.SpanNormalized * 0.5f), 1f);
            var startPoint = GetPointOnLoop(detailLoop, startNormalized);
            var endPoint = GetPointOnLoop(detailLoop, endNormalized);

            var centerSize = HandleUtility.GetHandleSize(island.transform.TransformPoint(centerPoint)) * EdgeZoneHandleScale;
            var spanHandleSize = centerSize * (EdgeZoneSpanHandleScale / EdgeZoneHandleScale);

            Handles.color = SelectedEdgeZoneColor;
            EditorGUI.BeginChangeCheck();
            var movedCenter = Handles.Slider2D(
                centerPoint,
                Vector3.up,
                Vector3.right,
                Vector3.forward,
                centerSize,
                Handles.CircleHandleCap,
                Vector2.zero);

            if (EditorGUI.EndChangeCheck())
            {
                edgeZone.CenterNormalized = GetClosestNormalizedPosition(detailLoop, movedCenter);
                ApplyEdgeZoneChange(island, selectedZoneIndex, edgeZone, "Move Edge Zone Center");
                return;
            }

            Handles.color = EdgeZoneArcColor;
            Handles.DrawDottedLine(centerPoint, startPoint, 3f);
            Handles.DrawDottedLine(centerPoint, endPoint, 3f);

            EditorGUI.BeginChangeCheck();
            var movedStart = Handles.Slider2D(
                startPoint,
                Vector3.up,
                Vector3.right,
                Vector3.forward,
                spanHandleSize,
                Handles.DotHandleCap,
                Vector2.zero);

            if (EditorGUI.EndChangeCheck())
            {
                var handleNormalized = GetClosestNormalizedPosition(detailLoop, movedStart);
                edgeZone.SpanNormalized = GetSpanFromHandle(edgeZone.CenterNormalized, handleNormalized);
                ApplyEdgeZoneChange(island, selectedZoneIndex, edgeZone, "Resize Edge Zone");
                return;
            }

            EditorGUI.BeginChangeCheck();
            var movedEnd = Handles.Slider2D(
                endPoint,
                Vector3.up,
                Vector3.right,
                Vector3.forward,
                spanHandleSize,
                Handles.DotHandleCap,
                Vector2.zero);

            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            var endHandleNormalized = GetClosestNormalizedPosition(detailLoop, movedEnd);
            edgeZone.SpanNormalized = GetSpanFromHandle(edgeZone.CenterNormalized, endHandleNormalized);
            ApplyEdgeZoneChange(island, selectedZoneIndex, edgeZone, "Resize Edge Zone");
        }

        private static void ApplyEdgeZoneChange(IslandShape island, int edgeZoneIndex, IslandEdgeZone edgeZone, string actionName)
        {
            Undo.RecordObject(island, actionName);
            island.SetEdgeZone(edgeZoneIndex, edgeZone);
            MarkDirty(island);
            SceneView.RepaintAll();
        }

        private static void DrawEdgeZoneArc(IReadOnlyList<Vector3> detailLoop, IslandEdgeZone edgeZone, bool selected)
        {
            var arcPoints = SampleLoopArc(detailLoop, edgeZone.CenterNormalized, edgeZone.SpanNormalized, selected ? 28 : 18);
            if (arcPoints.Count < 2)
            {
                return;
            }

            Handles.color = selected ? SelectedEdgeZoneColor : EdgeZoneArcColor;
            Handles.DrawAAPolyLine(selected ? 4f : 2f, arcPoints.ToArray());
        }

        private static void DrawKnotHandles(IslandShape island)
        {
            if (island.IsDrawing || island.ToolMode == IslandToolMode.EdgeZones)
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
            if (island.IsDrawing || island.ToolMode == IslandToolMode.EdgeZones)
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

        private static List<Vector3> BuildDetailPreviewLoop(IReadOnlyList<Vector3> baseLoop, IReadOnlyList<IslandEdgeZone> edgeZones)
        {
            var detailLoop = new List<Vector3>(baseLoop.Count);
            if (baseLoop.Count == 0)
            {
                return detailLoop;
            }

            var perimeterPositions = GetNormalizedPerimeterPositions(baseLoop);
            for (var pointIndex = 0; pointIndex < baseLoop.Count; pointIndex++)
            {
                var outwardNormal = GetVertexOutwardNormal(baseLoop, pointIndex);
                var silhouetteOffset = EvaluateZoneSample(edgeZones, perimeterPositions[pointIndex]);
                detailLoop.Add(baseLoop[pointIndex] + (outwardNormal * silhouetteOffset));
            }

            return detailLoop;
        }

        private static float EvaluateZoneSample(IReadOnlyList<IslandEdgeZone> edgeZones, float normalizedT)
        {
            var bestInfluence = 0f;
            var silhouetteOffset = 0f;
            if (edgeZones != null)
            {
                for (var zoneIndex = 0; zoneIndex < edgeZones.Count; zoneIndex++)
                {
                    var influence = EvaluateZoneInfluence(edgeZones[zoneIndex], normalizedT);
                    if (influence <= bestInfluence)
                    {
                        continue;
                    }

                    bestInfluence = influence;
                    silhouetteOffset = edgeZones[zoneIndex].SilhouetteOffset * influence;
                }
            }

            return silhouetteOffset;
        }

        private static float EvaluateZoneInfluence(IslandEdgeZone edgeZone, float normalizedT)
        {
            var halfSpan = Mathf.Max(edgeZone.SpanNormalized * 0.5f, 0.01f);
            var wrappedDistance = Mathf.Abs(Mathf.DeltaAngle(normalizedT * 360f, edgeZone.CenterNormalized * 360f)) / 360f;
            if (wrappedDistance >= halfSpan)
            {
                return 0f;
            }

            var falloff = 1f - (wrappedDistance / halfSpan);
            return falloff * falloff * (3f - (2f * falloff));
        }

        private static float[] GetNormalizedPerimeterPositions(IReadOnlyList<Vector3> loop)
        {
            var normalizedPositions = new float[loop.Count];
            var perimeter = GetLoopPerimeter(loop);
            if (perimeter <= Mathf.Epsilon)
            {
                return normalizedPositions;
            }

            var travel = 0f;
            for (var pointIndex = 0; pointIndex < loop.Count; pointIndex++)
            {
                normalizedPositions[pointIndex] = travel / perimeter;
                var nextIndex = (pointIndex + 1) % loop.Count;
                travel += Vector3.Distance(loop[pointIndex], loop[nextIndex]);
            }

            return normalizedPositions;
        }

        private static float GetLoopPerimeter(IReadOnlyList<Vector3> loop)
        {
            var perimeter = 0f;
            for (var pointIndex = 0; pointIndex < loop.Count; pointIndex++)
            {
                perimeter += Vector3.Distance(loop[pointIndex], loop[(pointIndex + 1) % loop.Count]);
            }

            return perimeter;
        }

        private static Vector3 GetVertexOutwardNormal(IReadOnlyList<Vector3> loop, int pointIndex)
        {
            var previous = loop[(pointIndex - 1 + loop.Count) % loop.Count];
            var current = loop[pointIndex];
            var next = loop[(pointIndex + 1) % loop.Count];

            var previousDirection = (current - previous).normalized;
            var nextDirection = (next - current).normalized;
            var previousNormal = new Vector3(previousDirection.z, 0f, -previousDirection.x);
            var nextNormal = new Vector3(nextDirection.z, 0f, -nextDirection.x);
            var outwardNormal = (previousNormal + nextNormal).normalized;

            if (outwardNormal.sqrMagnitude <= Mathf.Epsilon)
            {
                outwardNormal = previousNormal.sqrMagnitude > Mathf.Epsilon ? previousNormal : nextNormal;
            }

            return outwardNormal.normalized;
        }

        private static void DrawLoop(IReadOnlyList<Vector3> loop, float thickness)
        {
            if (loop.Count < 2)
            {
                return;
            }

            var closedPolyline = new Vector3[loop.Count + 1];
            for (var pointIndex = 0; pointIndex < loop.Count; pointIndex++)
            {
                closedPolyline[pointIndex] = loop[pointIndex];
            }

            closedPolyline[loop.Count] = loop[0];
            Handles.DrawAAPolyLine(thickness, closedPolyline);
        }

        private static List<Vector3> SampleLoopArc(IReadOnlyList<Vector3> loop, float centerNormalized, float spanNormalized, int sampleCount)
        {
            var arcPoints = new List<Vector3>(sampleCount + 1);
            var startNormalized = Mathf.Repeat(centerNormalized - (spanNormalized * 0.5f), 1f);
            for (var sampleIndex = 0; sampleIndex <= sampleCount; sampleIndex++)
            {
                var t = startNormalized + (spanNormalized * (sampleIndex / (float)sampleCount));
                arcPoints.Add(GetPointOnLoop(loop, t));
            }

            return arcPoints;
        }

        private static Vector3 GetPointOnLoop(IReadOnlyList<Vector3> loop, float normalizedPosition)
        {
            if (loop.Count == 0)
            {
                return default;
            }

            if (loop.Count == 1)
            {
                return loop[0];
            }

            var perimeter = GetLoopPerimeter(loop);
            if (perimeter <= Mathf.Epsilon)
            {
                return loop[0];
            }

            var targetDistance = Mathf.Repeat(normalizedPosition, 1f) * perimeter;
            var travel = 0f;
            for (var pointIndex = 0; pointIndex < loop.Count; pointIndex++)
            {
                var nextIndex = (pointIndex + 1) % loop.Count;
                var segmentLength = Vector3.Distance(loop[pointIndex], loop[nextIndex]);
                if (travel + segmentLength >= targetDistance)
                {
                    var lerp = segmentLength <= Mathf.Epsilon ? 0f : (targetDistance - travel) / segmentLength;
                    return Vector3.Lerp(loop[pointIndex], loop[nextIndex], lerp);
                }

                travel += segmentLength;
            }

            return loop[0];
        }

        private static float GetClosestNormalizedPosition(IReadOnlyList<Vector3> loop, Vector3 localPoint)
        {
            if (loop.Count == 0)
            {
                return 0f;
            }

            var perimeter = GetLoopPerimeter(loop);
            if (perimeter <= Mathf.Epsilon)
            {
                return 0f;
            }

            var bestDistance = float.MaxValue;
            var bestNormalized = 0f;
            var travel = 0f;
            for (var pointIndex = 0; pointIndex < loop.Count; pointIndex++)
            {
                var nextIndex = (pointIndex + 1) % loop.Count;
                var start = loop[pointIndex];
                var end = loop[nextIndex];
                var segment = end - start;
                var segmentLength = segment.magnitude;
                var segmentT = 0f;

                if (segmentLength > Mathf.Epsilon)
                {
                    segmentT = Mathf.Clamp01(Vector3.Dot(localPoint - start, segment) / segment.sqrMagnitude);
                }

                var closestPoint = start + (segment * segmentT);
                var distance = Vector3.SqrMagnitude(localPoint - closestPoint);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestNormalized = (travel + (segmentLength * segmentT)) / perimeter;
                }

                travel += segmentLength;
            }

            return Mathf.Repeat(bestNormalized, 1f);
        }

        private static float GetSpanFromHandle(float centerNormalized, float handleNormalized)
        {
            var wrappedDistance = Mathf.Abs(Mathf.DeltaAngle(centerNormalized * 360f, handleNormalized * 360f)) / 360f;
            return Mathf.Clamp(wrappedDistance * 2f, 0.02f, 1f);
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
