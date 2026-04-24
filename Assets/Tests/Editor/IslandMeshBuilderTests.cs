using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace Islands.EditorTools.Tests
{
    public sealed class IslandMeshBuilderTests
    {
        private const float HeightTolerance = 0.0001f;
        private const float BoundaryTolerance = 0.0015f;
        private static readonly IslandMeshBuildSettings DefaultSettings = new IslandMeshBuildSettings(2f, 0.5f, 0.1f, 0.01f);

        [Test]
        public void Build_AddsInteriorTopVertices_ForSimpleSquare()
        {
            var outlinePoints = new[]
            {
                new float3(-2f, 0f, -2f),
                new float3(2f, 0f, -2f),
                new float3(2f, 0f, 2f),
                new float3(-2f, 0f, 2f)
            };
            var outline = CreateOutline(outlinePoints);
            var spline = CreateClosedLinearSpline(outlinePoints);

            var result = IslandMeshBuilder.Build(spline, DefaultSettings);

            Assert.That(result.Succeeded, Is.True, result.ValidationMessage);
            Assert.That(CountInteriorTopVertices(result.MeshData, outline), Is.GreaterThan(0));
        }

        [Test]
        public void Build_TriangulatesConcaveOutline_AndKeepsBoundaryOnTheDrawnShape()
        {
            var outlinePoints = new[]
            {
                new float3(-2f, 0f, -1f),
                new float3(0f, 0f, -1f),
                new float3(0f, 0f, -2f),
                new float3(2f, 0f, 0f),
                new float3(0f, 0f, 2f),
                new float3(0f, 0f, 1f),
                new float3(-2f, 0f, 1f)
            };
            var outline = CreateOutline(outlinePoints);
            var spline = CreateClosedLinearSpline(outlinePoints);

            var result = IslandMeshBuilder.Build(spline, DefaultSettings);

            Assert.That(result.Succeeded, Is.True, result.ValidationMessage);

            var boundaryVertices = GetBoundaryTopVertices(result.MeshData);
            Assert.That(boundaryVertices.Count, Is.GreaterThan(0));

            foreach (var boundaryVertex in boundaryVertices)
            {
                Assert.That(DistanceToPolygonBoundary(boundaryVertex, outline), Is.LessThanOrEqualTo(BoundaryTolerance));
            }
        }

        [Test]
        public void Build_GeneratesUpwardTopTriangles_AndDownwardBottomTriangles()
        {
            var spline = CreateClosedLinearSpline(
                new float3(-1f, 0f, -1f),
                new float3(1f, 0f, -1f),
                new float3(1f, 0f, 1f),
                new float3(-1f, 0f, 1f));

            var result = IslandMeshBuilder.Build(spline, DefaultSettings);

            Assert.That(result.Succeeded, Is.True, result.ValidationMessage);

            var topTriangleCount = 0;
            var bottomTriangleCount = 0;
            for (var i = 0; i < result.MeshData.Triangles.Length; i += 3)
            {
                var a = result.MeshData.Vertices[result.MeshData.Triangles[i]];
                var b = result.MeshData.Vertices[result.MeshData.Triangles[i + 1]];
                var c = result.MeshData.Vertices[result.MeshData.Triangles[i + 2]];

                if (AreAllVerticesAtHeight(a, b, c, 0f))
                {
                    var normal = Vector3.Cross(b - a, c - a).normalized;
                    Assert.That(normal.y, Is.GreaterThan(0.99f));
                    topTriangleCount++;
                }

                if (AreAllVerticesAtHeight(a, b, c, -DefaultSettings.Depth))
                {
                    var normal = Vector3.Cross(b - a, c - a).normalized;
                    Assert.That(normal.y, Is.LessThan(-0.99f));
                    bottomTriangleCount++;
                }
            }

            Assert.That(topTriangleCount, Is.GreaterThan(0));
            Assert.That(bottomTriangleCount, Is.GreaterThan(0));
        }

        [Test]
        public void Build_OnlyUsesTopAndBottomHeights()
        {
            var spline = CreateClosedLinearSpline(
                new float3(-1f, 0f, -1f),
                new float3(1f, 0f, -1f),
                new float3(1f, 0f, 1f),
                new float3(-1f, 0f, 1f));

            var result = IslandMeshBuilder.Build(spline, DefaultSettings);

            Assert.That(result.Succeeded, Is.True, result.ValidationMessage);
            Assert.That(GetRoundedHeights(result.MeshData), Is.EquivalentTo(new[] { -2000, 0 }));
        }

        [Test]
        public void Build_SmallerSpacing_IncreasesTopDensity()
        {
            var spline = CreateClosedEllipseSpline(3f, 2f);

            var denseResult = IslandMeshBuilder.Build(spline, CreateSettings(0.35f));
            var sparseResult = IslandMeshBuilder.Build(spline, CreateSettings(0.9f));

            Assert.That(denseResult.Succeeded, Is.True, denseResult.ValidationMessage);
            Assert.That(sparseResult.Succeeded, Is.True, sparseResult.ValidationMessage);

            Assert.That(CountUniqueVerticesAtHeight(denseResult.MeshData, 0f), Is.GreaterThan(CountUniqueVerticesAtHeight(sparseResult.MeshData, 0f)));
            Assert.That(CountTrianglesAtHeight(denseResult.MeshData, 0f), Is.GreaterThan(CountTrianglesAtHeight(sparseResult.MeshData, 0f)));
        }

        [Test]
        public void Build_UsesCleanAuthoringBoundary_ForTopPerimeter()
        {
            var spline = CreateClosedEllipseSpline(3f, 2f);

            var buildResult = IslandMeshBuilder.Build(spline, CreateSettings(1.5f));
            var polygonResult = IslandMeshBuilder.TryBuildTopPolygon(spline, CreateSettings(1.5f), out var polygon, out var validationMessage);

            Assert.That(buildResult.Succeeded, Is.True, buildResult.ValidationMessage);
            Assert.That(polygonResult, Is.True, validationMessage);
            Assert.That(CountTopBoundaryVertices(buildResult.MeshData, polygon), Is.EqualTo(polygon.Count));
        }

        [Test]
        public void Build_RejectsSelfIntersectingOutline()
        {
            var spline = CreateClosedLinearSpline(
                new float3(-1f, 0f, -1f),
                new float3(1f, 0f, 1f),
                new float3(-1f, 0f, 1f),
                new float3(1f, 0f, -1f));

            var result = IslandMeshBuilder.Build(spline, DefaultSettings);

            Assert.That(result.Succeeded, Is.False);
            StringAssert.Contains("intersects", result.ValidationMessage.ToLowerInvariant());
        }

        [Test]
        public void Build_AllowsNearMissConcaveOutline()
        {
            var spline = CreateClosedLinearSpline(
                new float3(0f, 0f, 0f),
                new float3(0.1f, 0f, 0.02f),
                new float3(0.1f, 0f, 1f),
                new float3(2f, 0f, 1f),
                new float3(2f, 0f, 2f),
                new float3(-2f, 0f, 2f),
                new float3(-2f, 0f, 0.04f),
                new float3(0f, 0f, 0.04f));

            var result = IslandMeshBuilder.Build(spline, CreateSettings(0.35f));

            Assert.That(result.Succeeded, Is.True, result.ValidationMessage);
        }

        [TestCase(0.2f)]
        [TestCase(0.35f)]
        [TestCase(0.5f)]
        [TestCase(0.8f)]
        public void Build_NearMissConcaveOutline_RemainsValidAcrossSpacingChanges(float spacing)
        {
            var spline = CreateClosedLinearSpline(
                new float3(0f, 0f, 0f),
                new float3(0.1f, 0f, 0.02f),
                new float3(0.1f, 0f, 1f),
                new float3(2f, 0f, 1f),
                new float3(2f, 0f, 2f),
                new float3(-2f, 0f, 2f),
                new float3(-2f, 0f, 0.04f),
                new float3(0f, 0f, 0.04f));

            var result = IslandMeshBuilder.Build(spline, CreateSettings(spacing));

            Assert.That(result.Succeeded, Is.True, result.ValidationMessage);
        }

        [Test]
        public void TryBuildTopPolygon_DoesNotForceSmoothKnotsIntoBoundary()
        {
            const int knotCount = 12;
            var spline = new Spline(knotCount, true);
            for (var i = 0; i < knotCount; i++)
            {
                var angle = (Mathf.PI * 2f * i) / knotCount;
                spline.Add(
                    new BezierKnot(new float3(Mathf.Cos(angle) * 3f, 0f, Mathf.Sin(angle) * 2f)),
                    TangentMode.AutoSmooth);
            }

            spline.Closed = true;

            var succeeded = IslandMeshBuilder.TryBuildTopPolygon(spline, CreateSettings(4.5f), out var polygon, out var validationMessage);

            Assert.That(succeeded, Is.True, validationMessage);
            Assert.That(polygon.Count, Is.LessThan(knotCount));
        }

        [Test]
        public void Build_UsesCleanAuthoringBoundary_ForSideWalls()
        {
            var spline = CreateClosedEllipseSpline(3f, 2f);

            var buildResult = IslandMeshBuilder.Build(spline, CreateSettings(1.5f));
            var polygonResult = IslandMeshBuilder.TryBuildTopPolygon(spline, CreateSettings(1.5f), out var polygon, out var validationMessage);

            Assert.That(buildResult.Succeeded, Is.True, buildResult.ValidationMessage);
            Assert.That(polygonResult, Is.True, validationMessage);
            Assert.That(GetBoundaryTopVertices(buildResult.MeshData).Count, Is.EqualTo(polygon.Count));
        }

        [Test]
        public void TryBuildTopPolygon_StaysOnTheAuthoringBoundary()
        {
            var spline = CreateClosedEllipseSpline(3f, 2f);

            var buildResult = IslandMeshBuilder.Build(spline, DefaultSettings);
            var polygonResult = IslandMeshBuilder.TryBuildTopPolygon(spline, DefaultSettings, out var polygon, out var validationMessage);

            Assert.That(buildResult.Succeeded, Is.True, buildResult.ValidationMessage);
            Assert.That(polygonResult, Is.True, validationMessage);
            Assert.That(CountUniqueVerticesAtHeight(buildResult.MeshData, 0f), Is.GreaterThan(polygon.Count));
        }

        private static List<Vector2> CreateOutline(params float3[] points)
        {
            var outline = new List<Vector2>(points.Length);
            for (var i = 0; i < points.Length; i++)
            {
                outline.Add(new Vector2(points[i].x, points[i].z));
            }

            return outline;
        }

        private static Spline CreateClosedLinearSpline(params float3[] points)
        {
            var spline = new Spline(points.Length, true);
            for (var i = 0; i < points.Length; i++)
            {
                spline.Add(new BezierKnot(points[i]), TangentMode.Linear);
            }

            spline.Closed = true;
            return spline;
        }

        private static Spline CreateClosedEllipseSpline(float radiusX, float radiusZ)
        {
            const float tangentFactor = 0.55228475f;
            var tangentX = radiusX * tangentFactor;
            var tangentZ = radiusZ * tangentFactor;

            var spline = new Spline(4, true);
            spline.Add(
                new BezierKnot(
                    new float3(radiusX, 0f, 0f),
                    new float3(0f, 0f, -tangentZ),
                    new float3(0f, 0f, tangentZ)),
                TangentMode.Broken);
            spline.Add(
                new BezierKnot(
                    new float3(0f, 0f, radiusZ),
                    new float3(tangentX, 0f, 0f),
                    new float3(-tangentX, 0f, 0f)),
                TangentMode.Broken);
            spline.Add(
                new BezierKnot(
                    new float3(-radiusX, 0f, 0f),
                    new float3(0f, 0f, tangentZ),
                    new float3(0f, 0f, -tangentZ)),
                TangentMode.Broken);
            spline.Add(
                new BezierKnot(
                    new float3(0f, 0f, -radiusZ),
                    new float3(-tangentX, 0f, 0f),
                    new float3(tangentX, 0f, 0f)),
                TangentMode.Broken);

            spline.Closed = true;
            return spline;
        }

        private static IslandMeshBuildSettings CreateSettings(float spacing, float duplicatePointTolerance = 0.01f)
        {
            return new IslandMeshBuildSettings(2f, spacing, 0.1f, duplicatePointTolerance);
        }

        private static int CountInteriorTopVertices(IslandMeshData meshData, IReadOnlyList<Vector2> outline)
        {
            var count = 0;
            foreach (var vertex in GetUniqueVerticesAtHeight(meshData, 0f))
            {
                if (DistanceToPolygonBoundary(vertex, outline) > BoundaryTolerance)
                {
                    count++;
                }
            }

            return count;
        }

        private static List<Vector2> GetBoundaryTopVertices(IslandMeshData meshData)
        {
            var uniquePoints = new Dictionary<Vector2Int, Vector2>();
            for (var i = 0; i < meshData.Triangles.Length; i += 3)
            {
                var a = meshData.Vertices[meshData.Triangles[i]];
                var b = meshData.Vertices[meshData.Triangles[i + 1]];
                var c = meshData.Vertices[meshData.Triangles[i + 2]];
                if (AreAllVerticesAtHeight(a, b, c, 0f) || AreAllVerticesAtHeight(a, b, c, -DefaultSettings.Depth))
                {
                    continue;
                }

                AddIfAtTopHeight(a, uniquePoints);
                AddIfAtTopHeight(b, uniquePoints);
                AddIfAtTopHeight(c, uniquePoints);
            }

            return new List<Vector2>(uniquePoints.Values);
        }

        private static int CountUniqueVerticesAtHeight(IslandMeshData meshData, float height)
        {
            return GetUniqueVerticesAtHeight(meshData, height).Count;
        }

        private static List<Vector2> GetUniqueVerticesAtHeight(IslandMeshData meshData, float height)
        {
            var uniquePoints = new Dictionary<Vector2Int, Vector2>();
            for (var i = 0; i < meshData.Vertices.Length; i++)
            {
                var vertex = meshData.Vertices[i];
                if (Mathf.Abs(vertex.y - height) > HeightTolerance)
                {
                    continue;
                }

                var point = new Vector2(vertex.x, vertex.z);
                uniquePoints[ToQuantizedKey(point)] = point;
            }

            return new List<Vector2>(uniquePoints.Values);
        }

        private static int CountTopBoundaryVertices(IslandMeshData meshData, IReadOnlyList<Vector2> polygon)
        {
            var count = 0;
            foreach (var vertex in GetUniqueVerticesAtHeight(meshData, 0f))
            {
                if (DistanceToPolygonBoundary(vertex, polygon) > BoundaryTolerance)
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private static int CountTrianglesAtHeight(IslandMeshData meshData, float height)
        {
            var count = 0;
            for (var i = 0; i < meshData.Triangles.Length; i += 3)
            {
                var a = meshData.Vertices[meshData.Triangles[i]];
                var b = meshData.Vertices[meshData.Triangles[i + 1]];
                var c = meshData.Vertices[meshData.Triangles[i + 2]];
                if (AreAllVerticesAtHeight(a, b, c, height))
                {
                    count++;
                }
            }

            return count;
        }

        private static HashSet<int> GetRoundedHeights(IslandMeshData meshData)
        {
            var heights = new HashSet<int>();
            for (var i = 0; i < meshData.Vertices.Length; i++)
            {
                heights.Add(Mathf.RoundToInt(meshData.Vertices[i].y * 1000f));
            }

            return heights;
        }

        private static float DistanceToPolygonBoundary(Vector2 point, IReadOnlyList<Vector2> polygon)
        {
            var minDistance = float.MaxValue;
            for (var i = 0; i < polygon.Count; i++)
            {
                var distance = DistancePointToSegment(point, polygon[i], polygon[(i + 1) % polygon.Count]);
                if (distance < minDistance)
                {
                    minDistance = distance;
                }
            }

            return minDistance;
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

        private static bool AreAllVerticesAtHeight(Vector3 a, Vector3 b, Vector3 c, float height)
        {
            return Mathf.Abs(a.y - height) <= HeightTolerance &&
                   Mathf.Abs(b.y - height) <= HeightTolerance &&
                   Mathf.Abs(c.y - height) <= HeightTolerance;
        }

        private static void AddIfAtTopHeight(Vector3 vertex, IDictionary<Vector2Int, Vector2> uniquePoints)
        {
            if (Mathf.Abs(vertex.y) > HeightTolerance)
            {
                return;
            }

            var point = new Vector2(vertex.x, vertex.z);
            uniquePoints[ToQuantizedKey(point)] = point;
        }

        private static Vector2Int ToQuantizedKey(Vector2 point)
        {
            return new Vector2Int(
                Mathf.RoundToInt(point.x * 1000f),
                Mathf.RoundToInt(point.y * 1000f));
        }
    }
}
