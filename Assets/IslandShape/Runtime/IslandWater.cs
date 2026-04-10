using System.Collections.Generic;
using UnityEngine;

namespace Islands.EditorTools
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class IslandWater : MonoBehaviour
    {
        [SerializeField]
        [Min(0f)]
        private float seaLevel;

        [SerializeField]
        private Vector2 size = new Vector2(120f, 120f);

        [SerializeField]
        [Range(8, 160)]
        private int resolution = 72;

        [SerializeField]
        private bool autoCollectIslands = true;

        [SerializeField]
        private List<IslandShape> islands = new List<IslandShape>();

        [Header("Color")]
        [SerializeField]
        private Color shallowColor = new Color(0.31f, 0.77f, 0.88f, 0.92f);

        [SerializeField]
        private Color deepColor = new Color(0.08f, 0.38f, 0.58f, 0.95f);

        [SerializeField]
        private Color foamColor = new Color(0.92f, 0.97f, 1f, 0.95f);

        [SerializeField]
        [Min(0.1f)]
        private float shallowDistance = 9f;

        [SerializeField]
        [Min(0.05f)]
        private float foamWidth = 1.8f;

        [SerializeField]
        [Range(0f, 1f)]
        private float foamStrength = 0.8f;

        [Header("Waves")]
        [SerializeField]
        [Min(0f)]
        private float waveAmplitude = 0.14f;

        [SerializeField]
        [Min(0.05f)]
        private float waveFrequency = 1.7f;

        [SerializeField]
        [Min(0f)]
        private float waveSpeed = 1.1f;

        [SerializeField]
        private Vector2 waveDirection = new Vector2(0.85f, 0.25f);

        [SerializeField]
        [Range(0f, 1f)]
        private float openWaterWaveBlend = 0.35f;

        [SerializeField]
        [HideInInspector]
        private MeshFilter meshFilter;

        [SerializeField]
        [HideInInspector]
        private MeshRenderer meshRenderer;

        [System.NonSerialized]
        private Mesh generatedMesh;

        [System.NonSerialized]
        private Vector3[] baseVertices;

        [System.NonSerialized]
        private Vector3[] animatedVertices;

        [System.NonSerialized]
        private Vector2[] baseUvs;

        [System.NonSerialized]
        private int[] baseTriangles;

        [System.NonSerialized]
        private Color[] vertexColors;

        [System.NonSerialized]
        private float[] shorelineDistances;

        [System.NonSerialized]
        private bool shorelineDirty = true;

        private void Reset()
        {
            EnsureSetup();
            QueueRebuildShoreline();
        }

        private void OnEnable()
        {
            EnsureSetup();
            EnsureGeneratedMesh();
            QueueRebuildShoreline();
        }

        private void OnDisable()
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
            size.x = Mathf.Max(8f, size.x);
            size.y = Mathf.Max(8f, size.y);
            resolution = Mathf.Clamp(resolution, 8, 160);
            shallowDistance = Mathf.Max(0.1f, shallowDistance);
            foamWidth = Mathf.Max(0.05f, foamWidth);
            foamStrength = Mathf.Clamp01(foamStrength);
            waveAmplitude = Mathf.Max(0f, waveAmplitude);
            waveFrequency = Mathf.Max(0.05f, waveFrequency);
            waveSpeed = Mathf.Max(0f, waveSpeed);
            if (waveDirection.sqrMagnitude <= 0.0001f)
            {
                waveDirection = new Vector2(1f, 0f);
            }

            EnsureSetup();
            QueueRebuildShoreline();
        }

        private void Update()
        {
            EnsureSetup();
            EnsureGeneratedMesh();

            if (shorelineDirty || baseVertices == null || shorelineDistances == null || baseVertices.Length == 0)
            {
                RebuildShoreline();
            }

            AnimateWater(Application.isPlaying ? Time.time : Time.realtimeSinceStartup);
        }

        public void QueueRebuildShoreline()
        {
            shorelineDirty = true;
        }

        public void EnsureSetup()
        {
            meshFilter = meshFilter != null ? meshFilter : GetComponent<MeshFilter>();
            meshRenderer = meshRenderer != null ? meshRenderer : GetComponent<MeshRenderer>();
        }

        private void EnsureGeneratedMesh()
        {
            if (generatedMesh == null)
            {
                generatedMesh = new Mesh
                {
                    name = $"Island Water Mesh ({GetInstanceID()})",
                    hideFlags = HideFlags.HideAndDontSave
                };
                generatedMesh.MarkDynamic();
            }

            if (meshFilter != null && meshFilter.sharedMesh != generatedMesh)
            {
                meshFilter.sharedMesh = generatedMesh;
            }
        }

        private void RebuildShoreline()
        {
            shorelineDirty = false;
            if (autoCollectIslands)
            {
                CollectIslands();
            }

            BuildGrid();
            ComputeShorelineDistances();
            ApplyMeshData();
        }

        private void CollectIslands()
        {
            islands.Clear();
            var foundIslands = FindObjectsByType<IslandShape>(FindObjectsSortMode.None);
            for (var i = 0; i < foundIslands.Length; i++)
            {
                if (foundIslands[i] != null)
                {
                    islands.Add(foundIslands[i]);
                }
            }
        }

        private void BuildGrid()
        {
            var vertsPerAxis = resolution + 1;
            var vertexCount = vertsPerAxis * vertsPerAxis;
            baseVertices = new Vector3[vertexCount];
            animatedVertices = new Vector3[vertexCount];
            baseUvs = new Vector2[vertexCount];
            vertexColors = new Color[vertexCount];
            shorelineDistances = new float[vertexCount];

            var minX = -size.x * 0.5f;
            var minZ = -size.y * 0.5f;
            var stepX = size.x / resolution;
            var stepZ = size.y / resolution;

            var vertexIndex = 0;
            for (var z = 0; z < vertsPerAxis; z++)
            {
                for (var x = 0; x < vertsPerAxis; x++)
                {
                    var localX = minX + (x * stepX);
                    var localZ = minZ + (z * stepZ);
                    var position = new Vector3(localX, seaLevel, localZ);
                    baseVertices[vertexIndex] = position;
                    animatedVertices[vertexIndex] = position;
                    baseUvs[vertexIndex] = new Vector2(x / (float)resolution, z / (float)resolution);
                    vertexColors[vertexIndex] = deepColor;
                    shorelineDistances[vertexIndex] = shallowDistance * 2f;
                    vertexIndex++;
                }
            }

            var triangleCount = resolution * resolution * 6;
            baseTriangles = new int[triangleCount];
            var triangleIndex = 0;
            for (var z = 0; z < resolution; z++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var start = (z * vertsPerAxis) + x;
                    baseTriangles[triangleIndex++] = start;
                    baseTriangles[triangleIndex++] = start + vertsPerAxis;
                    baseTriangles[triangleIndex++] = start + 1;

                    baseTriangles[triangleIndex++] = start + 1;
                    baseTriangles[triangleIndex++] = start + vertsPerAxis;
                    baseTriangles[triangleIndex++] = start + vertsPerAxis + 1;
                }
            }
        }

        private void ComputeShorelineDistances()
        {
            var worldPolygons = new List<List<Vector2>>();
            for (var islandIndex = 0; islandIndex < islands.Count; islandIndex++)
            {
                var island = islands[islandIndex];
                if (island == null || !island.TryGetResolvedTopPolygon(out var localPolygon) || localPolygon == null || localPolygon.Count < 3)
                {
                    continue;
                }

                var worldPolygon = new List<Vector2>(localPolygon.Count);
                for (var pointIndex = 0; pointIndex < localPolygon.Count; pointIndex++)
                {
                    var worldPoint = island.transform.TransformPoint(new Vector3(localPolygon[pointIndex].x, 0f, localPolygon[pointIndex].y));
                    worldPolygon.Add(new Vector2(worldPoint.x, worldPoint.z));
                }

                worldPolygons.Add(worldPolygon);
            }

            for (var vertexIndex = 0; vertexIndex < baseVertices.Length; vertexIndex++)
            {
                var worldPoint = transform.TransformPoint(baseVertices[vertexIndex]);
                var waterPoint = new Vector2(worldPoint.x, worldPoint.z);
                shorelineDistances[vertexIndex] = ComputeShoreDistance(waterPoint, worldPolygons);
            }
        }

        private void ApplyMeshData()
        {
            generatedMesh.Clear();
            generatedMesh.vertices = animatedVertices;
            generatedMesh.uv = baseUvs;
            generatedMesh.colors = vertexColors;
            generatedMesh.triangles = baseTriangles;
            generatedMesh.RecalculateBounds();
            generatedMesh.RecalculateNormals();
            meshFilter.sharedMesh = generatedMesh;
        }

        private void AnimateWater(float timeValue)
        {
            if (generatedMesh == null || baseVertices == null || animatedVertices == null || shorelineDistances == null)
            {
                return;
            }

            var normalizedWaveDirection = waveDirection.normalized;
            for (var vertexIndex = 0; vertexIndex < baseVertices.Length; vertexIndex++)
            {
                var baseVertex = baseVertices[vertexIndex];
                var worldPoint = transform.TransformPoint(baseVertex);
                var worldXZ = new Vector2(worldPoint.x, worldPoint.z);
                var shoreDistance = shorelineDistances[vertexIndex];
                var shoreFactor = 1f - Mathf.Clamp01(shoreDistance / shallowDistance);

                var shoreWave = Mathf.Sin((shoreDistance * waveFrequency) - (timeValue * waveSpeed * 2f));
                var openWave = Mathf.Sin((Vector2.Dot(worldXZ, normalizedWaveDirection) * waveFrequency * 0.12f) - (timeValue * waveSpeed));

                animatedVertices[vertexIndex] = baseVertex;
                animatedVertices[vertexIndex].y = seaLevel +
                                                 (shoreWave * waveAmplitude * Mathf.Lerp(0.35f, 1f, shoreFactor)) +
                                                 (openWave * waveAmplitude * openWaterWaveBlend * (1f - (shoreFactor * 0.4f)));

                var depthLerp = Mathf.Clamp01(shoreDistance / shallowDistance);
                var color = Color.Lerp(shallowColor, deepColor, depthLerp);
                var foamPulse = 0.5f + (0.5f * Mathf.Sin((shoreDistance * waveFrequency * 1.9f) - (timeValue * waveSpeed * 2.5f)));
                var foamMask = (1f - Mathf.Clamp01(shoreDistance / foamWidth)) * foamPulse * foamStrength;
                vertexColors[vertexIndex] = Color.Lerp(color, foamColor, foamMask);
            }

            generatedMesh.vertices = animatedVertices;
            generatedMesh.colors = vertexColors;
            generatedMesh.RecalculateBounds();
        }

        private static float ComputeShoreDistance(Vector2 point, List<List<Vector2>> worldPolygons)
        {
            if (worldPolygons.Count == 0)
            {
                return 999f;
            }

            var minDistance = float.MaxValue;
            for (var polygonIndex = 0; polygonIndex < worldPolygons.Count; polygonIndex++)
            {
                var polygon = worldPolygons[polygonIndex];
                if (IsPointInsidePolygon(point, polygon))
                {
                    return 0f;
                }

                for (var edgeIndex = 0; edgeIndex < polygon.Count; edgeIndex++)
                {
                    var a = polygon[edgeIndex];
                    var b = polygon[(edgeIndex + 1) % polygon.Count];
                    minDistance = Mathf.Min(minDistance, DistancePointToSegment(point, a, b));
                }
            }

            return minDistance;
        }

        private static bool IsPointInsidePolygon(Vector2 point, List<Vector2> polygon)
        {
            var inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var intersects = ((polygon[i].y > point.y) != (polygon[j].y > point.y)) &&
                                 (point.x < ((polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y + 0.00001f)) + polygon[i].x);
                if (intersects)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static float DistancePointToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            var segment = end - start;
            if (segment.sqrMagnitude <= Mathf.Epsilon)
            {
                return Vector2.Distance(point, start);
            }

            var t = Vector2.Dot(point - start, segment) / segment.sqrMagnitude;
            t = Mathf.Clamp01(t);
            return Vector2.Distance(point, start + (segment * t));
        }
    }
}
