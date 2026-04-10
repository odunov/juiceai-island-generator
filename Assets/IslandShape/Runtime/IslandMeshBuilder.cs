using System;
using System.Collections.Generic;
using Clipper2Lib;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace Islands.EditorTools
{
    public readonly struct IslandMeshBuildSettings
    {
        public IslandMeshBuildSettings(
            float depth,
            int terraceCount,
            float terraceStepWidth,
            float terraceWidthBias,
            float terraceDepthBias,
            float terraceSoftness,
            float spacing,
            float coastBandWidth,
            IReadOnlyList<IslandEdgeZone> edgeZones,
            float minimumArea,
            float duplicatePointTolerance)
        {
            Depth = depth;
            TerraceCount = terraceCount;
            TerraceStepWidth = terraceStepWidth;
            TerraceWidthBias = terraceWidthBias;
            TerraceDepthBias = terraceDepthBias;
            TerraceSoftness = terraceSoftness;
            Spacing = spacing;
            CoastBandWidth = coastBandWidth;
            EdgeZones = edgeZones ?? Array.Empty<IslandEdgeZone>();
            MinimumArea = minimumArea;
            DuplicatePointTolerance = duplicatePointTolerance;
        }

        public IslandMeshBuildSettings(
            float depth,
            int terraceCount,
            float terraceStepWidth,
            float terraceWidthBias,
            float terraceDepthBias,
            float terraceSoftness,
            float spacing,
            float minimumArea,
            float duplicatePointTolerance)
            : this(
                depth,
                terraceCount,
                terraceStepWidth,
                terraceWidthBias,
                terraceDepthBias,
                terraceSoftness,
                spacing,
                0.45f,
                Array.Empty<IslandEdgeZone>(),
                minimumArea,
                duplicatePointTolerance)
        {
        }

        public float Depth { get; }

        public int TerraceCount { get; }

        public float TerraceStepWidth { get; }

        public float TerraceWidthBias { get; }

        public float TerraceDepthBias { get; }

        public float TerraceSoftness { get; }

        public float Spacing { get; }

        public float CoastBandWidth { get; }

        public IReadOnlyList<IslandEdgeZone> EdgeZones { get; }

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

    public static class IslandMeshBuilder
    {
        private const int ClipperPrecision = 4;
        private const int MaxBaseSamplesPerCurve = 2048;
        private const float MinimumDimension = 0.0001f;
        private const float MinimumCoastBandWidth = 0.02f;

        private readonly struct EdgeZoneSample
        {
            public EdgeZoneSample(float normalizedT, float silhouetteOffset, float coastBandWidth, float terraceWidthScale, float terraceSoftnessScale)
            {
                NormalizedT = normalizedT;
                SilhouetteOffset = silhouetteOffset;
                CoastBandWidth = coastBandWidth;
                TerraceWidthScale = terraceWidthScale;
                TerraceSoftnessScale = terraceSoftnessScale;
            }

            public float NormalizedT { get; }

            public float SilhouetteOffset { get; }

            public float CoastBandWidth { get; }

            public float TerraceWidthScale { get; }

            public float TerraceSoftnessScale { get; }
        }

        private sealed class TopSurfaceData
        {
            public List<Vector2> OuterRing;
            public List<Vector2> InnerRing;
            public List<int> CoreTriangles;
            public bool HasCoastBand;
        }

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
            var coastBandWidth = Mathf.Max(settings.CoastBandWidth, MinimumCoastBandWidth);
            var minimumArea = Mathf.Max(settings.MinimumArea, 0.0001f);
            var duplicatePointTolerance = Mathf.Max(settings.DuplicatePointTolerance, 0.0001f);
            var collinearTolerance = GetCollinearTolerance(spacing);
            var validationTolerance = GetValidationTolerance(collinearTolerance, spacing);
            var depth = Mathf.Max(settings.Depth, 0.01f);
            var terraceCount = Mathf.Max(settings.TerraceCount, 1);
            var terraceStepWidth = Mathf.Max(settings.TerraceStepWidth, 0.01f);
            var terraceWidthBias = Mathf.Clamp(settings.TerraceWidthBias, -1f, 1f);
            var terraceDepthBias = Mathf.Clamp(settings.TerraceDepthBias, -1f, 1f);
            var terraceSoftness = Mathf.Clamp01(settings.TerraceSoftness);

            if (!TryBuildValidatedPolygonFromSpline(
                    spline,
                    spacing,
                    duplicatePointTolerance,
                    collinearTolerance,
                    validationTolerance,
                    minimumArea,
                    out var basePolygon,
                    out var validationMessage))
            {
                return new IslandMeshBuildResult(validationMessage);
            }

            if (!TryBuildDetailPolygon(
                    basePolygon,
                    settings.EdgeZones,
                    duplicatePointTolerance,
                    collinearTolerance,
                    validationTolerance,
                    minimumArea,
                    out var detailPolygon,
                    out validationMessage))
            {
                return new IslandMeshBuildResult(validationMessage);
            }

            var outerRing = ResamplePolygon(detailPolygon, Mathf.Max(3, detailPolygon.Count));
            var edgeSamples = SampleEdgeZones(outerRing, coastBandWidth, settings.EdgeZones);

            if (!TryBuildTopSurface(
                    outerRing,
                    edgeSamples,
                    duplicatePointTolerance,
                    collinearTolerance,
                    validationTolerance,
                    minimumArea,
                    out var topSurface,
                    out validationMessage))
            {
                return new IslandMeshBuildResult(validationMessage);
            }

            var terraceBoundaries = BuildTerraceBoundaries(
                outerRing,
                edgeSamples,
                terraceCount,
                terraceStepWidth,
                terraceWidthBias,
                terraceSoftness,
                duplicatePointTolerance,
                collinearTolerance,
                validationTolerance,
                minimumArea);

            return new IslandMeshBuildResult(BuildMesh(topSurface, terraceBoundaries, depth, terraceDepthBias));
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

            if (!TryBuildValidatedPolygonFromSpline(
                    spline,
                    spacing,
                    duplicatePointTolerance,
                    collinearTolerance,
                    validationTolerance,
                    minimumArea,
                    out var basePolygon,
                    out validationMessage))
            {
                polygon = null;
                return false;
            }

            if (!TryBuildDetailPolygon(
                    basePolygon,
                    settings.EdgeZones,
                    duplicatePointTolerance,
                    collinearTolerance,
                    validationTolerance,
                    minimumArea,
                    out var detailPolygon,
                    out validationMessage))
            {
                polygon = null;
                return false;
            }

            polygon = ResamplePolygon(detailPolygon, Mathf.Max(3, detailPolygon.Count));
            validationMessage = string.Empty;
            return true;
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

            var distanceUntilNextSample = spacing;
            for (var curveIndex = 0; curveIndex < curveCount; curveIndex++)
            {
                var curve = spline.GetCurve(curveIndex);
                var curveLength = spline.GetCurveLength(curveIndex);
                var previousCurveT = 0f;
                var nextCurveDistance = distanceUntilNextSample;

                if (sampledPoints.Count == 0)
                {
                    sampledPoints.Add(ToVector3(curve.P0));
                }

                var sampleCount = 0;
                while (nextCurveDistance < curveLength - MinimumDimension && sampleCount < MaxBaseSamplesPerCurve)
                {
                    var curveT = spline.GetCurveInterpolation(curveIndex, nextCurveDistance);
                    sampledPoints.Add(ToVector3(CurveUtility.EvaluatePosition(curve, curveT)));
                    previousCurveT = curveT;
                    nextCurveDistance += spacing;
                    sampleCount++;
                }

                if (previousCurveT < 1f - MinimumDimension)
                {
                    sampledPoints.Add(ToVector3(curve.P3));
                }

                var remainingDistance = nextCurveDistance - curveLength;
                distanceUntilNextSample = remainingDistance <= MinimumDimension ? spacing : remainingDistance;
            }

            return sampledPoints;
        }

        private static bool TryBuildDetailPolygon(
            List<Vector2> basePolygon,
            IReadOnlyList<IslandEdgeZone> edgeZones,
            float duplicatePointTolerance,
            float collinearTolerance,
            float validationTolerance,
            float minimumArea,
            out List<Vector2> detailPolygon,
            out string validationMessage)
        {
            var candidatePolygon = BuildDetailSilhouettePolyline(basePolygon, edgeZones);
            if (TryValidateAndNormalizePolygon(
                    ToSampledPoints(candidatePolygon),
                    duplicatePointTolerance,
                    collinearTolerance,
                    validationTolerance,
                    minimumArea,
                    out detailPolygon,
                    out validationMessage))
            {
                return true;
            }

            if (!TryResolveCandidatePolygon(
                    candidatePolygon,
                    duplicatePointTolerance,
                    collinearTolerance,
                    validationTolerance,
                    minimumArea,
                    basePolygon.Count,
                    out detailPolygon))
            {
                return false;
            }

            validationMessage = string.Empty;
            return true;
        }

        private static List<Vector2> BuildDetailSilhouettePolyline(List<Vector2> basePolygon, IReadOnlyList<IslandEdgeZone> edgeZones)
        {
            if (basePolygon.Count == 0)
            {
                return new List<Vector2>();
            }

            var detailPolygon = new List<Vector2>(basePolygon.Count);
            var perimeterPositions = GetNormalizedPerimeterPositions(basePolygon);
            for (var pointIndex = 0; pointIndex < basePolygon.Count; pointIndex++)
            {
                var outwardNormal = GetVertexOutwardNormal(basePolygon, pointIndex);
                var edgeZoneSample = EvaluateEdgeZone(edgeZones, perimeterPositions[pointIndex], MinimumCoastBandWidth);
                detailPolygon.Add(basePolygon[pointIndex] + (outwardNormal * edgeZoneSample.SilhouetteOffset));
            }

            return detailPolygon;
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
            if (polygon.Count < 3)
            {
                validationMessage = "The island outline became degenerate after simplification.";
                return false;
            }

            if (HasSelfIntersections(polygon, validationTolerance))
            {
                validationMessage = "The island outline intersects itself.";
                return false;
            }

            return true;
        }

        private static bool TryTriangulateValidatedPolygon(
            List<Vector2> polygon,
            float validationTolerance,
            out List<int> topTriangles,
            out string validationMessage)
        {
            if (!TryTriangulate(polygon, validationTolerance, out topTriangles))
            {
                validationMessage = "The island outline could not be triangulated.";
                return false;
            }

            validationMessage = string.Empty;
            return true;
        }

        private static bool TryBuildTopSurface(
            List<Vector2> outerRing,
            EdgeZoneSample[] edgeSamples,
            float duplicatePointTolerance,
            float collinearTolerance,
            float validationTolerance,
            float minimumArea,
            out TopSurfaceData topSurface,
            out string validationMessage)
        {
            topSurface = new TopSurfaceData
            {
                OuterRing = outerRing,
                InnerRing = null,
                CoreTriangles = null,
                HasCoastBand = false
            };

            var coastBandDistances = new float[edgeSamples.Length];
            var coastBandSoftness = new float[edgeSamples.Length];
            for (var sampleIndex = 0; sampleIndex < edgeSamples.Length; sampleIndex++)
            {
                coastBandDistances[sampleIndex] = Mathf.Max(MinimumCoastBandWidth, edgeSamples[sampleIndex].CoastBandWidth);
                coastBandSoftness[sampleIndex] = Mathf.Clamp01(edgeSamples[sampleIndex].TerraceSoftnessScale * 0.35f);
            }

            if (!TryTransformPolygonVariable(
                    outerRing,
                    coastBandDistances,
                    coastBandSoftness,
                    false,
                    duplicatePointTolerance,
                    collinearTolerance,
                    validationTolerance,
                    minimumArea,
                    outerRing.Count,
                    out var innerRing))
            {
                if (!TryTriangulateValidatedPolygon(outerRing, validationTolerance, out var fallbackTriangles, out validationMessage))
                {
                    return false;
                }

                topSurface.InnerRing = outerRing;
                topSurface.CoreTriangles = fallbackTriangles;
                return true;
            }

            innerRing = ResamplePolygon(innerRing, outerRing.Count);
            if (!TryTriangulateValidatedPolygon(innerRing, validationTolerance, out var coreTriangles, out validationMessage))
            {
                if (!TryTriangulateValidatedPolygon(outerRing, validationTolerance, out var fallbackTriangles, out validationMessage))
                {
                    return false;
                }

                topSurface.InnerRing = outerRing;
                topSurface.CoreTriangles = fallbackTriangles;
                return true;
            }

            topSurface.InnerRing = innerRing;
            topSurface.CoreTriangles = coreTriangles;
            topSurface.HasCoastBand = true;
            validationMessage = string.Empty;
            return true;
        }

        private static EdgeZoneSample[] SampleEdgeZones(List<Vector2> polygon, float coastBandWidth, IReadOnlyList<IslandEdgeZone> edgeZones)
        {
            var perimeterPositions = GetNormalizedPerimeterPositions(polygon);
            var samples = new EdgeZoneSample[polygon.Count];
            for (var pointIndex = 0; pointIndex < polygon.Count; pointIndex++)
            {
                samples[pointIndex] = EvaluateEdgeZone(edgeZones, perimeterPositions[pointIndex], coastBandWidth);
            }

            return samples;
        }

        private static EdgeZoneSample EvaluateEdgeZone(IReadOnlyList<IslandEdgeZone> edgeZones, float normalizedT, float defaultCoastBandWidth)
        {
            var bestInfluence = 0f;
            var silhouetteOffset = 0f;
            var coastBandWidth = defaultCoastBandWidth;
            var terraceWidthScale = 1f;
            var terraceSoftnessScale = 1f;

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
                    coastBandWidth = defaultCoastBandWidth + (edgeZones[zoneIndex].CoastBandWidthDelta * influence);
                    terraceWidthScale = Mathf.Lerp(1f, edgeZones[zoneIndex].TerraceWidthScale, influence);
                    terraceSoftnessScale = Mathf.Lerp(1f, edgeZones[zoneIndex].TerraceSoftnessScale, influence);
                }
            }

            return new EdgeZoneSample(
                normalizedT,
                silhouetteOffset,
                Mathf.Max(MinimumCoastBandWidth, coastBandWidth),
                Mathf.Max(0.1f, terraceWidthScale),
                Mathf.Max(0.1f, terraceSoftnessScale));
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

        private static float[] GetNormalizedPerimeterPositions(List<Vector2> polygon)
        {
            var normalizedPositions = new float[polygon.Count];
            var perimeter = GetPolygonPerimeter(polygon);
            if (perimeter <= MinimumDimension)
            {
                return normalizedPositions;
            }

            var travel = 0f;
            for (var pointIndex = 0; pointIndex < polygon.Count; pointIndex++)
            {
                normalizedPositions[pointIndex] = travel / perimeter;
                var nextIndex = (pointIndex + 1) % polygon.Count;
                travel += Vector2.Distance(polygon[pointIndex], polygon[nextIndex]);
            }

            return normalizedPositions;
        }

        private static float GetPolygonPerimeter(List<Vector2> polygon)
        {
            var perimeter = 0f;
            for (var pointIndex = 0; pointIndex < polygon.Count; pointIndex++)
            {
                perimeter += Vector2.Distance(polygon[pointIndex], polygon[(pointIndex + 1) % polygon.Count]);
            }

            return perimeter;
        }

        private static Vector2 GetVertexOutwardNormal(List<Vector2> polygon, int pointIndex)
        {
            var prev = polygon[(pointIndex - 1 + polygon.Count) % polygon.Count];
            var current = polygon[pointIndex];
            var next = polygon[(pointIndex + 1) % polygon.Count];

            var prevDirection = (current - prev).normalized;
            var nextDirection = (next - current).normalized;
            var prevNormal = new Vector2(prevDirection.y, -prevDirection.x);
            var nextNormal = new Vector2(nextDirection.y, -nextDirection.x);
            var outwardNormal = (prevNormal + nextNormal).normalized;

            if (outwardNormal.sqrMagnitude <= Mathf.Epsilon)
            {
                outwardNormal = prevNormal.sqrMagnitude > Mathf.Epsilon ? prevNormal : nextNormal;
            }

            return outwardNormal.normalized;
        }

        private static bool TryTransformPolygonVariable(
            List<Vector2> polygon,
            float[] distances,
            float[] softnessValues,
            bool outward,
            float duplicatePointTolerance,
            float collinearTolerance,
            float validationTolerance,
            float minimumArea,
            int targetCount,
            out List<Vector2> transformedPolygon)
        {
            transformedPolygon = null;
            if (polygon.Count < 3 || distances == null || softnessValues == null || distances.Length != softnessValues.Length)
            {
                return false;
            }

            var workingPolygon = polygon.Count == targetCount ? polygon : ResamplePolygon(polygon, targetCount);
            var candidate = new List<Vector2>(workingPolygon.Count);
            for (var pointIndex = 0; pointIndex < workingPolygon.Count; pointIndex++)
            {
                var outwardNormal = GetVertexOutwardNormal(workingPolygon, pointIndex);
                var offsetDistance = Mathf.Max(0f, distances[pointIndex]);
                if (!outward)
                {
                    offsetDistance = -offsetDistance;
                }

                candidate.Add(workingPolygon[pointIndex] + (outwardNormal * offsetDistance));
            }

            candidate = ApplyLocalSoftness(candidate, softnessValues);
            if (TryValidateAndNormalizePolygon(
                    ToSampledPoints(candidate),
                    duplicatePointTolerance,
                    collinearTolerance,
                    validationTolerance,
                    minimumArea,
                    out transformedPolygon,
                    out _))
            {
                transformedPolygon = ResamplePolygon(transformedPolygon, targetCount);
                return true;
            }

            return TryResolveCandidatePolygon(
                candidate,
                duplicatePointTolerance,
                collinearTolerance,
                validationTolerance,
                minimumArea,
                targetCount,
                out transformedPolygon);
        }

        private static List<Vector2> ApplyLocalSoftness(List<Vector2> polygon, float[] softnessValues)
        {
            var softened = new List<Vector2>(polygon.Count);
            for (var pointIndex = 0; pointIndex < polygon.Count; pointIndex++)
            {
                var previous = polygon[(pointIndex - 1 + polygon.Count) % polygon.Count];
                var current = polygon[pointIndex];
                var next = polygon[(pointIndex + 1) % polygon.Count];
                var smoothingTarget = (previous + next) * 0.5f;
                var smoothing = Mathf.Clamp01(softnessValues[pointIndex] * 0.35f);
                softened.Add(Vector2.Lerp(current, smoothingTarget, smoothing));
            }

            return softened;
        }

        private static bool TryResolveCandidatePolygon(
            List<Vector2> candidatePolygon,
            float duplicatePointTolerance,
            float collinearTolerance,
            float validationTolerance,
            float minimumArea,
            int targetCount,
            out List<Vector2> polygon)
        {
            polygon = null;
            var candidatePath = ToClipperPath(candidatePolygon);
            var cleanedPaths = Clipper.BooleanOp(
                ClipType.Union,
                new PathsD { candidatePath },
                null,
                FillRule.NonZero,
                ClipperPrecision);

            if (!TryExtractLargestBoundary(cleanedPaths, validationTolerance, minimumArea, out polygon))
            {
                return false;
            }

            if (!TryValidateAndNormalizePolygon(
                    ToSampledPoints(polygon),
                    duplicatePointTolerance,
                    collinearTolerance,
                    validationTolerance,
                    minimumArea,
                    out polygon,
                    out _))
            {
                return false;
            }

            polygon = ResamplePolygon(polygon, Mathf.Max(3, targetCount));
            return true;
        }

        private static List<Vector2> ResamplePolygon(List<Vector2> polygon, int targetCount)
        {
            if (polygon.Count < 3 || targetCount <= 0)
            {
                return new List<Vector2>(polygon);
            }

            var perimeter = GetPolygonPerimeter(polygon);
            if (perimeter <= MinimumDimension)
            {
                return new List<Vector2>(polygon);
            }

            var resampled = new List<Vector2>(targetCount);
            var stepLength = perimeter / targetCount;
            for (var sampleIndex = 0; sampleIndex < targetCount; sampleIndex++)
            {
                resampled.Add(GetPointOnPolygonPerimeter(polygon, stepLength * sampleIndex));
            }

            return resampled;
        }

        private static Vector2 GetPointOnPolygonPerimeter(List<Vector2> polygon, float distance)
        {
            var perimeter = GetPolygonPerimeter(polygon);
            if (perimeter <= MinimumDimension)
            {
                return polygon[0];
            }

            distance = Mathf.Repeat(distance, perimeter);
            var travel = 0f;
            for (var pointIndex = 0; pointIndex < polygon.Count; pointIndex++)
            {
                var nextIndex = (pointIndex + 1) % polygon.Count;
                var edgeLength = Vector2.Distance(polygon[pointIndex], polygon[nextIndex]);
                if (travel + edgeLength >= distance)
                {
                    var edgeT = edgeLength <= MinimumDimension ? 0f : (distance - travel) / edgeLength;
                    return Vector2.Lerp(polygon[pointIndex], polygon[nextIndex], edgeT);
                }

                travel += edgeLength;
            }

            return polygon[0];
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
                    var prev = polygon[(i - 1 + polygon.Count) % polygon.Count];
                    var current = polygon[i];
                    var next = polygon[(i + 1) % polygon.Count];

                    if (DistancePointToSegment(current, prev, next) <= tolerance)
                    {
                        polygon.RemoveAt(i);
                        removed = true;
                        break;
                    }
                }
            }
        }

        private static bool HasSelfIntersections(List<Vector2> polygon, float tolerance)
        {
            for (var i = 0; i < polygon.Count; i++)
            {
                var a1 = polygon[i];
                var a2 = polygon[(i + 1) % polygon.Count];

                for (var j = i + 1; j < polygon.Count; j++)
                {
                    if (Mathf.Abs(i - j) <= 1)
                    {
                        continue;
                    }

                    if (i == 0 && j == polygon.Count - 1)
                    {
                        continue;
                    }

                    var b1 = polygon[j];
                    var b2 = polygon[(j + 1) % polygon.Count];

                    if (SegmentsIntersect(a1, a2, b1, b2, tolerance))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryTriangulate(List<Vector2> polygon, float tolerance, out List<int> triangles)
        {
            triangles = new List<int>((polygon.Count - 2) * 3);
            var indices = new List<int>(polygon.Count);
            for (var i = 0; i < polygon.Count; i++)
            {
                indices.Add(i);
            }

            var guard = 0;
            var maxIterations = polygon.Count * polygon.Count;
            while (indices.Count > 3 && guard++ < maxIterations)
            {
                var clippedEar = false;
                for (var i = 0; i < indices.Count; i++)
                {
                    var prevIndex = indices[(i - 1 + indices.Count) % indices.Count];
                    var currentIndex = indices[i];
                    var nextIndex = indices[(i + 1) % indices.Count];

                    var prev = polygon[prevIndex];
                    var current = polygon[currentIndex];
                    var next = polygon[nextIndex];

                    if (Cross(prev, current, next) <= tolerance)
                    {
                        continue;
                    }

                    var containsOtherPoint = false;
                    for (var pointIndex = 0; pointIndex < indices.Count; pointIndex++)
                    {
                        var candidate = indices[pointIndex];
                        if (candidate == prevIndex || candidate == currentIndex || candidate == nextIndex)
                        {
                            continue;
                        }

                        if (IsPointInTriangle(polygon[candidate], prev, current, next, tolerance))
                        {
                            containsOtherPoint = true;
                            break;
                        }
                    }

                    if (containsOtherPoint)
                    {
                        continue;
                    }

                    triangles.Add(prevIndex);
                    triangles.Add(currentIndex);
                    triangles.Add(nextIndex);
                    indices.RemoveAt(i);
                    clippedEar = true;
                    break;
                }

                if (!clippedEar)
                {
                    return false;
                }
            }

            if (indices.Count != 3)
            {
                return false;
            }

            triangles.Add(indices[0]);
            triangles.Add(indices[1]);
            triangles.Add(indices[2]);
            return true;
        }

        private static List<List<Vector2>> BuildTerraceBoundaries(
            List<Vector2> polygon,
            EdgeZoneSample[] edgeSamples,
            int terraceCount,
            float terraceStepWidth,
            float terraceWidthBias,
            float terraceSoftness,
            float duplicatePointTolerance,
            float collinearTolerance,
            float tolerance,
            float minimumArea)
        {
            var terraceBoundaries = new List<List<Vector2>>(terraceCount)
            {
                new List<Vector2>(polygon)
            };

            if (terraceCount <= 1)
            {
                return terraceBoundaries;
            }

            var stepWeights = BuildNormalizedWeights(terraceCount - 1, terraceWidthBias);
            var currentBoundary = terraceBoundaries[0];
            var useVariableProfiles = HasVariableTerraceProfiles(edgeSamples);
            for (var terraceIndex = 0; terraceIndex < stepWeights.Length; terraceIndex++)
            {
                var stepWidth = terraceStepWidth * stepWeights[terraceIndex] * stepWeights.Length;
                if (useVariableProfiles)
                {
                    var distances = new float[edgeSamples.Length];
                    var softnessValues = new float[edgeSamples.Length];
                    for (var sampleIndex = 0; sampleIndex < edgeSamples.Length; sampleIndex++)
                    {
                        distances[sampleIndex] = stepWidth * edgeSamples[sampleIndex].TerraceWidthScale;
                        softnessValues[sampleIndex] = Mathf.Clamp01(terraceSoftness * edgeSamples[sampleIndex].TerraceSoftnessScale);
                    }

                    if (!TryTransformPolygonVariable(
                            currentBoundary,
                            distances,
                            softnessValues,
                            true,
                            duplicatePointTolerance,
                            collinearTolerance,
                            tolerance,
                            minimumArea,
                            polygon.Count,
                            out var expandedBoundary))
                    {
                        break;
                    }

                    terraceBoundaries.Add(expandedBoundary);
                    currentBoundary = expandedBoundary;
                    continue;
                }

                if (!TryExpandPolygon(currentBoundary, stepWidth, terraceSoftness, tolerance, minimumArea, out var uniformBoundary))
                {
                    break;
                }

                var expandedUniformBoundary = ResamplePolygon(uniformBoundary, polygon.Count);
                terraceBoundaries.Add(expandedUniformBoundary);
                currentBoundary = expandedUniformBoundary;
            }

            return terraceBoundaries;
        }

        private static bool HasVariableTerraceProfiles(EdgeZoneSample[] edgeSamples)
        {
            if (edgeSamples == null)
            {
                return false;
            }

            for (var sampleIndex = 0; sampleIndex < edgeSamples.Length; sampleIndex++)
            {
                if (Mathf.Abs(edgeSamples[sampleIndex].TerraceWidthScale - 1f) > 0.001f ||
                    Mathf.Abs(edgeSamples[sampleIndex].TerraceSoftnessScale - 1f) > 0.001f)
                {
                    return true;
                }
            }

            return false;
        }

        private static IslandMeshData BuildMesh(
            TopSurfaceData topSurface,
            List<List<Vector2>> terraceBoundaries,
            float depth,
            float terraceDepthBias)
        {
            var topPolygon = topSurface.OuterRing;
            GetPolygonBounds(topPolygon, out var min, out var max);
            var size = max - min;
            size.x = Mathf.Max(size.x, MinimumDimension);
            size.y = Mathf.Max(size.y, MinimumDimension);

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();

            if (topSurface.HasCoastBand && topSurface.InnerRing != null && topSurface.InnerRing.Count == topSurface.OuterRing.Count)
            {
                AddTopBand(topSurface.OuterRing, topSurface.InnerRing, min, size, vertices, normals, uv, triangles);
            }

            AddTopCore(topSurface.InnerRing ?? topSurface.OuterRing, topSurface.CoreTriangles, min, size, vertices, normals, uv, triangles);

            var boundaryHeights = BuildBoundaryHeights(depth, terraceBoundaries.Count, terraceDepthBias);
            for (var bandIndex = 0; bandIndex < terraceBoundaries.Count; bandIndex++)
            {
                var upperBoundary = terraceBoundaries[bandIndex];
                var upperHeight = boundaryHeights[bandIndex];
                var lowerHeight = boundaryHeights[bandIndex + 1];

                AddVerticalWallBand(upperBoundary, upperHeight, lowerHeight, vertices, normals, uv, triangles);

                if (bandIndex >= terraceBoundaries.Count - 1)
                {
                    continue;
                }

                AddHorizontalLedgeBand(
                    terraceBoundaries[bandIndex + 1],
                    upperBoundary,
                    lowerHeight,
                    min,
                    size,
                    vertices,
                    normals,
                    uv,
                    triangles);
            }

            return new IslandMeshData(vertices.ToArray(), normals.ToArray(), uv.ToArray(), triangles.ToArray());
        }

        private static void AddTopBand(
            List<Vector2> outerRing,
            List<Vector2> innerRing,
            Vector2 min,
            Vector2 size,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uv,
            List<int> triangles)
        {
            for (var pointIndex = 0; pointIndex < outerRing.Count; pointIndex++)
            {
                var nextIndex = (pointIndex + 1) % outerRing.Count;
                var startIndex = vertices.Count;

                var outerA = new Vector3(outerRing[pointIndex].x, 0f, outerRing[pointIndex].y);
                var outerB = new Vector3(outerRing[nextIndex].x, 0f, outerRing[nextIndex].y);
                var innerA = new Vector3(innerRing[pointIndex].x, 0f, innerRing[pointIndex].y);
                var innerB = new Vector3(innerRing[nextIndex].x, 0f, innerRing[nextIndex].y);

                vertices.Add(outerA);
                vertices.Add(outerB);
                vertices.Add(innerA);
                vertices.Add(innerB);

                normals.Add(Vector3.up);
                normals.Add(Vector3.up);
                normals.Add(Vector3.up);
                normals.Add(Vector3.up);

                uv.Add(GetPlanarUv(outerRing[pointIndex], min, size));
                uv.Add(GetPlanarUv(outerRing[nextIndex], min, size));
                uv.Add(GetPlanarUv(innerRing[pointIndex], min, size));
                uv.Add(GetPlanarUv(innerRing[nextIndex], min, size));

                var useOuterToInnerDiagonal = Vector2.SqrMagnitude(outerRing[pointIndex] - innerRing[nextIndex]) <=
                                             Vector2.SqrMagnitude(outerRing[nextIndex] - innerRing[pointIndex]);

                if (useOuterToInnerDiagonal)
                {
                    AppendUpwardTriangle(startIndex, startIndex + 1, startIndex + 3, vertices, triangles);
                    AppendUpwardTriangle(startIndex, startIndex + 3, startIndex + 2, vertices, triangles);
                    continue;
                }

                AppendUpwardTriangle(startIndex, startIndex + 1, startIndex + 2, vertices, triangles);
                AppendUpwardTriangle(startIndex + 1, startIndex + 3, startIndex + 2, vertices, triangles);
            }
        }

        private static void AddTopCore(
            List<Vector2> polygon,
            List<int> topTriangles,
            Vector2 min,
            Vector2 size,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uv,
            List<int> triangles)
        {
            if (polygon == null || topTriangles == null)
            {
                return;
            }

            var startIndex = vertices.Count;
            for (var pointIndex = 0; pointIndex < polygon.Count; pointIndex++)
            {
                vertices.Add(new Vector3(polygon[pointIndex].x, 0f, polygon[pointIndex].y));
                normals.Add(Vector3.up);
                uv.Add(GetPlanarUv(polygon[pointIndex], min, size));
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

        private static bool TryExpandPolygon(
            List<Vector2> polygon,
            float expansionDistance,
            float cornerSoftness,
            float tolerance,
            float minimumArea,
            out List<Vector2> expandedPolygon)
        {
            expandedPolygon = null;
            if (polygon.Count < 3 || expansionDistance <= tolerance)
            {
                return false;
            }

            var offsetPaths = Clipper.InflatePaths(
                new PathsD { ToClipperPath(polygon) },
                expansionDistance,
                JoinType.Round,
                EndType.Polygon,
                2.0,
                ClipperPrecision,
                GetOffsetArcTolerance(expansionDistance, cornerSoftness, tolerance));

            if (offsetPaths.Count == 0)
            {
                return false;
            }

            var simplifiedPaths = Clipper.SimplifyPaths(
                offsetPaths,
                GetSimplifyTolerance(expansionDistance, cornerSoftness, tolerance),
                true);

            var mergedPaths = Clipper.BooleanOp(
                ClipType.Union,
                simplifiedPaths.Count > 0 ? simplifiedPaths : offsetPaths,
                null,
                FillRule.NonZero,
                ClipperPrecision);

            return TryExtractLargestBoundary(mergedPaths, tolerance, minimumArea, out expandedPolygon);
        }

        private static bool IsTerraceBoundaryValid(List<Vector2> polygon, float tolerance, float minimumArea)
        {
            if (polygon.Count < 3 || HasCollapsedEdges(polygon, tolerance))
            {
                return false;
            }

            var signedArea = SignedArea(polygon);
            if (signedArea < minimumArea)
            {
                return false;
            }

            return !HasSelfIntersections(polygon, tolerance);
        }

        private static float[] BuildNormalizedWeights(int count, float bias)
        {
            var weights = new float[count];
            if (count <= 0)
            {
                return weights;
            }

            var sum = 0f;
            for (var i = 0; i < count; i++)
            {
                var t = count == 1 ? 0.5f : i / (float)(count - 1);
                var weight = 1f + bias * ((t * 2f) - 1f);
                weight = Mathf.Max(0.1f, weight);
                weights[i] = weight;
                sum += weight;
            }

            for (var i = 0; i < count; i++)
            {
                weights[i] /= sum;
            }

            return weights;
        }

        private static float[] BuildBoundaryHeights(float depth, int bandCount, float depthBias)
        {
            var heights = new float[bandCount + 1];
            heights[0] = 0f;

            var bandWeights = BuildNormalizedWeights(bandCount, depthBias);
            var currentHeight = 0f;
            for (var bandIndex = 0; bandIndex < bandCount; bandIndex++)
            {
                currentHeight -= depth * bandWeights[bandIndex];
                heights[bandIndex + 1] = currentHeight;
            }

            heights[bandCount] = -depth;
            return heights;
        }

        private static void AddHorizontalLedgeBand(
            List<Vector2> outerBoundary,
            List<Vector2> innerBoundary,
            float height,
            Vector2 min,
            Vector2 size,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uv,
            List<int> triangles)
        {
            var ledgePaths = Clipper.Difference(
                new PathsD { ToClipperPath(outerBoundary) },
                new PathsD { ToClipperPath(innerBoundary) },
                FillRule.NonZero,
                ClipperPrecision);

            if (ledgePaths.Count == 0)
            {
                return;
            }

            if (Clipper.Triangulate(ledgePaths, ClipperPrecision, out var trianglePaths) != TriangulateResult.success)
            {
                return;
            }

            for (var triangleIndex = 0; triangleIndex < trianglePaths.Count; triangleIndex++)
            {
                AddHorizontalTriangle(trianglePaths[triangleIndex], height, min, size, vertices, normals, uv, triangles);
            }
        }

        private static void AddVerticalWallBand(
            List<Vector2> boundary,
            float upperHeight,
            float lowerHeight,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uv,
            List<int> triangles)
        {
            var perimeter = 0f;
            var edgeLengths = new float[boundary.Count];
            for (var i = 0; i < boundary.Count; i++)
            {
                var next = boundary[(i + 1) % boundary.Count];
                var edgeLength = Vector2.Distance(boundary[i], next);
                edgeLengths[i] = edgeLength;
                perimeter += edgeLength;
            }

            var perimeterTravel = 0f;
            for (var edgeIndex = 0; edgeIndex < boundary.Count; edgeIndex++)
            {
                var nextIndex = (edgeIndex + 1) % boundary.Count;
                var startIndex = vertices.Count;

                var upperA = new Vector3(boundary[edgeIndex].x, upperHeight, boundary[edgeIndex].y);
                var upperB = new Vector3(boundary[nextIndex].x, upperHeight, boundary[nextIndex].y);
                var lowerA = new Vector3(boundary[edgeIndex].x, lowerHeight, boundary[edgeIndex].y);
                var lowerB = new Vector3(boundary[nextIndex].x, lowerHeight, boundary[nextIndex].y);

                vertices.Add(upperA);
                vertices.Add(upperB);
                vertices.Add(lowerA);
                vertices.Add(lowerB);

                var faceNormal = Vector3.Normalize(Vector3.Cross(upperB - upperA, lowerA - upperA));
                if (faceNormal.sqrMagnitude <= Mathf.Epsilon)
                {
                    var edgeDirection = (boundary[nextIndex] - boundary[edgeIndex]).normalized;
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

        private static void GetPolygonBounds(List<Vector2> polygon, out Vector2 min, out Vector2 max)
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

        private static double GetOffsetArcTolerance(float expansionDistance, float cornerSoftness, float tolerance)
        {
            var softness = Mathf.Clamp01(cornerSoftness);
            var scale = Mathf.Lerp(0.3f, 0.06f, softness);
            return Math.Max(tolerance * 0.5f, expansionDistance * scale);
        }

        private static double GetSimplifyTolerance(float expansionDistance, float cornerSoftness, float tolerance)
        {
            var softness = Mathf.Clamp01(cornerSoftness);
            var scale = Mathf.Lerp(0.03f, 0.08f, softness);
            return Math.Max(tolerance * 0.5f, expansionDistance * scale);
        }

        private static bool TryExtractLargestBoundary(
            PathsD paths,
            float tolerance,
            float minimumArea,
            out List<Vector2> polygon)
        {
            polygon = null;
            var bestArea = minimumArea;

            for (var pathIndex = 0; pathIndex < paths.Count; pathIndex++)
            {
                var candidate = NormalizePolygon(ToSampledPoints(paths[pathIndex]), tolerance);
                if (candidate.Count < 3)
                {
                    continue;
                }

                var signedArea = SignedArea(candidate);
                if (Mathf.Abs(signedArea) < minimumArea)
                {
                    continue;
                }

                if (signedArea < 0f)
                {
                    candidate.Reverse();
                }

                RemoveCollinearVertices(candidate, tolerance);
                if (!IsTerraceBoundaryValid(candidate, tolerance, minimumArea))
                {
                    continue;
                }

                var candidateArea = Mathf.Abs(SignedArea(candidate));
                if (candidateArea <= bestArea)
                {
                    continue;
                }

                bestArea = candidateArea;
                polygon = candidate;
            }

            return polygon != null;
        }

        private static List<Vector3> ToSampledPoints(PathD path)
        {
            var sampledPoints = new List<Vector3>(path.Count);
            for (var pointIndex = 0; pointIndex < path.Count; pointIndex++)
            {
                sampledPoints.Add(new Vector3((float)path[pointIndex].x, 0f, (float)path[pointIndex].y));
            }

            return sampledPoints;
        }

        private static List<Vector3> ToSampledPoints(List<Vector2> polygon)
        {
            var sampledPoints = new List<Vector3>(polygon.Count);
            for (var pointIndex = 0; pointIndex < polygon.Count; pointIndex++)
            {
                sampledPoints.Add(new Vector3(polygon[pointIndex].x, 0f, polygon[pointIndex].y));
            }

            return sampledPoints;
        }

        private static PathD ToClipperPath(List<Vector2> polygon)
        {
            var path = new PathD(polygon.Count);
            for (var pointIndex = 0; pointIndex < polygon.Count; pointIndex++)
            {
                path.Add(new PointD(polygon[pointIndex].x, polygon[pointIndex].y));
            }

            return path;
        }

        private static void AddHorizontalTriangle(
            PathD trianglePath,
            float height,
            Vector2 min,
            Vector2 size,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uv,
            List<int> triangles)
        {
            if (trianglePath.Count < 3)
            {
                return;
            }

            var triangleA = new Vector3((float)trianglePath[0].x, height, (float)trianglePath[0].y);
            var triangleB = new Vector3((float)trianglePath[1].x, height, (float)trianglePath[1].y);
            var triangleC = new Vector3((float)trianglePath[2].x, height, (float)trianglePath[2].y);

            var triangleNormal = Vector3.Cross(triangleB - triangleA, triangleC - triangleA);
            if (triangleNormal.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            var startIndex = vertices.Count;
            vertices.Add(triangleA);
            vertices.Add(triangleB);
            vertices.Add(triangleC);

            normals.Add(Vector3.up);
            normals.Add(Vector3.up);
            normals.Add(Vector3.up);

            uv.Add(GetPlanarUv(new Vector2(triangleA.x, triangleA.z), min, size));
            uv.Add(GetPlanarUv(new Vector2(triangleB.x, triangleB.z), min, size));
            uv.Add(GetPlanarUv(new Vector2(triangleC.x, triangleC.z), min, size));

            if (triangleNormal.y >= 0f)
            {
                triangles.Add(startIndex);
                triangles.Add(startIndex + 1);
                triangles.Add(startIndex + 2);
                return;
            }

            triangles.Add(startIndex);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex + 1);
        }

        private static bool IsPointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c, float tolerance)
        {
            var ab = Cross(a, b, point);
            var bc = Cross(b, c, point);
            var ca = Cross(c, a, point);

            var abTolerance = GetCrossTolerance(a, b, tolerance);
            var bcTolerance = GetCrossTolerance(b, c, tolerance);
            var caTolerance = GetCrossTolerance(c, a, tolerance);

            var hasNegative = ab < -abTolerance || bc < -bcTolerance || ca < -caTolerance;
            var hasPositive = ab > abTolerance || bc > bcTolerance || ca > caTolerance;
            return !(hasNegative && hasPositive);
        }

        private static bool SegmentsIntersect(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2, float tolerance)
        {
            var o1 = Orientation(a1, a2, b1, tolerance);
            var o2 = Orientation(a1, a2, b2, tolerance);
            var o3 = Orientation(b1, b2, a1, tolerance);
            var o4 = Orientation(b1, b2, a2, tolerance);

            if (o1 != o2 && o3 != o4)
            {
                return true;
            }

            if (o1 == 0 && OnSegment(a1, b1, a2, tolerance))
            {
                return true;
            }

            if (o2 == 0 && OnSegment(a1, b2, a2, tolerance))
            {
                return true;
            }

            if (o3 == 0 && OnSegment(b1, a1, b2, tolerance))
            {
                return true;
            }

            return o4 == 0 && OnSegment(b1, a2, b2, tolerance);
        }

        private static int Orientation(Vector2 a, Vector2 b, Vector2 c, float tolerance)
        {
            var value = Cross(a, b, c);
            if (Mathf.Abs(value) <= GetCrossTolerance(a, b, tolerance))
            {
                return 0;
            }

            return value > 0f ? 1 : 2;
        }

        private static bool OnSegment(Vector2 a, Vector2 point, Vector2 b, float tolerance)
        {
            return DistancePointToSegment(point, a, b) <= tolerance &&
                   point.x <= Mathf.Max(a.x, b.x) + tolerance &&
                   point.x >= Mathf.Min(a.x, b.x) - tolerance &&
                   point.y <= Mathf.Max(a.y, b.y) + tolerance &&
                   point.y >= Mathf.Min(a.y, b.y) - tolerance;
        }

        private static float GetCrossTolerance(Vector2 a, Vector2 b, float linearTolerance)
        {
            var edgeLength = Vector2.Distance(a, b);
            return Mathf.Max(linearTolerance * Mathf.Max(edgeLength, MinimumDimension), linearTolerance * linearTolerance);
        }

        private static float SignedArea(List<Vector2> polygon)
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

        private static float Cross(Vector2 a, Vector2 b, Vector2 c)
        {
            return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        }

        private static float DistancePointToSegment(Vector3 point, Vector3 start, Vector3 end)
        {
            var segment = end - start;
            if (segment.sqrMagnitude <= Mathf.Epsilon)
            {
                return Vector3.Distance(point, start);
            }

            var t = Vector3.Dot(point - start, segment) / segment.sqrMagnitude;
            t = Mathf.Clamp01(t);
            return Vector3.Distance(point, start + segment * t);
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
            return Vector2.Distance(point, start + segment * t);
        }

        private static bool NearlyEqual(Vector3 a, Vector3 b, float tolerance)
        {
            return Vector3.SqrMagnitude(a - b) <= tolerance * tolerance;
        }

        private static bool NearlyEqual(Vector2 a, Vector2 b, float tolerance)
        {
            return Vector2.SqrMagnitude(a - b) <= tolerance * tolerance;
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }
    }
}

