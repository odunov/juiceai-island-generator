using Islands.Prototype;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Islands.EditorTools.Tests
{
    public sealed class PrototypeWaveSystemTests
    {
        [Test]
        public void GetWaveComposition_FirstWaveStartsWithOneOfEachEnemy()
        {
            var composition = PrototypeCombatSandbox.GetWaveComposition(1);

            Assert.That(composition.Wave, Is.EqualTo(1));
            Assert.That(composition.ChaserCount, Is.EqualTo(1));
            Assert.That(composition.SpitterCount, Is.EqualTo(1));
            Assert.That(composition.TotalCount, Is.EqualTo(2));
        }

        [Test]
        public void GetWaveComposition_IncreasesTotalEnemiesByOnePerWave()
        {
            for (var wave = 1; wave <= 10; wave++)
            {
                var composition = PrototypeCombatSandbox.GetWaveComposition(wave);

                Assert.That(composition.TotalCount, Is.EqualTo(wave + 1));
            }
        }

        [Test]
        public void GetWaveComposition_SpittersNeverExceedHalfTheWave()
        {
            for (var wave = 1; wave <= 20; wave++)
            {
                var composition = PrototypeCombatSandbox.GetWaveComposition(wave);

                Assert.That(composition.SpitterCount, Is.LessThanOrEqualTo(composition.TotalCount / 2));
                Assert.That(composition.SpitterCount, Is.GreaterThanOrEqualTo(1));
            }
        }

        [Test]
        public void GetWaveComposition_ConfigurableScalingUsesDesignerValues()
        {
            var composition = PrototypeCombatSandbox.GetWaveComposition(
                wave: 3,
                firstWaveEnemyCount: 4,
                enemiesAddedPerWave: 2,
                spitterRatio: 0.25f,
                maxSpitterShare: 0.5f);

            Assert.That(composition.Wave, Is.EqualTo(3));
            Assert.That(composition.TotalCount, Is.EqualTo(8));
            Assert.That(composition.SpitterCount, Is.EqualTo(2));
            Assert.That(composition.ChaserCount, Is.EqualTo(6));
        }

        [Test]
        public void GetWaveComposition_ClampsSpittersToConfiguredShare()
        {
            var composition = PrototypeCombatSandbox.GetWaveComposition(
                wave: 1,
                firstWaveEnemyCount: 10,
                enemiesAddedPerWave: 0,
                spitterRatio: 1f,
                maxSpitterShare: 0.2f);

            Assert.That(composition.TotalCount, Is.EqualTo(10));
            Assert.That(composition.SpitterCount, Is.EqualTo(2));
            Assert.That(composition.ChaserCount, Is.EqualTo(8));
        }

        [Test]
        public void GetWaveCompositionWithEnemyBonus_AddsCorruptionScalingBeforeEnemySplit()
        {
            var composition = PrototypeCombatSandbox.GetWaveCompositionWithEnemyBonus(
                wave: 3,
                firstWaveEnemyCount: 2,
                enemiesAddedPerWave: 1,
                flatEnemyBonus: 4,
                spitterRatio: 0.25f,
                maxSpitterShare: 0.5f);

            Assert.That(composition.Wave, Is.EqualTo(3));
            Assert.That(composition.TotalCount, Is.EqualTo(8));
            Assert.That(composition.SpitterCount, Is.EqualTo(2));
            Assert.That(composition.ChaserCount, Is.EqualTo(6));
        }

        [Test]
        public void GetWaveIntervalForCorruptionLevel_DropsEveryTwoLevelsAndStopsAtFloor()
        {
            Assert.That(PrototypeCombatSandbox.GetWaveIntervalForCorruptionLevel(0, 20f, 2, 14f), Is.EqualTo(20f).Within(0.001f));
            Assert.That(PrototypeCombatSandbox.GetWaveIntervalForCorruptionLevel(1, 20f, 2, 14f), Is.EqualTo(20f).Within(0.001f));
            Assert.That(PrototypeCombatSandbox.GetWaveIntervalForCorruptionLevel(2, 20f, 2, 14f), Is.EqualTo(19f).Within(0.001f));
            Assert.That(PrototypeCombatSandbox.GetWaveIntervalForCorruptionLevel(11, 20f, 2, 14f), Is.EqualTo(15f).Within(0.001f));
            Assert.That(PrototypeCombatSandbox.GetWaveIntervalForCorruptionLevel(12, 20f, 2, 14f), Is.EqualTo(14f).Within(0.001f));
            Assert.That(PrototypeCombatSandbox.GetWaveIntervalForCorruptionLevel(24, 20f, 2, 14f), Is.EqualTo(14f).Within(0.001f));
        }

        [Test]
        public void AdvanceCorruption_PreservesCarryOverWhenMeterRollsOver()
        {
            var nextLevel = PrototypeCombatSandbox.AdvanceCorruption(
                currentMeterValue: 90f,
                currentCorruptionLevel: 2,
                deltaTime: 8.5f,
                corruptionMeterMax: 100f,
                secondsPerCorruptionLevel: 42.5f,
                out var nextMeterValue);

            Assert.That(nextLevel, Is.EqualTo(3));
            Assert.That(nextMeterValue, Is.EqualTo(10f).Within(0.001f));
        }

        [Test]
        public void TryProjectToSurface_ReturnsProjectedPointOnMeshCollider()
        {
            var surface = CreateSquareSurface("spawn-surface", new Vector3(10f, 2f, -7f), 10f, out var mesh);
            try
            {
                var collider = surface.GetComponent<MeshCollider>();

                var projected = PrototypeCombatSandbox.TryProjectToSurface(
                    collider,
                    new Vector3(10f, 2f, -7f),
                    probeHeight: 12f,
                    probeDepth: 20f,
                    groundOffset: 0.05f,
                    out var surfacePoint);

                Assert.That(projected, Is.True);
                Assert.That(surfacePoint.x, Is.EqualTo(10f).Within(0.001f));
                Assert.That(surfacePoint.y, Is.EqualTo(2.05f).Within(0.001f));
                Assert.That(surfacePoint.z, Is.EqualTo(-7f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(surface);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void TryProjectToSurface_RejectsPointsOutsideMeshCollider()
        {
            var surface = CreateSquareSurface("spawn-surface", Vector3.zero, 10f, out var mesh);
            try
            {
                var collider = surface.GetComponent<MeshCollider>();

                var projected = PrototypeCombatSandbox.TryProjectToSurface(
                    collider,
                    new Vector3(8f, 0f, 0f),
                    probeHeight: 12f,
                    probeDepth: 20f,
                    groundOffset: 0f,
                    out _);

                Assert.That(projected, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(surface);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void HasSurfaceFootprint_RejectsCandidateTooCloseToEdge()
        {
            var surface = CreateSquareSurface("spawn-surface", Vector3.zero, 10f, out var mesh);
            try
            {
                var collider = surface.GetComponent<MeshCollider>();

                var centered = PrototypeCombatSandbox.HasSurfaceFootprint(
                    collider,
                    Vector3.zero,
                    footprintRadius: 1f,
                    sampleCount: 8,
                    probeHeight: 12f,
                    probeDepth: 20f,
                    maxHeightDelta: 0.05f);
                var nearEdge = PrototypeCombatSandbox.HasSurfaceFootprint(
                    collider,
                    new Vector3(4.8f, 0f, 0f),
                    footprintRadius: 0.5f,
                    sampleCount: 8,
                    probeHeight: 12f,
                    probeDepth: 20f,
                    maxHeightDelta: 0.05f);

                Assert.That(centered, Is.True);
                Assert.That(nearEdge, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(surface);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void ResourceSpawnCandidate_ProjectsToValidSurfacePoint()
        {
            var surface = CreateSquareSurface("resource-surface", new Vector3(15f, 1.5f, -4f), 10f, out var mesh);
            try
            {
                var collider = surface.GetComponent<MeshCollider>();

                var resolved = PrototypeExpeditionRunController.TryResolveResourceSpawnCandidate(
                    collider,
                    new Vector3(15f, 1.5f, -4f),
                    probeHeight: 12f,
                    probeDepth: 20f,
                    groundOffset: 0.12f,
                    footprintRadius: 0.5f,
                    footprintSamples: 8,
                    maxSurfaceHeightDelta: 0.05f,
                    out var spawnPoint);

                Assert.That(resolved, Is.True);
                Assert.That(spawnPoint.x, Is.EqualTo(15f).Within(0.001f));
                Assert.That(spawnPoint.y, Is.EqualTo(1.62f).Within(0.001f));
                Assert.That(spawnPoint.z, Is.EqualTo(-4f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(surface);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void ResourceSpawnSpacing_RejectsNearbyResource()
        {
            var existing = new[]
            {
                new Vector3(20f, 3f, 20f)
            };

            Assert.That(PrototypeExpeditionRunController.HasResourceSpawnSpacing(new Vector3(22f, 0f, 20f), existing, 3f), Is.False);
            Assert.That(PrototypeExpeditionRunController.HasResourceSpawnSpacing(new Vector3(24f, 0f, 20f), existing, 3f), Is.True);
        }

        [Test]
        public void WaveHud_CreateForBuildsRuntimeCounterText()
        {
            var sandboxObject = new GameObject("wave-sandbox");
            try
            {
                var sandbox = sandboxObject.AddComponent<PrototypeCombatSandbox>();

                var hud = PrototypeWaveHud.CreateFor(sandbox);

                Assert.That(hud.Source, Is.SameAs(sandbox));
                Assert.That(hud.GetComponent<Canvas>(), Is.Not.Null);
                Assert.That(hud.GetComponentsInChildren<Text>(true), Has.Length.EqualTo(5));
                Assert.That(hud.GetComponentsInChildren<Image>(true).Length, Is.GreaterThanOrEqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(sandboxObject);
            }
        }

        [Test]
        public void ExpeditionGoals_RequireResourcesAndKills()
        {
            Assert.That(PrototypeExpeditionRunController.GoalsComplete(2, 3, 50, 50), Is.False);
            Assert.That(PrototypeExpeditionRunController.GoalsComplete(3, 3, 49, 50), Is.False);
            Assert.That(PrototypeExpeditionRunController.GoalsComplete(3, 3, 50, 50), Is.True);
        }

        [Test]
        public void ExpeditionResourceNode_CollectsOnlyOnce()
        {
            var resourceObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                PrototypePersistentInventory.ResetProgression();
                var resource = resourceObject.AddComponent<PrototypeExpeditionResourceNode>();
                var collectedCount = 0;
                resource.Collected += _ => collectedCount++;

                Assert.That(resource.TryCollect(), Is.True);
                Assert.That(resource.TryCollect(), Is.False);

                Assert.That(resource.IsCollected, Is.True);
                Assert.That(collectedCount, Is.EqualTo(1));
                Assert.That(PrototypePersistentInventory.Resources, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(resourceObject);
                PrototypePersistentInventory.ResetProgression();
            }
        }

        [Test]
        public void IslandTravelGate_CanBeLockedByController()
        {
            var gateObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var gate = gateObject.AddComponent<PrototypeIslandTravelGate>();

                gate.SetUnlocked(false);

                Assert.That(gate.IsUnlocked, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gateObject);
            }
        }

        [Test]
        public void PersistentInventory_ReloadsCollectedTotals()
        {
            try
            {
                PrototypePersistentInventory.ResetProgression();

                PrototypePersistentInventory.AddCurrency(7);
                PrototypePersistentInventory.AddResources(3);
                PrototypePersistentInventory.ReloadFromStorage();

                Assert.That(PrototypePersistentInventory.Currency, Is.EqualTo(7));
                Assert.That(PrototypePersistentInventory.Resources, Is.EqualTo(3));
            }
            finally
            {
                PrototypePersistentInventory.ResetProgression();
            }
        }

        [Test]
        public void ExpeditionHud_CreateForBuildsRuntimeCounterText()
        {
            var controllerObject = new GameObject("expedition-controller");
            try
            {
                var controller = controllerObject.AddComponent<PrototypeExpeditionRunController>();

                var hud = PrototypeExpeditionRunHud.CreateFor(controller);

                Assert.That(hud.Source, Is.SameAs(controller));
                Assert.That(hud.GetComponent<Canvas>(), Is.Not.Null);
                Assert.That(hud.GetComponentsInChildren<Text>(true), Has.Length.EqualTo(5));
            }
            finally
            {
                Object.DestroyImmediate(controllerObject);
            }
        }

        private static GameObject CreateSquareSurface(string name, Vector3 position, float size, out Mesh mesh)
        {
            var halfSize = size * 0.5f;
            mesh = new Mesh
            {
                name = $"{name}-mesh",
                vertices = new[]
                {
                    new Vector3(-halfSize, 0f, -halfSize),
                    new Vector3(-halfSize, 0f, halfSize),
                    new Vector3(halfSize, 0f, halfSize),
                    new Vector3(halfSize, 0f, -halfSize)
                },
                triangles = new[]
                {
                    0, 1, 2,
                    0, 2, 3
                }
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var surface = new GameObject(name);
            surface.transform.position = position;
            var collider = surface.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            Physics.SyncTransforms();
            return surface;
        }
    }
}
