using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace Islands.EditorTools.Tests
{
    public sealed class IslandMeshBuilderTests
    {
        private static readonly IslandMeshBuildSettings DefaultSettings =
            new IslandMeshBuildSettings(2f, 3, 0.35f, 0f, 0f, 0.35f, 0.5f, 0.1f, 0.01f);

        [Test]
        public void Build_CreatesTopAndSides_ForSimpleSquare()
        {
            var spline = CreateClosedLinearSpline(
                new float3(-1f, 0f, -1f),
                new float3(1f, 0f, -1f),
                new float3(1f, 0f, 1f),
                new float3(-1f, 0f, 1f));

            var result = IslandMeshBuilder.Build(spline, DefaultSettings);

            Assert.That(result.Succeeded, Is.True, result.ValidationMessage);
            Assert.That(result.MeshData.Vertices.Length, Is.GreaterThan(0));
            Assert.That(result.MeshData.Triangles.Length % 3, Is.EqualTo(0));

            var bounds = new Bounds(result.MeshData.Vertices[0], Vector3.zero);
            for (var i = 1; i < result.MeshData.Vertices.Length; i++)
            {
                bounds.Encapsulate(result.MeshData.Vertices[i]);
            }

            Assert.That(bounds.max.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(bounds.min.y, Is.EqualTo(-2f).Within(0.0001f));
        }

        [Test]
        public void Build_TriangulatesConcaveOutline()
        {
            var spline = CreateClosedLinearSpline(
                new float3(-2f, 0f, -1f),
                new float3(0f, 0f, -1f),
                new float3(0f, 0f, -2f),
                new float3(2f, 0f, 0f),
                new float3(0f, 0f, 2f),
                new float3(0f, 0f, 1f),
                new float3(-2f, 0f, 1f));

            var result = IslandMeshBuilder.Build(spline, DefaultSettings);

            Assert.That(result.Succeeded, Is.True, result.ValidationMessage);
            Assert.That(result.MeshData.Triangles.Length, Is.GreaterThan(0));
        }

        [Test]
        public void Build_GeneratesUpwardTopTriangles_AndNoBottomCap()
        {
            var spline = CreateClosedLinearSpline(
                new float3(-1f, 0f, -1f),
                new float3(1f, 0f, -1f),
                new float3(1f, 0f, 1f),
                new float3(-1f, 0f, 1f));

            var result = IslandMeshBuilder.Build(spline, DefaultSettings);

            Assert.That(result.Succeeded, Is.True, result.ValidationMessage);

            var bottomTriangleCount = 0;
            for (var i = 0; i < result.MeshData.Triangles.Length; i += 3)
            {
                var a = result.MeshData.Vertices[result.MeshData.Triangles[i]];
                var b = result.MeshData.Vertices[result.MeshData.Triangles[i + 1]];
                var c = result.MeshData.Vertices[result.MeshData.Triangles[i + 2]];

                var isTopTriangle = Mathf.Abs(a.y) < 0.0001f && Mathf.Abs(b.y) < 0.0001f && Mathf.Abs(c.y) < 0.0001f;
                if (isTopTriangle)
                {
                    var normal = Vector3.Cross(b - a, c - a).normalized;
                    Assert.That(normal.y, Is.GreaterThan(0.99f));
                }

                var isBottomTriangle = Mathf.Abs(a.y + DefaultSettings.Depth) < 0.0001f &&
                                       Mathf.Abs(b.y + DefaultSettings.Depth) < 0.0001f &&
                                       Mathf.Abs(c.y + DefaultSettings.Depth) < 0.0001f;
                if (isBottomTriangle)
                {
                    bottomTriangleCount++;
                }
            }

            Assert.That(bottomTriangleCount, Is.EqualTo(0));
        }

        [Test]
        public void Build_CreatesStructuredTopBand_WithInnerTopRingVertices()
        {
            var spline = CreateClosedLinearSpline(
                new float3(-1f, 0f, -1f),
                new float3(1f, 0f, -1f),
                new float3(1f, 0f, 1f),
                new float3(-1f, 0f, 1f));

            var result = IslandMeshBuilder.Build(spline, DefaultSettings);

            Assert.That(result.Succeeded, Is.True, result.ValidationMessage);
            Assert.That(CountUniqueTopOutlinePoints(result.MeshData), Is.GreaterThan(4));

            var innerRingVertexCount = 0;
            for (var i = 0; i < result.MeshData.Vertices.Length; i++)
            {
                var vertex = result.MeshData.Vertices[i];
                if (Mathf.Abs(vertex.y) > 0.0001f)
                {
                    continue;
                }

                if (Mathf.Abs(vertex.x) < 0.9f && Mathf.Abs(vertex.z) < 0.9f)
                {
                    innerRingVertexCount++;
                }
            }

            Assert.That(innerRingVertexCount, Is.GreaterThan(0));
        }

        [Test]
        public void Build_CreatesHorizontalTerraceLedges()
        {
            var spline = CreateClosedLinearSpline(
                new float3(-1f, 0f, -1f),
                new float3(1f, 0f, -1f),
                new float3(1f, 0f, 1f),
                new float3(-1f, 0f, 1f));

            var result = IslandMeshBuilder.Build(spline, DefaultSettings);

            Assert.That(result.Succeeded, Is.True, result.ValidationMessage);

            var ledgeTriangleCount = 0;
            for (var i = 0; i < result.MeshData.Triangles.Length; i += 3)
            {
                var a = result.MeshData.Vertices[result.MeshData.Triangles[i]];
                var b = result.MeshData.Vertices[result.MeshData.Triangles[i + 1]];
                var c = result.MeshData.Vertices[result.MeshData.Triangles[i + 2]];

                var sameHeight = Mathf.Abs(a.y - b.y) < 0.0001f && Mathf.Abs(b.y - c.y) < 0.0001f;
                var isIntermediateHeight = a.y < -0.0001f && a.y > -DefaultSettings.Depth + 0.0001f;
                if (!sameHeight || !isIntermediateHeight)
                {
                    continue;
                }

                var normal = Vector3.Cross(b - a, c - a).normalized;
                if (normal.y > 0.99f)
                {
                    ledgeTriangleCount++;
                }
            }

            Assert.That(ledgeTriangleCount, Is.GreaterThan(0));
        }

        [Test]
        public void Build_ExpandsTerracesOutward_AsTheyDescend()
        {
            var spline = CreateClosedLinearSpline(
                new float3(-1f, 0f, -1f),
                new float3(1f, 0f, -1f),
                new float3(1f, 0f, 1f),
                new float3(-1f, 0f, 1f));

            var result = IslandMeshBuilder.Build(spline, DefaultSettings);

            Assert.That(result.Succeeded, Is.True, result.ValidationMessage);

            var bounds = new Bounds(result.MeshData.Vertices[0], Vector3.zero);
            for (var i = 1; i < result.MeshData.Vertices.Length; i++)
            {
                bounds.Encapsulate(result.MeshData.Vertices[i]);
            }

            Assert.That(bounds.min.x, Is.LessThan(-1f));
            Assert.That(bounds.max.x, Is.GreaterThan(1f));
            Assert.That(bounds.min.z, Is.LessThan(-1f));
            Assert.That(bounds.max.z, Is.GreaterThan(1f));
        }

        [Test]
        public void Build_MergesNarrowConcavities_IntoSmoothTerraces()
        {
            var settings = new IslandMeshBuildSettings(2f, 2, 0.6f, 0f, 0f, 0.8f, 0.25f, 0.1f, 0.01f);
            var spline = CreateClosedLinearSpline(
                new float3(-3f, 0f, -2f),
                new float3(3f, 0f, -2f),
                new float3(3f, 0f, 2f),
                new float3(0.5f, 0f, 2f),
                new float3(0.5f, 0f, 0.3f),
                new float3(-0.5f, 0f, 0.3f),
                new float3(-0.5f, 0f, 2f),
                new float3(-3f, 0f, 2f));

            var result = IslandMeshBuilder.Build(spline, settings);

            Assert.That(result.Succeeded, Is.True, result.ValidationMessage);

            var bridgedVertexCount = 0;
            for (var i = 0; i < result.MeshData.Vertices.Length; i++)
            {
                var vertex = result.MeshData.Vertices[i];
                if (vertex.y < -0.0001f && vertex.z > 2.2f && Mathf.Abs(vertex.x) < 0.2f)
                {
                    bridgedVertexCount++;
                }
            }

            Assert.That(bridgedVertexCount, Is.GreaterThan(0), "Expected lower terraces to smooth over the narrow notch.");
        }

        [Test]
        public void Build_AllowsShortEdgesThatOnlyNearMissOtherSegments()
        {
            var settings = new IslandMeshBuildSettings(2f, 1, 0.35f, 0f, 0f, 0.35f, 0.5f, 0.1f, 0.025f);
            var spline = CreateClosedLinearSpline(
                new float3(0f, 0f, 0f),
                new float3(0.1f, 0f, 0.02f),
                new float3(0.1f, 0f, 1f),
                new float3(2f, 0f, 1f),
                new float3(2f, 0f, 2f),
                new float3(-2f, 0f, 2f),
                new float3(-2f, 0f, 0.04f),
                new float3(0f, 0f, 0.04f));

            var result = IslandMeshBuilder.Build(spline, settings);

            Assert.That(result.Succeeded, Is.True, result.ValidationMessage);
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
        public void Build_EdgeZoneSilhouetteOffset_ReshapesLocalCoastline()
        {
            var spline = CreateClosedLinearSpline(
                new float3(-1f, 0f, -1f),
                new float3(1f, 0f, -1f),
                new float3(1f, 0f, 1f),
                new float3(-1f, 0f, 1f));

            var baselineResult = IslandMeshBuilder.Build(spline, CreateStructuredSettings(0.25f));
            var sculptedResult = IslandMeshBuilder.Build(
                spline,
                CreateStructuredSettings(
                    0.25f,
                    CreateEdgeZone(0.375f, 0.35f, 0.4f, 0f, 1f, 1f)));

            Assert.That(baselineResult.Succeeded, Is.True, baselineResult.ValidationMessage);
            Assert.That(sculptedResult.Succeeded, Is.True, sculptedResult.ValidationMessage);

            var baselineTopBounds = GetTopBounds(baselineResult.MeshData);
            var sculptedTopBounds = GetTopBounds(sculptedResult.MeshData);

            Assert.That(sculptedTopBounds.max.x, Is.GreaterThan(baselineTopBounds.max.x + 0.15f));
            Assert.That(sculptedTopBounds.min.x, Is.EqualTo(baselineTopBounds.min.x).Within(0.05f));
        }

        [Test]
        public void Build_EdgeZoneCoastBandDelta_ChangesLocalInnerRingWidth()
        {
            var spline = CreateClosedLinearSpline(
                new float3(-1f, 0f, -1f),
                new float3(1f, 0f, -1f),
                new float3(1f, 0f, 1f),
                new float3(-1f, 0f, 1f));

            var baselineResult = IslandMeshBuilder.Build(
                spline,
                CreateStructuredSettings(5f, coastBandWidth: 0.3f));
            var widenedBandResult = IslandMeshBuilder.Build(
                spline,
                CreateStructuredSettings(
                    5f,
                    CreateEdgeZone(0.375f, 0.55f, 0f, 0.5f, 1f, 1f),
                    0.3f));

            Assert.That(baselineResult.Succeeded, Is.True, baselineResult.ValidationMessage);
            Assert.That(widenedBandResult.Succeeded, Is.True, widenedBandResult.ValidationMessage);

            var baselineInnerMaxX = GetLargestInnerTopX(baselineResult.MeshData);
            var widenedInnerMaxX = GetLargestInnerTopX(widenedBandResult.MeshData);

            Assert.That(widenedInnerMaxX, Is.LessThan(baselineInnerMaxX - 0.05f));
        }

        [Test]
        public void Build_EdgeZoneTerraceWidthScale_WidensTerracesLocally()
        {
            var spline = CreateClosedLinearSpline(
                new float3(-1f, 0f, -1f),
                new float3(1f, 0f, -1f),
                new float3(1f, 0f, 1f),
                new float3(-1f, 0f, 1f));

            var baselineResult = IslandMeshBuilder.Build(spline, CreateStructuredSettings(0.25f));
            var widenedTerraceResult = IslandMeshBuilder.Build(
                spline,
                CreateStructuredSettings(
                    0.25f,
                    CreateEdgeZone(0.375f, 0.35f, 0f, 0f, 1.8f, 1f)));

            Assert.That(baselineResult.Succeeded, Is.True, baselineResult.ValidationMessage);
            Assert.That(widenedTerraceResult.Succeeded, Is.True, widenedTerraceResult.ValidationMessage);

            var baselineTopBounds = GetTopBounds(baselineResult.MeshData);
            var widenedTopBounds = GetTopBounds(widenedTerraceResult.MeshData);
            var baselineLowerMaxX = GetMaxXBelowHeight(baselineResult.MeshData, -0.2f);
            var widenedLowerMaxX = GetMaxXBelowHeight(widenedTerraceResult.MeshData, -0.2f);

            Assert.That(widenedTopBounds.max.x, Is.EqualTo(baselineTopBounds.max.x).Within(0.05f));
            Assert.That(widenedLowerMaxX, Is.GreaterThan(baselineLowerMaxX + 0.05f));
        }

        [Test]
        public void Build_IncreasingSpacing_ReducesBasePoints_OnBroadCurves()
        {
            var spline = CreateClosedEllipseSpline(3f, 2f);

            var denseResult = IslandMeshBuilder.Build(spline, CreateSamplingSettings(0.25f));
            var sparseResult = IslandMeshBuilder.Build(spline, CreateSamplingSettings(0.75f));

            Assert.That(denseResult.Succeeded, Is.True, denseResult.ValidationMessage);
            Assert.That(sparseResult.Succeeded, Is.True, sparseResult.ValidationMessage);
            Assert.That(CountUniqueTopOutlinePoints(denseResult.MeshData), Is.GreaterThan(CountUniqueTopOutlinePoints(sparseResult.MeshData)));
        }

        [Test]
        public void Build_DecreasingSpacing_MonotonicallyIncreasesBasePoints_OnBroadCurves()
        {
            var spline = CreateClosedEllipseSpline(3f, 2f);

            var coarseResult = IslandMeshBuilder.Build(spline, CreateSamplingSettings(1.2f));
            var mediumResult = IslandMeshBuilder.Build(spline, CreateSamplingSettings(0.6f));
            var denseResult = IslandMeshBuilder.Build(spline, CreateSamplingSettings(0.3f));

            Assert.That(coarseResult.Succeeded, Is.True, coarseResult.ValidationMessage);
            Assert.That(mediumResult.Succeeded, Is.True, mediumResult.ValidationMessage);
            Assert.That(denseResult.Succeeded, Is.True, denseResult.ValidationMessage);

            var coarseCount = CountUniqueTopOutlinePoints(coarseResult.MeshData);
            var mediumCount = CountUniqueTopOutlinePoints(mediumResult.MeshData);
            var denseCount = CountUniqueTopOutlinePoints(denseResult.MeshData);

            Assert.That(mediumCount, Is.GreaterThan(coarseCount));
            Assert.That(denseCount, Is.GreaterThan(mediumCount));
        }

        [Test]
        public void Build_SpacingMaintainsConsistentWorldSpaceDensity_AcrossScales()
        {
            var smallSpline = CreateClosedLinearSpline(
                new float3(-1f, 0f, -1f),
                new float3(1f, 0f, -1f),
                new float3(1f, 0f, 1f),
                new float3(-1f, 0f, 1f));
            var largeSpline = CreateClosedLinearSpline(
                new float3(-2f, 0f, -2f),
                new float3(2f, 0f, -2f),
                new float3(2f, 0f, 2f),
                new float3(-2f, 0f, 2f));

            var settings = CreateSamplingSettings(0.5f);
            var smallResult = IslandMeshBuilder.Build(smallSpline, settings);
            var largeResult = IslandMeshBuilder.Build(largeSpline, settings);

            Assert.That(smallResult.Succeeded, Is.True, smallResult.ValidationMessage);
            Assert.That(largeResult.Succeeded, Is.True, largeResult.ValidationMessage);

            var smallPointCount = CountUniqueTopOutlinePoints(smallResult.MeshData);
            var largePointCount = CountUniqueTopOutlinePoints(largeResult.MeshData);
            var ratio = largePointCount / (float)smallPointCount;

            Assert.That(largePointCount, Is.GreaterThan(smallPointCount));
            Assert.That(ratio, Is.EqualTo(2f).Within(0.15f));
        }

        [Test]
        public void Build_HighDuplicateTolerance_DoesNotInvertSpacingDensity_OnBroadCurves()
        {
            var spline = CreateClosedEllipseSpline(3f, 2f);
            var looseSettings = new IslandMeshBuildSettings(2f, 1, 0.35f, 0f, 0f, 0.35f, 0.6f, 0.1f, 0.1f);
            var denseSettings = new IslandMeshBuildSettings(2f, 1, 0.35f, 0f, 0f, 0.35f, 0.3f, 0.1f, 0.1f);

            var looseResult = IslandMeshBuilder.Build(spline, looseSettings);
            var denseResult = IslandMeshBuilder.Build(spline, denseSettings);

            Assert.That(looseResult.Succeeded, Is.True, looseResult.ValidationMessage);
            Assert.That(denseResult.Succeeded, Is.True, denseResult.ValidationMessage);
            Assert.That(CountUniqueTopOutlinePoints(denseResult.MeshData), Is.GreaterThan(CountUniqueTopOutlinePoints(looseResult.MeshData)));
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

        private static IslandMeshBuildSettings CreateSamplingSettings(float spacing)
        {
            return new IslandMeshBuildSettings(2f, 1, 0.35f, 0f, 0f, 0.35f, spacing, 0.1f, 0.01f);
        }

        private static IslandMeshBuildSettings CreateStructuredSettings(float spacing, IslandEdgeZone? edgeZone = null, float coastBandWidth = 0.45f)
        {
            var edgeZones = edgeZone.HasValue ? new List<IslandEdgeZone> { edgeZone.Value } : new List<IslandEdgeZone>();
            return new IslandMeshBuildSettings(2f, 3, 0.35f, 0f, 0f, 0.35f, spacing, coastBandWidth, edgeZones, 0.1f, 0.01f);
        }

        private static IslandEdgeZone CreateEdgeZone(
            float centerNormalized,
            float spanNormalized,
            float silhouetteOffset,
            float coastBandWidthDelta,
            float terraceWidthScale,
            float terraceSoftnessScale)
        {
            var edgeZone = IslandEdgeZonePresets.CreateDefault();
            edgeZone.CenterNormalized = centerNormalized;
            edgeZone.SpanNormalized = spanNormalized;
            edgeZone.SilhouetteOffset = silhouetteOffset;
            edgeZone.CoastBandWidthDelta = coastBandWidthDelta;
            edgeZone.TerraceWidthScale = terraceWidthScale;
            edgeZone.TerraceSoftnessScale = terraceSoftnessScale;
            return edgeZone;
        }

        private static int CountUniqueTopOutlinePoints(IslandMeshData meshData)
        {
            var uniquePoints = new HashSet<Vector2Int>();
            for (var i = 0; i < meshData.Vertices.Length; i++)
            {
                var vertex = meshData.Vertices[i];
                if (Mathf.Abs(vertex.y) > 0.0001f)
                {
                    continue;
                }

                uniquePoints.Add(new Vector2Int(
                    Mathf.RoundToInt(vertex.x * 1000f),
                    Mathf.RoundToInt(vertex.z * 1000f)));
            }

            return uniquePoints.Count;
        }

        private static Bounds GetTopBounds(IslandMeshData meshData)
        {
            var hasTopVertex = false;
            var bounds = default(Bounds);
            for (var i = 0; i < meshData.Vertices.Length; i++)
            {
                var vertex = meshData.Vertices[i];
                if (Mathf.Abs(vertex.y) > 0.0001f)
                {
                    continue;
                }

                if (!hasTopVertex)
                {
                    bounds = new Bounds(vertex, Vector3.zero);
                    hasTopVertex = true;
                }
                else
                {
                    bounds.Encapsulate(vertex);
                }
            }

            Assert.That(hasTopVertex, Is.True, "Expected at least one top-surface vertex.");
            return bounds;
        }

        private static float GetMaxXBelowHeight(IslandMeshData meshData, float maxHeight)
        {
            var bestX = float.MinValue;
            for (var i = 0; i < meshData.Vertices.Length; i++)
            {
                var vertex = meshData.Vertices[i];
                if (vertex.y >= maxHeight)
                {
                    continue;
                }

                if (vertex.x > bestX)
                {
                    bestX = vertex.x;
                }
            }

            Assert.That(bestX, Is.GreaterThan(float.MinValue));
            return bestX;
        }

        private static float GetLargestInnerTopX(IslandMeshData meshData)
        {
            var topBounds = GetTopBounds(meshData);
            var bestInnerX = float.MinValue;
            for (var i = 0; i < meshData.Vertices.Length; i++)
            {
                var vertex = meshData.Vertices[i];
                if (Mathf.Abs(vertex.y) > 0.0001f || vertex.x >= topBounds.max.x - 0.05f)
                {
                    continue;
                }

                if (vertex.x > bestInnerX)
                {
                    bestInnerX = vertex.x;
                }
            }

            Assert.That(bestInnerX, Is.GreaterThan(float.MinValue));
            return bestInnerX;
        }
    }
}

