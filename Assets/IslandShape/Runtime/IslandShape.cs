using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEngine.Splines;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Splines;
#endif

namespace Islands.EditorTools
{
    public enum IslandToolMode
    {
        Outline,
        EdgeZones
    }

    public enum IslandEdgeZonePreset
    {
        Neutral,
        Beach,
        Cliff,
        Cove,
        Point
    }

    [System.Serializable]
    public struct IslandEdgeZone
    {
        [SerializeField]
        private string presetLabel;

        [SerializeField]
        [Range(0f, 1f)]
        private float centerNormalized;

        [SerializeField]
        [Range(0.02f, 1f)]
        private float spanNormalized;

        [SerializeField]
        private float silhouetteOffset;

        [SerializeField]
        private float coastBandWidthDelta;

        [SerializeField]
        [Min(0.1f)]
        private float terraceWidthScale;

        [SerializeField]
        [Min(0.1f)]
        private float terraceSoftnessScale;

        public string PresetLabel
        {
            get => string.IsNullOrWhiteSpace(presetLabel) ? IslandEdgeZonePreset.Neutral.ToString() : presetLabel;
            set => presetLabel = value;
        }

        public float CenterNormalized
        {
            get => centerNormalized;
            set => centerNormalized = Mathf.Repeat(value, 1f);
        }

        public float SpanNormalized
        {
            get => spanNormalized;
            set => spanNormalized = Mathf.Clamp(value, 0.02f, 1f);
        }

        public float SilhouetteOffset
        {
            get => silhouetteOffset;
            set => silhouetteOffset = value;
        }

        public float CoastBandWidthDelta
        {
            get => coastBandWidthDelta;
            set => coastBandWidthDelta = value;
        }

        public float TerraceWidthScale
        {
            get => terraceWidthScale;
            set => terraceWidthScale = Mathf.Max(0.1f, value);
        }

        public float TerraceSoftnessScale
        {
            get => terraceSoftnessScale;
            set => terraceSoftnessScale = Mathf.Max(0.1f, value);
        }

        public void Validate()
        {
            CenterNormalized = centerNormalized;
            SpanNormalized = spanNormalized;
            TerraceWidthScale = terraceWidthScale;
            TerraceSoftnessScale = terraceSoftnessScale;
            PresetLabel = presetLabel;
        }
    }

    public static class IslandEdgeZonePresets
    {
        public static IslandEdgeZone CreateDefault()
        {
            var zone = CreateNeutralZone();
            zone.CenterNormalized = 0f;
            zone.SpanNormalized = 0.18f;
            return zone;
        }

        public static void ApplyPreset(ref IslandEdgeZone zone, IslandEdgeZonePreset preset)
        {
            if (zone.Equals(default(IslandEdgeZone)))
            {
                zone = CreateNeutralZone();
            }

            switch (preset)
            {
                case IslandEdgeZonePreset.Beach:
                    zone.PresetLabel = IslandEdgeZonePreset.Beach.ToString();
                    zone.SilhouetteOffset = 0.04f;
                    zone.CoastBandWidthDelta = 0.24f;
                    zone.TerraceWidthScale = 0.85f;
                    zone.TerraceSoftnessScale = 1.2f;
                    break;
                case IslandEdgeZonePreset.Cliff:
                    zone.PresetLabel = IslandEdgeZonePreset.Cliff.ToString();
                    zone.SilhouetteOffset = 0f;
                    zone.CoastBandWidthDelta = -0.14f;
                    zone.TerraceWidthScale = 1.2f;
                    zone.TerraceSoftnessScale = 0.8f;
                    break;
                case IslandEdgeZonePreset.Cove:
                    zone.PresetLabel = IslandEdgeZonePreset.Cove.ToString();
                    zone.SilhouetteOffset = -0.18f;
                    zone.CoastBandWidthDelta = 0.1f;
                    zone.TerraceWidthScale = 0.95f;
                    zone.TerraceSoftnessScale = 1.1f;
                    break;
                case IslandEdgeZonePreset.Point:
                    zone.PresetLabel = IslandEdgeZonePreset.Point.ToString();
                    zone.SilhouetteOffset = 0.18f;
                    zone.CoastBandWidthDelta = -0.06f;
                    zone.TerraceWidthScale = 1.12f;
                    zone.TerraceSoftnessScale = 0.9f;
                    break;
                default:
                    zone.PresetLabel = IslandEdgeZonePreset.Neutral.ToString();
                    zone.SilhouetteOffset = 0f;
                    zone.CoastBandWidthDelta = 0f;
                    zone.TerraceWidthScale = 1f;
                    zone.TerraceSoftnessScale = 1f;
                    break;
            }

            zone.Validate();
        }

        private static IslandEdgeZone CreateNeutralZone()
        {
            var zone = new IslandEdgeZone
            {
                PresetLabel = IslandEdgeZonePreset.Neutral.ToString(),
                CenterNormalized = 0f,
                SpanNormalized = 0.18f,
                SilhouetteOffset = 0f,
                CoastBandWidthDelta = 0f,
                TerraceWidthScale = 1f,
                TerraceSoftnessScale = 1f
            };
            zone.Validate();
            return zone;
        }
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(SplineContainer))]
    public sealed class IslandShape : MonoBehaviour
    {
        private static readonly HashSet<IslandShape> EditorInstances = new HashSet<IslandShape>();

        [SerializeField]
        [Min(0.1f)]
        private float depth = 2f;

        [SerializeField]
        [Min(1)]
        private int terraceCount = 3;

        [SerializeField]
        [FormerlySerializedAs("terraceInset")]
        [Min(0.01f)]
        private float terraceStepWidth = 0.35f;

        [SerializeField]
        [Range(-1f, 1f)]
        private float terraceWidthBias;

        [SerializeField]
        [Range(-1f, 1f)]
        private float terraceDepthBias;

        [SerializeField]
        [Range(0f, 1f)]
        private float terraceSoftness = 0.35f;

        [SerializeField]
        [FormerlySerializedAs("sampleSpacing")]
        [Min(0.05f)]
        private float spacing = 0.5f;

        [SerializeField]
        [Min(0.05f)]
        private float coastBandWidth = 0.45f;

        [SerializeField]
        private bool generateCollider = true;

        [SerializeField]
        [Min(0.001f)]
        private float minimumArea = 0.25f;

        [SerializeField]
        [Min(0.0001f)]
        private float duplicatePointTolerance = 0.0001f;

        [SerializeField]
        private List<IslandEdgeZone> edgeZones = new List<IslandEdgeZone>();

        [SerializeField]
        private IslandWater linkedWater;

        [SerializeField]
        [HideInInspector]
        private bool isDrawing;

        [SerializeField]
        [HideInInspector]
        private IslandToolMode toolMode;

        [SerializeField]
        [HideInInspector]
        private SplineContainer splineContainer;

        [SerializeField]
        [HideInInspector]
        private MeshFilter meshFilter;

        [SerializeField]
        [HideInInspector]
        private MeshRenderer meshRenderer;

        [SerializeField]
        [HideInInspector]
        private MeshCollider meshCollider;

        [System.NonSerialized]
        private Mesh generatedMesh;

        [System.NonSerialized]
        private string lastValidationMessage;

        [System.NonSerialized]
        private int selectedKnotIndex = -1;

        [System.NonSerialized]
        private int selectedEdgeZoneIndex = -1;

        public float Depth => depth;

        public int TerraceCount => terraceCount;

        public float TerraceStepWidth => terraceStepWidth;

        public float TerraceWidthBias => terraceWidthBias;

        public float TerraceDepthBias => terraceDepthBias;

        public float TerraceSoftness => terraceSoftness;

        public float Spacing => spacing;

        public float CoastBandWidth => coastBandWidth;

        public bool GenerateCollider => generateCollider;

        public float MinimumArea => minimumArea;

        public float DuplicatePointTolerance => duplicatePointTolerance;

        public IReadOnlyList<IslandEdgeZone> EdgeZones => edgeZones;

        public IslandWater LinkedWater => linkedWater;

        public bool IsDrawing => isDrawing;

        public IslandToolMode ToolMode
        {
            get => toolMode;
            set => toolMode = value;
        }

        public int SelectedKnotIndex
        {
            get => selectedKnotIndex;
            set => selectedKnotIndex = value;
        }

        public int SelectedEdgeZoneIndex
        {
            get => selectedEdgeZoneIndex;
            set => selectedEdgeZoneIndex = value;
        }

        public string LastValidationMessage => lastValidationMessage;

        public SplineContainer SplineContainer => splineContainer;

        public Spline Spline => splineContainer != null ? splineContainer.Spline : null;

        public bool HasClosedSpline => Spline != null && Spline.Closed && Spline.Count >= 3;

        public IslandMeshBuildSettings BuildSettings =>
            new IslandMeshBuildSettings(
                depth,
                terraceCount,
                terraceStepWidth,
                terraceWidthBias,
                terraceDepthBias,
                terraceSoftness,
                spacing,
                coastBandWidth,
                edgeZones,
                minimumArea,
                duplicatePointTolerance);

        private void Reset()
        {
            EnsureSetup();
            EnsureSplineExists();
            RebuildImmediate();
        }

        private void OnEnable()
        {
            EnsureSetup();
            EnsureSplineExists();
            EnsureGeneratedMesh();
#if UNITY_EDITOR
            RegisterEditorInstance(this);
#endif
            RebuildImmediate();
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            UnregisterEditorInstance(this);
#endif
        }

        private void OnDestroy()
        {
            if (generatedMesh == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(generatedMesh);
            }
            else
            {
                DestroyImmediate(generatedMesh);
            }
        }

        private void OnValidate()
        {
            depth = Mathf.Max(0.1f, depth);
            terraceCount = Mathf.Clamp(terraceCount, 1, 8);
            terraceStepWidth = Mathf.Max(0.01f, terraceStepWidth);
            terraceWidthBias = Mathf.Clamp(terraceWidthBias, -1f, 1f);
            terraceDepthBias = Mathf.Clamp(terraceDepthBias, -1f, 1f);
            terraceSoftness = Mathf.Clamp01(terraceSoftness);
            spacing = Mathf.Max(0.05f, spacing);
            coastBandWidth = Mathf.Max(0.05f, coastBandWidth);
            minimumArea = Mathf.Max(0.001f, minimumArea);
            duplicatePointTolerance = Mathf.Max(0.0001f, duplicatePointTolerance);
            ValidateEdgeZones();

            EnsureSetup();
            EnsureSplineExists();
            ForcePlanarSpline();
            RebuildImmediate();
        }

        public void EnsureSetup()
        {
            splineContainer = splineContainer != null ? splineContainer : GetComponent<SplineContainer>();
            meshFilter = meshFilter != null ? meshFilter : GetComponent<MeshFilter>();
            meshRenderer = meshRenderer != null ? meshRenderer : GetComponent<MeshRenderer>();
            meshCollider = meshCollider != null ? meshCollider : GetComponent<MeshCollider>();
        }

        public void EnsureSplineExists()
        {
            if (splineContainer == null)
            {
                return;
            }

            if (splineContainer.Spline == null)
            {
                splineContainer.Splines = new[] { new Spline() };
            }
        }

        public void BeginDrawing()
        {
            EnsureSetup();
            EnsureSplineExists();
            isDrawing = true;
            toolMode = IslandToolMode.Outline;

            if (Spline != null)
            {
                Spline.Closed = false;
            }

            lastValidationMessage = string.Empty;
            selectedKnotIndex = Mathf.Clamp(selectedKnotIndex, -1, (Spline?.Count ?? 0) - 1);
            selectedEdgeZoneIndex = Mathf.Clamp(selectedEdgeZoneIndex, -1, edgeZones.Count - 1);
            RebuildImmediate();
        }

        public bool CloseLoop()
        {
            if (Spline == null || Spline.Count < 3)
            {
                return false;
            }

            Spline.Closed = true;
            isDrawing = false;
            toolMode = IslandToolMode.Outline;
            RebuildImmediate();
            return true;
        }

        public void CancelDrawing()
        {
            if (Spline == null)
            {
                isDrawing = false;
                return;
            }

            if (!Spline.Closed)
            {
                Spline.Clear();
            }

            isDrawing = false;
            toolMode = IslandToolMode.Outline;
            selectedKnotIndex = -1;
            selectedEdgeZoneIndex = -1;
            lastValidationMessage = string.Empty;
            RebuildImmediate();
        }

        public void AddKnot(Vector3 localPosition)
        {
            EnsureSplineExists();
            localPosition.y = 0f;

            var knot = new BezierKnot(ToFloat3(localPosition));
            Spline.Add(knot, TangentMode.AutoSmooth);
            selectedKnotIndex = Spline.Count - 1;
            RebuildImmediate();
        }

        public void DeleteKnot(int knotIndex)
        {
            if (Spline == null || knotIndex < 0 || knotIndex >= Spline.Count)
            {
                return;
            }

            Spline.RemoveAt(knotIndex);
            if (Spline.Count < 3)
            {
                Spline.Closed = false;
            }

            selectedKnotIndex = Mathf.Clamp(selectedKnotIndex, -1, Spline.Count - 1);
            RebuildImmediate();
        }

        public void DeleteLastKnot()
        {
            if (Spline == null || Spline.Count == 0)
            {
                return;
            }

            DeleteKnot(Spline.Count - 1);
        }

        public void SetKnotPosition(int knotIndex, Vector3 localPosition)
        {
            if (Spline == null || knotIndex < 0 || knotIndex >= Spline.Count)
            {
                return;
            }

            localPosition.y = 0f;
            var knot = Spline[knotIndex];
            knot.Position = ToFloat3(localPosition);
            ClampPlanar(ref knot);
            Spline[knotIndex] = knot;
            selectedKnotIndex = knotIndex;
            RebuildImmediate();
        }

        public void AddEdgeZone(float centerNormalized = 0f)
        {
            edgeZones ??= new List<IslandEdgeZone>();
            var zone = IslandEdgeZonePresets.CreateDefault();
            zone.CenterNormalized = centerNormalized;
            edgeZones.Add(zone);
            selectedEdgeZoneIndex = edgeZones.Count - 1;
            toolMode = IslandToolMode.EdgeZones;
            RebuildImmediate();
        }

        public void RemoveEdgeZone(int edgeZoneIndex)
        {
            if (edgeZones == null || edgeZoneIndex < 0 || edgeZoneIndex >= edgeZones.Count)
            {
                return;
            }

            edgeZones.RemoveAt(edgeZoneIndex);
            selectedEdgeZoneIndex = Mathf.Clamp(selectedEdgeZoneIndex, -1, edgeZones.Count - 1);
            RebuildImmediate();
        }

        public void MoveEdgeZone(int fromIndex, int toIndex)
        {
            if (edgeZones == null ||
                fromIndex < 0 || fromIndex >= edgeZones.Count ||
                toIndex < 0 || toIndex >= edgeZones.Count ||
                fromIndex == toIndex)
            {
                return;
            }

            var zone = edgeZones[fromIndex];
            edgeZones.RemoveAt(fromIndex);
            edgeZones.Insert(toIndex, zone);
            selectedEdgeZoneIndex = toIndex;
            RebuildImmediate();
        }

        public IslandEdgeZone GetEdgeZone(int edgeZoneIndex)
        {
            if (edgeZones == null || edgeZoneIndex < 0 || edgeZoneIndex >= edgeZones.Count)
            {
                return IslandEdgeZonePresets.CreateDefault();
            }

            return edgeZones[edgeZoneIndex];
        }

        public void SetEdgeZone(int edgeZoneIndex, IslandEdgeZone edgeZone)
        {
            if (edgeZones == null || edgeZoneIndex < 0 || edgeZoneIndex >= edgeZones.Count)
            {
                return;
            }

            edgeZone.Validate();
            edgeZones[edgeZoneIndex] = edgeZone;
            selectedEdgeZoneIndex = edgeZoneIndex;
            RebuildImmediate();
        }

        public void ApplyEdgeZonePreset(int edgeZoneIndex, IslandEdgeZonePreset preset)
        {
            if (edgeZones == null || edgeZoneIndex < 0 || edgeZoneIndex >= edgeZones.Count)
            {
                return;
            }

            var edgeZone = edgeZones[edgeZoneIndex];
            IslandEdgeZonePresets.ApplyPreset(ref edgeZone, preset);
            edgeZones[edgeZoneIndex] = edgeZone;
            selectedEdgeZoneIndex = edgeZoneIndex;
            RebuildImmediate();
        }

        public void SetKnotTangent(int knotIndex, BezierTangent tangent, Vector3 localOffset)
        {
            if (Spline == null || knotIndex < 0 || knotIndex >= Spline.Count)
            {
                return;
            }

            localOffset.y = 0f;

            var knot = Spline[knotIndex];
            var tangentValue = ToFloat3(localOffset);
            var tangentMode = Spline.GetTangentMode(knotIndex);

            if (tangent == BezierTangent.In)
            {
                knot.TangentIn = tangentValue;
                ApplyOppositeTangent(ref knot, tangentValue, true, tangentMode);
            }
            else
            {
                knot.TangentOut = tangentValue;
                ApplyOppositeTangent(ref knot, tangentValue, false, tangentMode);
            }

            ClampPlanar(ref knot);
            Spline[knotIndex] = knot;

            if (tangentMode != TangentMode.Broken)
            {
                Spline.SetTangentMode(knotIndex, tangentMode);
            }

            selectedKnotIndex = knotIndex;
            RebuildImmediate();
        }

        public void SetKnotTangentMode(int knotIndex, TangentMode tangentMode)
        {
            if (Spline == null || knotIndex < 0 || knotIndex >= Spline.Count)
            {
                return;
            }

            Spline.SetTangentMode(knotIndex, tangentMode);
            selectedKnotIndex = knotIndex;
            RebuildImmediate();
        }

        public bool TryGetResolvedTopPolygon(out List<Vector2> polygon)
        {
            return IslandMeshBuilder.TryBuildTopPolygon(Spline, BuildSettings, out polygon, out _);
        }

        public void RebuildImmediate()
        {
            EnsureSetup();
            EnsureSplineExists();
            EnsureGeneratedMesh();
            ForcePlanarSpline();

            if (Spline == null || !Spline.Closed || Spline.Count < 3)
            {
                lastValidationMessage = string.Empty;
                ClearMesh();
                NotifyWater();
                return;
            }

            var buildResult = IslandMeshBuilder.Build(Spline, BuildSettings);
            lastValidationMessage = buildResult.ValidationMessage;

            if (!buildResult.Succeeded)
            {
                ClearMesh();
                NotifyWater();
                return;
            }

            ApplyMesh(buildResult.MeshData);
            UpdateColliderMesh();
            NotifyWater();
        }

        public bool OwnsSpline(Spline spline)
        {
            return spline != null && Spline == spline;
        }

        private void EnsureGeneratedMesh()
        {
            if (generatedMesh == null)
            {
                generatedMesh = new Mesh
                {
                    name = $"Island Shape Mesh ({GetInstanceID()})",
                    hideFlags = HideFlags.HideAndDontSave
                };
                generatedMesh.indexFormat = IndexFormat.UInt32;
            }

            if (meshFilter != null && meshFilter.sharedMesh != generatedMesh)
            {
                meshFilter.sharedMesh = generatedMesh;
            }
        }

        private void ApplyMesh(IslandMeshData meshData)
        {
            generatedMesh.Clear();
            generatedMesh.vertices = meshData.Vertices;
            generatedMesh.normals = meshData.Normals;
            generatedMesh.uv = meshData.UV;
            generatedMesh.triangles = meshData.Triangles;
            generatedMesh.RecalculateBounds();

            if (meshFilter != null)
            {
                meshFilter.sharedMesh = generatedMesh;
            }
        }

        private void ClearMesh()
        {
            if (generatedMesh != null)
            {
                generatedMesh.Clear();
            }

            UpdateColliderMesh();
        }

        private void UpdateColliderMesh()
        {
            EnsureSetup();

            if (!generateCollider)
            {
                if (meshCollider != null)
                {
                    meshCollider.enabled = false;
                    meshCollider.sharedMesh = null;
                }

                return;
            }

            if (meshCollider == null)
            {
                meshCollider = gameObject.GetComponent<MeshCollider>();
                if (meshCollider == null)
                {
                    meshCollider = gameObject.AddComponent<MeshCollider>();
                }
            }

            meshCollider.enabled = true;
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = generatedMesh != null && generatedMesh.vertexCount > 0 ? generatedMesh : null;
        }

        private void NotifyWater()
        {
            if (linkedWater != null)
            {
                linkedWater.QueueRebuildShoreline();
            }
        }

        private void ForcePlanarSpline()
        {
            if (Spline == null)
            {
                return;
            }

            for (var i = 0; i < Spline.Count; i++)
            {
                var knot = Spline[i];
                var clamped = knot;
                ClampPlanar(ref clamped);

                if (!KnotEquals(knot, clamped))
                {
                    Spline.SetKnotNoNotify(i, clamped);
                }
            }
        }

        private void ValidateEdgeZones()
        {
            edgeZones ??= new List<IslandEdgeZone>();
            for (var i = 0; i < edgeZones.Count; i++)
            {
                var edgeZone = edgeZones[i];
                edgeZone.Validate();
                edgeZones[i] = edgeZone;
            }

            selectedEdgeZoneIndex = Mathf.Clamp(selectedEdgeZoneIndex, -1, edgeZones.Count - 1);
        }

        private static void ClampPlanar(ref BezierKnot knot)
        {
            knot.Position = new float3(knot.Position.x, 0f, knot.Position.z);
            knot.TangentIn = new float3(knot.TangentIn.x, 0f, knot.TangentIn.z);
            knot.TangentOut = new float3(knot.TangentOut.x, 0f, knot.TangentOut.z);
        }

        private static bool KnotEquals(BezierKnot left, BezierKnot right)
        {
            return left.Position.Equals(right.Position) &&
                   left.TangentIn.Equals(right.TangentIn) &&
                   left.TangentOut.Equals(right.TangentOut) &&
                   left.Rotation.Equals(right.Rotation);
        }

        private static void ApplyOppositeTangent(ref BezierKnot knot, float3 tangentValue, bool changedInTangent, TangentMode tangentMode)
        {
            if (tangentMode == TangentMode.Broken || tangentMode == TangentMode.AutoSmooth || tangentMode == TangentMode.Linear)
            {
                return;
            }

            if (tangentMode == TangentMode.Mirrored)
            {
                if (changedInTangent)
                {
                    knot.TangentOut = -tangentValue;
                }
                else
                {
                    knot.TangentIn = -tangentValue;
                }

                return;
            }

            if (tangentMode != TangentMode.Continuous)
            {
                return;
            }

            var opposite = changedInTangent ? knot.TangentOut : knot.TangentIn;
            var magnitude = math.length(opposite);
            var direction = math.normalizesafe(-tangentValue, default);
            var adjusted = direction * magnitude;

            if (changedInTangent)
            {
                knot.TangentOut = adjusted;
            }
            else
            {
                knot.TangentIn = adjusted;
            }
        }

        private static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void RegisterEditorCallbacks()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;

            EditorSplineUtility.AfterSplineWasModified -= OnSplineModified;
            EditorSplineUtility.AfterSplineWasModified += OnSplineModified;
        }

        private static void RegisterEditorInstance(IslandShape islandShape)
        {
            RegisterEditorCallbacks();
            EditorInstances.Add(islandShape);
        }

        private static void UnregisterEditorInstance(IslandShape islandShape)
        {
            EditorInstances.Remove(islandShape);
        }

        private static void OnUndoRedoPerformed()
        {
            RebuildTrackedInstances();
        }

        private static void OnSplineModified(Spline spline)
        {
            foreach (var islandShape in EditorInstances)
            {
                if (islandShape == null || !islandShape.OwnsSpline(spline))
                {
                    continue;
                }

                islandShape.RebuildImmediate();
            }
        }

        private static void RebuildTrackedInstances()
        {
            var deadInstances = ListPool<IslandShape>.Get();
            foreach (var islandShape in EditorInstances)
            {
                if (islandShape == null)
                {
                    deadInstances.Add(islandShape);
                    continue;
                }

                islandShape.RebuildImmediate();
            }

            for (var i = 0; i < deadInstances.Count; i++)
            {
                EditorInstances.Remove(deadInstances[i]);
            }

            ListPool<IslandShape>.Release(deadInstances);
        }
#endif

        private static class ListPool<T>
        {
            private static readonly Stack<List<T>> Pool = new Stack<List<T>>();

            public static List<T> Get()
            {
                return Pool.Count > 0 ? Pool.Pop() : new List<T>();
            }

            public static void Release(List<T> list)
            {
                list.Clear();
                Pool.Push(list);
            }
        }
    }
}
