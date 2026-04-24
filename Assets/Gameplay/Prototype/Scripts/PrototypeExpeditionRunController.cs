using System.Collections.Generic;
using UnityEngine;

namespace Islands.Prototype
{
    [DisallowMultipleComponent]
    public sealed class PrototypeExpeditionRunController : MonoBehaviour
    {
        [SerializeField]
        private PrototypeCombatSandbox waveSpawner;

        [SerializeField]
        private PrototypeIslandTravelGate extractionGate;

        [SerializeField]
        private PrototypeExpeditionResourceNode[] resourceNodes;

        [SerializeField]
        [Min(0)]
        private int requiredKillCount = 50;

        [SerializeField]
        private bool lockExtractionOnStart = true;

        [SerializeField]
        private bool resetResourcesOnStart = true;

        [SerializeField]
        private bool resetObjectiveOnExtractionTravel = true;

        [SerializeField]
        private bool createRuntimeHud = true;

        [Header("Resource Placement")]
        [SerializeField]
        private bool randomizeResourcePositionsOnReset = true;

        [SerializeField]
        private MeshCollider resourceSpawnSurfaceCollider;

        [SerializeField]
        private bool autoFindResourceSpawnSurfaceByName = true;

        [SerializeField]
        private string resourceSpawnSurfaceObjectName = "Expedition Island";

        [SerializeField]
        [Min(1)]
        private int resourceSpawnAttemptsPerNode = 64;

        [SerializeField]
        [Min(0f)]
        private float resourceSpawnProbeHeight = 30f;

        [SerializeField]
        [Min(0f)]
        private float resourceSpawnProbeDepth = 80f;

        [SerializeField]
        [Min(0f)]
        private float resourceGroundOffset = 0.12f;

        [SerializeField]
        [Min(0f)]
        private float resourceFootprintRadius = 0.7f;

        [SerializeField]
        [Range(3, 16)]
        private int resourceFootprintSamples = 8;

        [SerializeField]
        [Min(0f)]
        private float maxResourceSurfaceHeightDelta = 0.35f;

        [SerializeField]
        [Min(0f)]
        private float minResourceSpacing = 7f;

        [SerializeField]
        [Min(0f)]
        private float minResourceDistanceFromPlayer = 5f;

        private readonly List<Vector3> resourceSpawnPositions = new List<Vector3>();
        private PrototypeIslandTravelGate subscribedExtractionGate;
        private PrototypePlayerController player;
        private bool completed;
        private bool warnedMissingResourceSpawnSurface;
        private bool warnedNoResourceSpawnPosition;

        public PrototypeCombatSandbox WaveSpawner => waveSpawner;

        public PrototypeIslandTravelGate ExtractionGate => extractionGate;

        public PrototypeExpeditionResourceNode[] ResourceNodes => resourceNodes;

        public int RequiredKillCount => requiredKillCount;

        public int KillCount => waveSpawner != null ? waveSpawner.TotalEnemiesKilled : 0;

        public int RequiredResourceCount => CountRequiredResources();

        public int CollectedResourceCount { get; private set; }

        public bool IsComplete => completed;

        public static bool GoalsComplete(int collectedResources, int requiredResources, int killCount, int requiredKills)
        {
            return collectedResources >= Mathf.Max(0, requiredResources)
                && killCount >= Mathf.Max(0, requiredKills);
        }

        public static bool TryResolveResourceSpawnCandidate(
            Collider surfaceCollider,
            Vector3 candidate,
            float probeHeight,
            float probeDepth,
            float groundOffset,
            float footprintRadius,
            int footprintSamples,
            float maxSurfaceHeightDelta,
            out Vector3 spawnPosition)
        {
            spawnPosition = candidate;
            if (surfaceCollider == null)
            {
                return false;
            }

            if (!PrototypeCombatSandbox.TryProjectToSurface(
                    surfaceCollider,
                    candidate,
                    probeHeight,
                    probeDepth,
                    groundOffset,
                    out spawnPosition))
            {
                return false;
            }

            return PrototypeCombatSandbox.HasSurfaceFootprint(
                surfaceCollider,
                spawnPosition,
                footprintRadius,
                footprintSamples,
                probeHeight,
                probeDepth,
                maxSurfaceHeightDelta + Mathf.Max(0f, groundOffset));
        }

        public static bool HasResourceSpawnSpacing(
            Vector3 candidate,
            IReadOnlyList<Vector3> existingPositions,
            float minSpacing)
        {
            if (existingPositions == null || minSpacing <= 0f)
            {
                return true;
            }

            var minSqrDistance = minSpacing * minSpacing;
            for (var i = 0; i < existingPositions.Count; i++)
            {
                var delta = existingPositions[i] - candidate;
                delta.y = 0f;
                if (delta.sqrMagnitude < minSqrDistance)
                {
                    return false;
                }
            }

            return true;
        }

        public void Configure(
            PrototypeCombatSandbox newWaveSpawner,
            PrototypeIslandTravelGate newExtractionGate,
            PrototypeExpeditionResourceNode[] newResourceNodes,
            int newRequiredKillCount)
        {
            if (isActiveAndEnabled)
            {
                UnsubscribeResources();
                UnsubscribeExtractionGate();
            }

            waveSpawner = newWaveSpawner;
            extractionGate = newExtractionGate;
            resourceNodes = newResourceNodes;
            requiredKillCount = Mathf.Max(0, newRequiredKillCount);

            if (isActiveAndEnabled)
            {
                SubscribeResources();
                SubscribeExtractionGate();
            }

            RecountResources();
        }

        private void OnEnable()
        {
            SubscribeResources();
            SubscribeExtractionGate();
        }

        private void Start()
        {
            BeginRunTracking();
        }

        private void OnDisable()
        {
            UnsubscribeResources();
            UnsubscribeExtractionGate();
        }

        private void OnValidate()
        {
            requiredKillCount = Mathf.Max(0, requiredKillCount);
            resourceSpawnSurfaceObjectName = string.IsNullOrWhiteSpace(resourceSpawnSurfaceObjectName)
                ? "Expedition Island"
                : resourceSpawnSurfaceObjectName.Trim();
            resourceSpawnAttemptsPerNode = Mathf.Max(1, resourceSpawnAttemptsPerNode);
            resourceSpawnProbeHeight = Mathf.Max(0f, resourceSpawnProbeHeight);
            resourceSpawnProbeDepth = Mathf.Max(0f, resourceSpawnProbeDepth);
            resourceGroundOffset = Mathf.Max(0f, resourceGroundOffset);
            resourceFootprintRadius = Mathf.Max(0f, resourceFootprintRadius);
            resourceFootprintSamples = Mathf.Clamp(resourceFootprintSamples, 3, 16);
            maxResourceSurfaceHeightDelta = Mathf.Max(0f, maxResourceSurfaceHeightDelta);
            minResourceSpacing = Mathf.Max(0f, minResourceSpacing);
            minResourceDistanceFromPlayer = Mathf.Max(0f, minResourceDistanceFromPlayer);
        }

        private void Update()
        {
            if (completed)
            {
                return;
            }

            RecountResources();
            TryCompleteRun();
        }

        public void BeginRunTracking()
        {
            completed = false;

            if (lockExtractionOnStart && extractionGate != null)
            {
                extractionGate.SetUnlocked(false);
            }

            if (resetResourcesOnStart)
            {
                ResetResources();
            }
            else
            {
                RecountResources();
            }

            if (createRuntimeHud)
            {
                PrototypeExpeditionRunHud.CreateFor(this);
            }

            if (waveSpawner != null)
            {
                waveSpawner.ArmRunStart();
            }

            TryCompleteRun();
        }

        private void ResetResources()
        {
            var nodes = resourceNodes;
            if (nodes == null)
            {
                CollectedResourceCount = 0;
                return;
            }

            if (randomizeResourcePositionsOnReset)
            {
                RandomizeResourcePositions(nodes);
            }

            foreach (var node in nodes)
            {
                if (node != null)
                {
                    node.ResetNode();
                }
            }

            RecountResources();
        }

        private void SubscribeResources()
        {
            var nodes = resourceNodes;
            if (nodes == null)
            {
                return;
            }

            foreach (var node in nodes)
            {
                if (node != null)
                {
                    node.Collected -= HandleResourceCollected;
                    node.Collected += HandleResourceCollected;
                }
            }
        }

        private void UnsubscribeResources()
        {
            var nodes = resourceNodes;
            if (nodes == null)
            {
                return;
            }

            foreach (var node in nodes)
            {
                if (node != null)
                {
                    node.Collected -= HandleResourceCollected;
                }
            }
        }

        private void SubscribeExtractionGate()
        {
            if (extractionGate == null || subscribedExtractionGate == extractionGate)
            {
                return;
            }

            UnsubscribeExtractionGate();
            subscribedExtractionGate = extractionGate;
            subscribedExtractionGate.Traveled += HandleExtractionGateTraveled;
        }

        private void UnsubscribeExtractionGate()
        {
            if (subscribedExtractionGate == null)
            {
                return;
            }

            subscribedExtractionGate.Traveled -= HandleExtractionGateTraveled;
            subscribedExtractionGate = null;
        }

        private void HandleResourceCollected(PrototypeExpeditionResourceNode resourceNode)
        {
            RecountResources();
            TryCompleteRun();
        }

        private void HandleExtractionGateTraveled(PrototypeIslandTravelGate gate, PrototypePlayerController player)
        {
            if (!resetObjectiveOnExtractionTravel || !completed)
            {
                return;
            }

            if (player != null)
            {
                player.RestoreHealth();
            }

            BeginRunTracking();
        }

        private void RecountResources()
        {
            var count = 0;
            var nodes = resourceNodes;
            if (nodes != null)
            {
                foreach (var node in nodes)
                {
                    if (node != null && node.IsCollected)
                    {
                        count++;
                    }
                }
            }

            CollectedResourceCount = count;
        }

        private bool TryCompleteRun()
        {
            if (completed || !GoalsComplete(CollectedResourceCount, RequiredResourceCount, KillCount, requiredKillCount))
            {
                return false;
            }

            completed = true;

            if (extractionGate != null)
            {
                extractionGate.SetUnlocked(true);
            }

            if (waveSpawner != null)
            {
                waveSpawner.CompleteRunAndKillRemainingEnemies();
            }

            return true;
        }

        private int CountRequiredResources()
        {
            var count = 0;
            var nodes = resourceNodes;
            if (nodes == null)
            {
                return 0;
            }

            foreach (var node in nodes)
            {
                if (node != null)
                {
                    count++;
                }
            }

            return count;
        }

        private void RandomizeResourcePositions(PrototypeExpeditionResourceNode[] nodes)
        {
            resourceSpawnPositions.Clear();
            var surfaceCollider = ResolveResourceSpawnSurfaceCollider();
            if (surfaceCollider == null)
            {
                WarnMissingResourceSpawnSurface();
                return;
            }

            foreach (var node in nodes)
            {
                if (node == null)
                {
                    continue;
                }

                if (TryGetResourceSpawnPosition(surfaceCollider, out var spawnPosition))
                {
                    node.transform.position = spawnPosition;
                    resourceSpawnPositions.Add(spawnPosition);
                    continue;
                }

                WarnNoResourceSpawnPosition();
                resourceSpawnPositions.Add(node.transform.position);
            }
        }

        private bool TryGetResourceSpawnPosition(Collider surfaceCollider, out Vector3 spawnPosition)
        {
            spawnPosition = transform.position;
            var bounds = surfaceCollider.bounds;
            for (var i = 0; i < resourceSpawnAttemptsPerNode; i++)
            {
                var candidate = new Vector3(
                    Random.Range(bounds.min.x, bounds.max.x),
                    bounds.center.y,
                    Random.Range(bounds.min.z, bounds.max.z));

                if (!TryResolveResourceSpawnCandidate(
                        surfaceCollider,
                        candidate,
                        resourceSpawnProbeHeight,
                        resourceSpawnProbeDepth,
                        resourceGroundOffset,
                        resourceFootprintRadius,
                        resourceFootprintSamples,
                        maxResourceSurfaceHeightDelta,
                        out var resolvedCandidate))
                {
                    continue;
                }

                if (!HasResourceSpawnSpacing(resolvedCandidate, resourceSpawnPositions, minResourceSpacing)
                    || !HasPlayerDistance(resolvedCandidate))
                {
                    continue;
                }

                spawnPosition = resolvedCandidate;
                return true;
            }

            return false;
        }

        private bool HasPlayerDistance(Vector3 candidate)
        {
            if (minResourceDistanceFromPlayer <= 0f)
            {
                return true;
            }

            player = player != null ? player : FindFirstObjectByType<PrototypePlayerController>();
            if (player == null)
            {
                return true;
            }

            var delta = player.transform.position - candidate;
            delta.y = 0f;
            return delta.sqrMagnitude >= minResourceDistanceFromPlayer * minResourceDistanceFromPlayer;
        }

        private MeshCollider ResolveResourceSpawnSurfaceCollider()
        {
            if (resourceSpawnSurfaceCollider != null)
            {
                return resourceSpawnSurfaceCollider;
            }

            if (waveSpawner != null)
            {
                resourceSpawnSurfaceCollider = waveSpawner.ResolvedSpawnSurfaceCollider;
                if (resourceSpawnSurfaceCollider != null)
                {
                    return resourceSpawnSurfaceCollider;
                }
            }

            if (!autoFindResourceSpawnSurfaceByName || string.IsNullOrWhiteSpace(resourceSpawnSurfaceObjectName))
            {
                return null;
            }

            var surfaceObject = GameObject.Find(resourceSpawnSurfaceObjectName);
            resourceSpawnSurfaceCollider = surfaceObject != null ? surfaceObject.GetComponent<MeshCollider>() : null;
            return resourceSpawnSurfaceCollider;
        }

        private void WarnMissingResourceSpawnSurface()
        {
            if (warnedMissingResourceSpawnSurface)
            {
                return;
            }

            warnedMissingResourceSpawnSurface = true;
            Debug.LogWarning(
                $"{nameof(PrototypeExpeditionRunController)} on {name} could not find a resource spawn surface. Resources will keep their authored positions.",
                this);
        }

        private void WarnNoResourceSpawnPosition()
        {
            if (warnedNoResourceSpawnPosition)
            {
                return;
            }

            warnedNoResourceSpawnPosition = true;
            Debug.LogWarning(
                $"{nameof(PrototypeExpeditionRunController)} on {name} could not find enough valid randomized resource positions. Some resources will keep their previous positions.",
                this);
        }
    }
}
