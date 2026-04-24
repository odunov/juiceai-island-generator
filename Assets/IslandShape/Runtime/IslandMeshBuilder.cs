using System;
using System.Collections.Generic;
using andywiecko.BurstTriangulator;
using Clipper2Lib;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace Islands.EditorTools
{
    public readonly struct IslandMeshBuildSettings
    {
        public IslandMeshBuildSettings(
            float depth,
            float spacing,
            float minimumArea,
            float duplicatePointTolerance)
        {
            Depth = depth;
            Spacing = spacing;
            MinimumArea = minimumArea;
            DuplicatePointTolerance = duplicatePointTolerance;
        }

        public float Depth { get; }

        public float Spacing { get; }

        public float MinimumArea { get; }

        public float DuplicatePointTolerance { get; }
    }

    public readonly struct IslandMeshBuildResult
    {
        public IslandMeshBuildResult(IslandMeshData meshData)
        {
            MeshData = meshData;
            ValidationMessage = string.Empty;
        }

        public IslandMeshBuildResult(string validationMessage)
        {
            MeshData = null;
            ValidationMessage = validationMessage ?? "Island mesh generation failed.";
        }

        public IslandMeshData MeshData { get; }

        public string ValidationMessage { get; }

        public bool Succeeded => MeshData != null;
    }

    public sealed class IslandMeshData
    {
        public IslandMeshData(Vector3[] vertices, Vector3[] normals, Vector2[] uv, int[] triangles)
        {
            Vertices = vertices;
            Normals = normals;
            UV = uv;
            Triangles = triangles;
        }

        public Vector3[] Vertices { get; }

        public Vector3[] Normals { get; }

        public Vector2[] UV { get; }

        public int[] Triangles { get; }
    }

    internal sealed class TopMeshData
    {
        public TopMeshData(Vector2[] positions, int[] triangles)
        {
            Positions = positions;
            Triangles = triangles;
        }

        public Vector2[] Positions { get; }

        public int[] Triangles { get; }
    }

    public static class IslandMeshBuilder
    {
        private const float CornerDotThreshold = 0.9961947f;
        private const float HexRowFactor = 0.8660254f;
        private const float InteriorSeedClearanceFactor = 0.2f;
        private const int MaxBaseSamplesPerCurve = 2048;
        private const int TopologyPrecision = 6;
        private const float MinimumDimension = 0.0001f;

        public static IslandMeshBuildResult Build(Spline spline, IslandMeshBuildSettings settings)
        {
            if (spline == null)
            {
                return new IslandMeshBuildResult("Island shape is missing a spline.");
            }

            if (!spline.Closed)
            {
                return new IslandMeshBuildResult("Close the spline loop before generating an island mesh.");
            }

            if (spline.Count < 3)
            {
                return new IslandMeshBuildResult("An island needs at least three knots.");
            }

            var spacing = Mathf.Max(settings.Spacing, 0.01f);
            var minimumArea = Mathf.Max(settings.MinimumArea, 0.0001f);
            var duplicatePointTolerance = Mathf.Max(settings.DuplicatePointTolerance, 0.0001f);
            var collinearTolerance = GetCollinearTolerance(spacing);
            var validationTolerance = GetValidationTolerance(collinearTolerance, spacing);
            var depth = Mathf.Max(settings.Depth, 0.01f);

            if (!TryBuildValidatedPolygonFromSpline(
                    spline,
                    spacing,
                    duplicatePointTolerance,
                    collinearTolerance,
                    validationTolerance,
                    minimumArea,
                    out var polygon,
                    out var validationMessage))
            {
                return new IslandMeshBuildResult(validationMessage);
            }

            if (!TryBuildTopMesh(polygon, spacing, out var topMesh, out validationMessage))
            {
                return new IslandMeshBuildResult(validationMessage);
            }

            return new IslandMeshBuildResult(BuildExtrudedMesh(topMesh, polygon, depth));
        }

        public static bool TryBuildTopPolygon(
            Spline spline,
            IslandMeshBuildSettings settings,
            out List<Vector2> polygon,
            out string validationMessage)
        {
            if (spline == null)
            {
                polygon = null;
                validationMessage = "Island shape is missing a spline.";
                return false;
            }

            if (!spline.Closed)
            {
                polygon = null;
                validationMessage = "Close the spline loop before generating an island mesh.";
                return false;
            }

            if (spline.Count < 3)
            {
                polygon = null;
                validationMessage = "An island needs at least three knots.";
                return false;
            }

            var spacing = Mathf.Max(settings.Spacing, 0.01f);
            var minimumArea = Mathf.Max(settings.MinimumArea, 0.0001f);
            var duplicatePointTolerance = Mathf.Max(settings.DuplicatePointTolerance, 0.0001f);
            var collinearTolerance = GetCollinearTolerance(spacing);
            var validationTolerance = GetValidationTolerance(collinearTolerance, spacing);

            return TryBuildValidatedPolygonFromSpline(
                spline,
                spacing,
                duplicatePointTolerance,
                collinearTolerance,
                validationTolerance,
                minimumArea,
                out polygon,
                out validationMessage);
        }

        private static bool TryBuildValidatedPolygonFromSpline(
            Spline spline,
            float spacing,
            float duplicatePointTolerance,
            float collinearTolerance,
            float validationTolerance,
            float minimumArea,
            out List<Vector2> polygon,
            out string validationMessage)
        {
            var baseSilhouette = BuildBaseSilhouettePolyline(spline, spacing);
            if (TryValidateAndNormalizePolygon(
                    baseSilhouette,
                    duplicatePointTolerance,
                    collinearTolerance,
                    validationTolerance,
                    minimumArea,
                    out polygon,
                    out validationMessage))
            {
                return true;
            }

            if (validationMessage != "The island outline intersects itself.")
            {
                return false;
            }

            var refinedSpacing = Mathf.Max(0.01f, spacing * 0.5f);
            if (refinedSpacing >= spacing)
            {
                return false;
            }

            var refinedCollinearTolerance = GetCollinearTolerance(refinedSpacing);
            var refinedValidationTolerance = GetValidationTolerance(refinedCollinearTolerance, refinedSpacing);
            var refinedSilhouette = BuildBaseSilhouettePolyline(spline, refinedSpacing);
            return TryValidateAndNormalizePolygon(
                refinedSilhouette,
                duplicatePointTolerance,
                refinedCollinearTolerance,
                refinedValidationTolerance,
                minimumArea,
                out polygon,
                out validationMessage);
        }

        private static List<Vector3> BuildBaseSilhouettePolyline(Spline spline, float spacing)
        {
            spline.Warmup();

            var sampledPoints = new List<Vector3>();
            var curveCount = SplineUtility.GetCurveCount(spline);
            if (curveCount <= 0)
            {
                return sampledPoints;
            }

            var totalLength = spline.GetLength();
            if (totalLength <= MinimumDimension)
            {
                return sampledPoints;
            }

            var sampleCount = Mathf.Max(3, Mathf.Min(MaxBaseSamplesPerCurve * curveCount, Mathf.CeilToInt(totalLength / Mathf.Max(spacing, 0.01f))));
            var sampleDistances = new List<float>(sampleCount + spline.Count) { 0f };
            var sampleStep = totalLength / sampleCount;
            for (var sampleIndex = 1; sampleIndex < sampleCount; sampleIndex++)
            {
                sampleDistances.Add(sampleIndex * sampleStep);
            }

            var knotDistance = 0f;
            for (var curveIndex = 0; curveIndex < curveCount; curveIndex++)
            {
                knotDistance += spline.GetCurveLength(curveIndex);
                if (knotDistance >= totalLength - MinimumDimension)
                {
                    break;
                }

                var knotIndex = (curveIndex + 1) % spline.Count;
                if (ShouldForceKnotSample(spline, knotIndex))
                {
                    sampleDistances.Add(knotDistance);
                }
            }

            sampleDistances.Sort();

            for (var i = 0; i < sampleDistances.Count; i++)
            {
                var normalizedT = spline.ConvertIndexUnit(sampleDistances[i], PathIndexUnit.Distance, PathIndexUnit.Normalized);
                var point = ToVector3(spline.EvaluatePosition(normalizedT));
                if (sampledPoints.Count > 0 && Vector3.SqrMagnitude(sampledPoints[sampledPoints.Count - 1] - point) <= MinimumDimension * MinimumDimension)
                {
                    continue;
                }

                sampledPoints.Add(point);
            }

            return sampledPoints;
        }

        private static bool ShouldForceKnotSample(Spline spline, int knotIndex)
        {
            if (spline == null || spline.Count < 2)
            {
                return false;
            }

            if (spline.GetTangentMode(knotIndex) == TangentMode.Linear)
            {
                return true;
            }

            var knot = spline[knotIndex];
            var incoming = new Vector2(-knot.TangentIn.x, -knot.TangentIn.z);
            var outgoing = new Vector2(knot.TangentOut.x, knot.TangentOut.z);
            if (incoming.sqrMagnitude <= MinimumDimension * MinimumDimension ||
                outgoing.sqrMagnitude <= MinimumDimension * MinimumDimension)
            {
                return true;
            }

            return Vector2.Dot(incoming.normalized, outgoing.normalized) < CornerDotThreshold;
        }

        private static bool TryValidateAndNormalizePolygon(
            List<Vector3> sampledPoints,
            float duplicatePointTolerance,
            float collinearTolerance,
            float validationTolerance,
            float minimumArea,
            out List<Vector2> polygon,
            out string validationMessage)
        {
            validationMessage = string.Empty;

            if (sampledPoints == null || sampledPoints.Count < 3)
            {
                polygon = null;
                validationMessage = "The sampled island outline is too small to triangulate.";
                return false;
            }

            polygon = NormalizePolygon(sampledPoints, duplicatePointTolerance);
            if (polygon.Count < 3)
            {
                validationMessage = "The island outline collapsed after removing duplicate points.";
                return false;
            }

            var signedArea = SignedArea(polygon);
            if (Mathf.Abs(signedArea) < minimumArea)
            {
                validationMessage = "The island outline is too small.";
                return false;
            }

            if (signedArea < 0f)
            {
                polygon.Reverse();
            }

            RemoveCollinearVertices(polygon, collinearTolerance);
            if (polygon.Count < 3 || HasCollapsedEdges(polygon, duplicatePointTolerance))
            {
                validationMessage = "The island outline became degenerate after simplification.";
                return false;
            }

            if (!TryResolvePolygonTopology(
                    polygon,
                    duplicatePointTolerance,
                    collinearTolerance,
                    validationTolerance,
                    minimumArea,
                    out polygon))
            {
                validationMessage = "The island outline intersects itself.";
                return false;
            }

            return true;
        }

        private static bool TryBuildTopMesh(
            List<Vector2> polygon,
            float spacing,
            out TopMeshData topMesh,
            out string validationMessage)
        {
            topMesh = null;
            validationMessage = string.Empty;

            var interiorSeeds = CollectInteriorSeedPoints(polygon, spacing);
            var positions = new NativeArray<double2>(polygon.Count + interiorSeeds.Count, Allocator.TempJob);
            var constraintEdges = new NativeArray<int>(polygon.Count * 2, Allocator.TempJob);

            try
            {
                for (var pointIndex = 0; pointIndex < polygon.Count; pointIndex++)
                {
                    positions[pointIndex] = new double2(polygon[pointIndex].x, polygon[pointIndex].y);

                    var edgeIndex = pointIndex * 2;
                    constraintEdges[edgeIndex] = pointIndex;
                    constraintEdges[edgeIndex + 1] = (pointIndex + 1) % polygon.Count;
                }

                for (var seedIndex = 0; seedIndex < interiorSeeds.Count; seedIndex++)
                {
                    var point = interiorSeeds[seedIndex];
                    positions[polygon.Count + seedIndex] = new double2(point.x, point.y);
                }

                using var triangulator = new Triangulator(Allocator.TempJob)
                {
                    Input =
                    {
                        Positions = positions,
                        ConstraintEdges = constraintEdges
                    },
                    Settings =
                    {
                        RestoreBoundary = true,
                        RefineMesh = false,
                        AutoHolesAndBoundary = false,
                        ValidateInput = false,
                        Verbose = false
                    }
                };

                triangulator.Run();

                if (triangulator.Output.Status.Value != Status.OK)
                {
                    validationMessage = "The island outline could not be triangulated.";
                    return false;
                }

                var outputPositions = triangulator.Output.Positions.AsArray();
                var outputTriangles = triangulator.Output.Triangles.AsArray();
                var outputHalfedges = triangulator.Output.Halfedges.AsArray();

                if (outputPositions.Length < 3 || outputTriangles.Length < 3)
                {
                    validationMessage = "The triangulated island top did not contain enough geometry.";
                    return false;
                }

                if (!TryExtractBoundaryLoop(
                        outputPositions,
                        outputTriangles,
                        outputHalfedges,
                        out var boundaryLoop))
                {
                    validationMessage = "The triangulated island boundary could not be reconstructed.";
                    return false;
                }

                if (!MatchesBoundaryPolygon(boundaryLoop, polygon, Mathf.Max(0.0005f, spacing * 0.01f)))
                {
                    validationMessage = "The triangulated island top did not preserve the clean authoring boundary.";
                    return false;
                }

                var topPositions = new Vector2[outputPositions.Length];
                for (var pointIndex = 0; pointIndex < outputPositions.Length; pointIndex++)
                {
                    topPositions[pointIndex] = ToVector2(outputPositions[pointIndex]);
                }

                var topTriangles = new int[outputTriangles.Length];
                outputTriangles.CopyTo(topTriangles);

                topMesh = new TopMeshData(topPositions, topTriangles);
                return true;
            }
            finally
            {
                if (constraintEdges.IsCreated)
                {
                    constraintEdges.Dispose();
                }

                if (positions.IsCreated)
                {
                    positions.Dispose();
                }
            }
        }

        private static bool TryExtractBoundaryLoop(
            NativeArray<double2> positions,
            NativeArray<int> triangles,
            NativeArray<int> halfedges,
            out Vector2[] boundaryLoop)
        {
            boundaryLoop = null;

            if (!positions.IsCreated || !triangles.IsCreated || !halfedges.IsCreated || triangles.Length != halfedges.Length)
            {
                return false;
            }

            var nextByVertex = new Dictionary<int, int>();
            var incomingCounts = new Dictionary<int, int>();
            var startVertex = -1;

            for (var halfedgeIndex = 0; halfedgeIndex < halfedges.Length; halfedgeIndex++)
            {
                if (halfedges[halfedgeIndex] != -1)
                {
                    continue;
                }

                var edgeStart = triangles[halfedgeIndex];
                var edgeEnd = triangles[GetNextHalfedgeIndex(halfedgeIndex)];
                if (edgeStart == edgeEnd || nextByVertex.ContainsKey(edgeStart))
                {
                    return false;
                }

                nextByVertex.Add(edgeStart, edgeEnd);
                incomingCounts[edgeEnd] = incomingCounts.TryGetValue(edgeEnd, out var existingIncomingCount)
                    ? existingIncomingCount + 1
                    : 1;

                if (startVertex < 0)
                {
                    startVertex = edgeStart;
                }
            }

            if (startVertex < 0 || nextByVertex.Count < 3)
            {
                return false;
            }

            foreach (var edge in nextByVertex)
            {
                if (!incomingCounts.TryGetValue(edge.Key, out var incomingCount) || incomingCount != 1)
                {
                    return false;
                }
            }

            var boundaryIndices = new List<int>(nextByVertex.Count);
            var currentVertex = startVertex;

            for (var step = 0; step < nextByVertex.Count; step++)
            {
                if (!nextByVertex.TryGetValue(currentVertex, out var nextVertex))
                {
                    return false;
                }

                boundaryIndices.Add(currentVertex);
                currentVertex = nextVertex;

                if (currentVertex == startVertex)
                {
                    break;
                }
            }

            if (currentVertex != startVertex || boundaryIndices.Count != nextByVertex.Count)
            {
                return false;
            }

            boundaryLoop = new Vector2[boundaryIndices.Count];
            for (var index = 0; index < boundaryIndices.Count; index++)
            {
                boundaryLoop[index] = ToVector2(positions[boundaryIndices[index]]);
            }

            if (SignedArea(boundaryLoop) < 0f)
            {
                Array.Reverse(boundaryLoop);
            }

            return true;
        }

        private static bool MatchesBoundaryPolygon(
            IReadOnlyList<Vector2> boundaryLoop,
            IReadOnlyList<Vector2> polygon,
            float tolerance)
        {
            if (boundaryLoop.Count != polygon.Count)
            {
                return false;
            }

            for (var polygonStart = 0; polygonStart < polygon.Count; polygonStart++)
            {
                if (!NearlyEqual(boundaryLoop[0], polygon[polygonStart], tolerance))
                {
                    continue;
                }

                var matchesForward = true;
                for (var i = 0; i < polygon.Count; i++)
                {
                    if (NearlyEqual(boundaryLoop[i], polygon[(polygonStart + i) % polygon.Count], tolerance))
                    {
                        continue;
                    }

                    matchesForward = false;
                    break;
                }

                if (matchesForward)
                {
                    return true;
                }

                var matchesReverse = true;
                for (var i = 0; i < polygon.Count; i++)
                {
                    var polygonIndex = polygonStart - i;
                    if (polygonIndex < 0)
                    {
                        polygonIndex += polygon.Count;
                    }

                    if (NearlyEqual(boundaryLoop[i], polygon[polygonIndex], tolerance))
                    {
                        continue;
                    }

                    matchesReverse = false;
                    break;
                }

                if (matchesReverse)
                {
                    return true;
                }
            }

            return false;
        }

        private static IslandMeshData BuildExtrudedMesh(TopMeshData topMesh, IReadOnlyList<Vector2> sideWallLoop, float depth)
        {
            GetPolygonBounds(sideWallLoop, out var min, out var max);
            var size = Vector2.Max(max - min, new Vector2(MinimumDimension, MinimumDimension));

            var vertices = new List<Vector3>((topMesh.Positions.Length * 2) + (sideWallLoop.Count * 4));
            var normals = new List<Vector3>(vertices.Capacity);
            var uv = new List<Vector2>(vertices.Capacity);
            var triangles = new List<int>((topMesh.Triangles.Length * 2) + (sideWallLoop.Count * 6));

            AddTopSurface(topMesh.Positions, topMesh.Triangles, min, size, vertices, normals, uv, triangles);
            AddBottomSurface(topMesh.Positions, topMesh.Triangles, depth, min, size, vertices, normals, uv, triangles);
            AddSideWalls(sideWallLoop, depth, vertices, normals, uv, triangles);

            return new IslandMeshData(vertices.ToArray(), normals.ToArray(), uv.ToArray(), triangles.ToArray());
        }

        private static List<Vector2> CollectInteriorSeedPoints(IReadOnlyList<Vector2> polygon, float spacing)
        {
            GetPolygonBounds(polygon, out var min, out var max);

            var columnStep = Mathf.Max(spacing, 0.01f);
            var rowStep = columnStep * HexRowFactor;
            var boundaryClearance = Mathf.Max(GetCollinearTolerance(columnStep) * 4f, columnStep * InteriorSeedClearanceFactor);
            var estimatedColumns = Mathf.Max(1, Mathf.CeilToInt((max.x - min.x) / columnStep));
            var estimatedRows = Mathf.Max(1, Mathf.CeilToInt((max.y - min.y) / rowStep));
            var polygonPath = CreatePath(polygon);
            var seeds = new List<Vector2>(estimatedColumns * estimatedRows);

            var rowIndex = 0;
            for (var y = min.y + (rowStep * 0.5f); y < max.y; y += rowStep, rowIndex++)
            {
                var rowOffset = (rowIndex & 1) == 0 ? 0f : columnStep * 0.5f;
                for (var x = min.x + (columnStep * 0.5f) + rowOffset; x < max.x; x += columnStep)
                {
                    var point = new Vector2(x, y);
                    if (!IsStrictlyInsidePolygon(polygonPath, point) || DistanceToPolygonBoundary(point, polygon) <= boundaryClearance)
                    {
                        continue;
                    }

                    seeds.Add(point);
                }
            }

            if (seeds.Count == 0 && TryGetPolygonCentroid(polygon, out var centroid))
            {
                if (IsStrictlyInsidePolygon(polygonPath, centroid) && DistanceToPolygonBoundary(centroid, polygon) > boundaryClearance * 0.5f)
                {
                    seeds.Add(centroid);
                }
            }

            return seeds;
        }

        private static PathD CreatePath(IReadOnlyList<Vector2> polygon)
        {
            var path = new PathD(polygon.Count);
            for (var i = 0; i < polygon.Count; i++)
            {
                path.Add(new PointD(polygon[i].x, polygon[i].y));
            }

            return path;
        }

        private static bool IsStrictlyInsidePolygon(PathD polygon, Vector2 point)
        {
            return Clipper.PointInPolygon(new PointD(point.x, point.y), polygon, TopologyPrecision) == PointInPolygonResult.IsInside;
        }

        private static bool TryGetPolygonCentroid(IReadOnlyList<Vector2> polygon, out Vector2 centroid)
        {
            var signedArea = SignedArea(polygon);
            if (Mathf.Abs(signedArea) <= MinimumDimension)
            {
                centroid = default;
                return false;
            }

            var centroidScale = 1f / (6f * signedArea);
            var centroidX = 0f;
            var centroidY = 0f;

            for (var i = 0; i < polygon.Count; i++)
            {
                var current = polygon[i];
                var next = polygon[(i + 1) % polygon.Count];
                var cross = (current.x * next.y) - (next.x * current.y);
                centroidX += (current.x + next.x) * cross;
                centroidY += (current.y + next.y) * cross;
            }

            centroid = new Vector2(centroidX * centroidScale, centroidY * centroidScale);
            return true;
        }

        private static void AddTopSurface(
            IReadOnlyList<Vector2> positions,
            IReadOnlyList<int> topTriangles,
            Vector2 min,
            Vector2 size,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uv,
            List<int> triangles)
        {
            var startIndex = vertices.Count;
            for (var pointIndex = 0; pointIndex < positions.Count; pointIndex++)
            {
                vertices.Add(new Vector3(positions[pointIndex].x, 0f, positions[pointIndex].y));
                normals.Add(Vector3.up);
                uv.Add(GetPlanarUv(positions[pointIndex], min, size));
            }

            for (var triangleIndex = 0; triangleIndex < topTriangles.Count; triangleIndex += 3)
            {
                AppendUpwardTriangle(
                    startIndex + topTriangles[triangleIndex],
                    startIndex + topTriangles[triangleIndex + 1],
                    startIndex + topTriangles[triangleIndex + 2],
                    vertices,
                    triangles);
            }
        }

        private static void AddBottomSurface(
            IReadOnlyList<Vector2> positions,
            IReadOnlyList<int> topTriangles,
            float depth,
            Vector2 min,
            Vector2 size,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uv,
            List<int> triangles)
        {
            var startIndex = vertices.Count;
            for (var pointIndex = 0; pointIndex < positions.Count; pointIndex++)
            {
                vertices.Add(new Vector3(positions[pointIndex].x, -depth, positions[pointIndex].y));
                normals.Add(Vector3.down);
                uv.Add(GetPlanarUv(positions[pointIndex], min, size));
            }

            for (var triangleIndex = 0; triangleIndex < topTriangles.Count; triangleIndex += 3)
            {
                AppendDownwardTriangle(
                    startIndex + topTriangles[triangleIndex],
                    startIndex + topTriangles[triangleIndex + 1],
                    startIndex + topTriangles[triangleIndex + 2],
                    vertices,
                    triangles);
            }
        }

        private static void AddSideWalls(
            IReadOnlyList<Vector2> polygon,
            float depth,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uv,
            List<int> triangles)
        {
            var perimeter = 0f;
            var edgeLengths = new float[polygon.Count];
            for (var edgeIndex = 0; edgeIndex < polygon.Count; edgeIndex++)
            {
                var nextIndex = (edgeIndex + 1) % polygon.Count;
                var edgeLength = Vector2.Distance(polygon[edgeIndex], polygon[nextIndex]);
                edgeLengths[edgeIndex] = edgeLength;
                perimeter += edgeLength;
            }

            var perimeterTravel = 0f;
            for (var edgeIndex = 0; edgeIndex < polygon.Count; edgeIndex++)
            {
                var nextIndex = (edgeIndex + 1) % polygon.Count;
                var startIndex = vertices.Count;

                var topA = new Vector3(polygon[edgeIndex].x, 0f, polygon[edgeIndex].y);
                var topB = new Vector3(polygon[nextIndex].x, 0f, polygon[nextIndex].y);
                var bottomA = new Vector3(polygon[edgeIndex].x, -depth, polygon[edgeIndex].y);
                var bottomB = new Vector3(polygon[nextIndex].x, -depth, polygon[nextIndex].y);

                vertices.Add(topA);
                vertices.Add(topB);
                vertices.Add(bottomA);
                vertices.Add(bottomB);

                var faceNormal = Vector3.Normalize(Vector3.Cross(topB - topA, bottomA - topA));
                if (faceNormal.sqrMagnitude <= Mathf.Epsilon)
                {
                    var edgeDirection = (polygon[nextIndex] - polygon[edgeIndex]).normalized;
                    faceNormal = new Vector3(edgeDirection.y, 0f, -edgeDirection.x);
                }

                normals.Add(faceNormal);
                normals.Add(faceNormal);
                normals.Add(faceNormal);
                normals.Add(faceNormal);

                var currentTravel = perimeter <= MinimumDimension ? 0f : perimeterTravel / perimeter;
                var nextTravel = perimeter <= MinimumDimension ? 1f : (perimeterTravel + edgeLengths[edgeIndex]) / perimeter;

                uv.Add(new Vector2(currentTravel, 1f));
                uv.Add(new Vector2(nextTravel, 1f));
                uv.Add(new Vector2(currentTravel, 0f));
                uv.Add(new Vector2(nextTravel, 0f));

                triangles.Add(startIndex);
                triangles.Add(startIndex + 1);
                triangles.Add(startIndex + 2);

                triangles.Add(startIndex + 1);
                triangles.Add(startIndex + 3);
                triangles.Add(startIndex + 2);

                perimeterTravel += edgeLengths[edgeIndex];
            }
        }

        private static void AppendUpwardTriangle(int a, int b, int c, List<Vector3> vertices, List<int> triangles)
        {
            var normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (normal.y >= 0f)
            {
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
                return;
            }

            triangles.Add(a);
            triangles.Add(c);
            triangles.Add(b);
        }

        private static void AppendDownwardTriangle(int a, int b, int c, List<Vector3> vertices, List<int> triangles)
        {
            var normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (normal.y <= 0f)
            {
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
                return;
            }

            triangles.Add(a);
            triangles.Add(c);
            triangles.Add(b);
        }

        private static List<Vector2> NormalizePolygon(List<Vector3> sampledPoints, float tolerance)
        {
            var polygon = new List<Vector2>(sampledPoints.Count);
            for (var i = 0; i < sampledPoints.Count; i++)
            {
                var point = new Vector2(sampledPoints[i].x, sampledPoints[i].z);
                if (polygon.Count > 0 && NearlyEqual(polygon[polygon.Count - 1], point, tolerance))
                {
                    continue;
                }

                polygon.Add(point);
            }

            if (polygon.Count > 1 && NearlyEqual(polygon[0], polygon[polygon.Count - 1], tolerance))
            {
                polygon.RemoveAt(polygon.Count - 1);
            }

            return polygon;
        }

        private static void RemoveCollinearVertices(List<Vector2> polygon, float tolerance)
        {
            var removed = true;
            while (removed && polygon.Count > 3)
            {
                removed = false;
                for (var i = 0; i < polygon.Count; i++)
                {
                    var previous = polygon[(i - 1 + polygon.Count) % polygon.Count];
                    var current = polygon[i];
                    var next = polygon[(i + 1) % polygon.Count];

                    if (DistancePointToSegment(current, previous, next) <= tolerance)
                    {
                        polygon.RemoveAt(i);
                        removed = true;
                        break;
                    }
                }
            }
        }

        private static bool TryResolvePolygonTopology(
            List<Vector2> polygon,
            float duplicatePointTolerance,
            float collinearTolerance,
            float validationTolerance,
            float minimumArea,
            out List<Vector2> resolvedPolygon)
        {
            var rawPath = new PathD(polygon.Count);
            for (var i = 0; i < polygon.Count; i++)
            {
                rawPath.Add(new PointD(polygon[i].x, polygon[i].y));
            }

            var rawArea = Mathf.Abs((float)Clipper.Area(rawPath));
            var topologyAreaTolerance = GetTopologyAreaTolerance(polygon, validationTolerance, minimumArea);
            var canonicalPaths = Clipper.BooleanOp(
                ClipType.Union,
                new PathsD { rawPath },
                null,
                FillRule.NonZero,
                TopologyPrecision);

            PathD dominantPath = null;
            var significantPathCount = 0;
            for (var i = 0; i < canonicalPaths.Count; i++)
            {
                var area = Mathf.Abs((float)Clipper.Area(canonicalPaths[i]));
                if (area < topologyAreaTolerance)
                {
                    continue;
                }

                significantPathCount++;
                if (dominantPath == null || area > Mathf.Abs((float)Clipper.Area(dominantPath)))
                {
                    dominantPath = canonicalPaths[i];
                }
            }

            if (significantPathCount != 1 || dominantPath == null)
            {
                resolvedPolygon = null;
                return false;
            }

            var trimmedPath = Clipper.TrimCollinear(dominantPath, TopologyPrecision);
            resolvedPolygon = ConvertPathToPolygon(trimmedPath);

            if (resolvedPolygon.Count < 3)
            {
                return false;
            }

            if (SignedArea(resolvedPolygon) < 0f)
            {
                resolvedPolygon.Reverse();
            }

            RemoveCollinearVertices(resolvedPolygon, collinearTolerance);

            if (resolvedPolygon.Count < 3 || HasCollapsedEdges(resolvedPolygon, duplicatePointTolerance))
            {
                return false;
            }

            var resolvedArea = Mathf.Abs(SignedArea(resolvedPolygon));
            if (resolvedArea < minimumArea)
            {
                return false;
            }

            return Mathf.Abs(resolvedArea - rawArea) <= topologyAreaTolerance;
        }

        private static List<Vector2> ConvertPathToPolygon(PathD path)
        {
            var polygon = new List<Vector2>(path.Count);
            for (var i = 0; i < path.Count; i++)
            {
                polygon.Add(new Vector2((float)path[i].x, (float)path[i].y));
            }

            return polygon;
        }

        private static float GetTopologyAreaTolerance(
            IReadOnlyList<Vector2> polygon,
            float validationTolerance,
            float minimumArea)
        {
            return Mathf.Max(minimumArea * 0.25f, GetPerimeter(polygon) * Mathf.Max(validationTolerance, MinimumDimension) * 0.25f);
        }

        private static float GetPerimeter(IReadOnlyList<Vector2> polygon)
        {
            var perimeter = 0f;
            for (var i = 0; i < polygon.Count; i++)
            {
                perimeter += Vector2.Distance(polygon[i], polygon[(i + 1) % polygon.Count]);
            }

            return perimeter;
        }

        private static void GetPolygonBounds(IReadOnlyList<Vector2> polygon, out Vector2 min, out Vector2 max)
        {
            min = polygon[0];
            max = polygon[0];
            for (var i = 1; i < polygon.Count; i++)
            {
                min = Vector2.Min(min, polygon[i]);
                max = Vector2.Max(max, polygon[i]);
            }
        }

        private static Vector2 GetPlanarUv(Vector2 point, Vector2 min, Vector2 size)
        {
            return new Vector2((point.x - min.x) / size.x, (point.y - min.y) / size.y);
        }

        private static float GetCollinearTolerance(float spacing)
        {
            return Mathf.Max(0.00001f, Mathf.Min(0.0005f, spacing * 0.0025f));
        }

        private static float GetValidationTolerance(float collinearTolerance, float spacing)
        {
            return Mathf.Max(0.0005f, Mathf.Min(0.0025f, Mathf.Max(collinearTolerance * 2f, spacing * 0.01f)));
        }

        private static bool HasCollapsedEdges(List<Vector2> polygon, float tolerance)
        {
            for (var i = 0; i < polygon.Count; i++)
            {
                if (NearlyEqual(polygon[i], polygon[(i + 1) % polygon.Count], tolerance))
                {
                    return true;
                }
            }

            return false;
        }

        private static float SignedArea(IReadOnlyList<Vector2> polygon)
        {
            var area = 0f;
            for (var i = 0; i < polygon.Count; i++)
            {
                var current = polygon[i];
                var next = polygon[(i + 1) % polygon.Count];
                area += (current.x * next.y) - (next.x * current.y);
            }

            return area * 0.5f;
        }

        private static float DistanceToPolygonBoundary(Vector2 point, IReadOnlyList<Vector2> polygon)
        {
            var minDistance = float.MaxValue;
            for (var i = 0; i < polygon.Count; i++)
            {
                minDistance = Mathf.Min(minDistance, DistancePointToSegment(point, polygon[i], polygon[(i + 1) % polygon.Count]));
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

        private static bool NearlyEqual(Vector2 a, Vector2 b, float tolerance)
        {
            return Vector2.SqrMagnitude(a - b) <= tolerance * tolerance;
        }

        private static int GetNextHalfedgeIndex(int halfedgeIndex)
        {
            return halfedgeIndex % 3 == 2 ? halfedgeIndex - 2 : halfedgeIndex + 1;
        }

        private static Vector2 ToVector2(double2 value)
        {
            return new Vector2((float)value.x, (float)value.y);
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }
    }
}
