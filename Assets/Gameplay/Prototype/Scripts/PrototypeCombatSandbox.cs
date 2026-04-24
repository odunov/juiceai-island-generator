using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Islands.Prototype
{
    public readonly struct PrototypeWaveComposition
    {
        public PrototypeWaveComposition(int wave, int chaserCount, int spitterCount)
        {
            Wave = Mathf.Max(1, wave);
            ChaserCount = Mathf.Max(0, chaserCount);
            SpitterCount = Mathf.Max(0, spitterCount);
        }

        public int Wave { get; }

        public int ChaserCount { get; }

        public int SpitterCount { get; }

        public int TotalCount => ChaserCount + SpitterCount;
    }

    public enum PrototypeWaveStartMode
    {
        Immediate,
        OnPlayerEnteredSurface,
        Manual
    }

    internal readonly struct PrototypeQueuedEnemySpawn
    {
        public PrototypeQueuedEnemySpawn(int wave, PrototypeEnemyType enemyType)
        {
            Wave = Mathf.Max(1, wave);
            EnemyType = enemyType;
        }

        public int Wave { get; }

        public PrototypeEnemyType EnemyType { get; }
    }

    [DisallowMultipleComponent]
    public sealed class PrototypeCombatSandbox : MonoBehaviour
    {
        private const int DefaultSpawnAttempts = 24;
        private const int MaxRecentDebugPoints = 12;
        private const float MinimumEnemyCombatHeight = 1.5f;
        private const float DefaultSpitterRatio = 1f / 3f;
        private const float DefaultMaxSpitterShare = 0.5f;

        [Header("Run Start")]
        [SerializeField]
        private PrototypeWaveStartMode startMode = PrototypeWaveStartMode.OnPlayerEnteredSurface;

        [SerializeField]
        private MeshCollider spawnSurfaceCollider;

        [SerializeField]
        private bool autoFindSpawnSurfaceByName = true;

        [SerializeField]
        private string spawnSurfaceObjectName = "Expedition Island";

        [SerializeField]
        [Min(0f)]
        private float playerSurfaceProbeHeight = 30f;

        [SerializeField]
        [Min(0f)]
        private float playerSurfaceProbeDepth = 80f;

        [Header("Waves")]
        [SerializeField]
        private bool spawnEnemies = true;

        [SerializeField]
        private bool onlySpawnWhenNoTargetsExist = true;

        [SerializeField]
        private bool createRuntimeHud = true;

        [SerializeField]
        [Min(0f)]
        private float firstWaveDelay = 0.35f;

        [FormerlySerializedAs("nextWaveDelay")]
        [SerializeField]
        [Min(0f)]
        private float baseWaveInterval = 20f;

        [SerializeField]
        [Min(0.01f)]
        private float corruptionMeterMax = 100f;

        [SerializeField]
        [Min(0.01f)]
        private float secondsPerCorruptionLevel = 42.5f;

        [SerializeField]
        [Min(0f)]
        private float minimumWaveInterval = 14f;

        [SerializeField]
        [Min(1)]
        private int waveIntervalReductionEveryLevels = 2;

        [SerializeField]
        [Min(0)]
        private int enemiesAddedPerCorruptionLevel = 1;

        [SerializeField]
        [Min(1)]
        private int firstWaveEnemyCount = 2;

        [SerializeField]
        [Min(0)]
        private int enemiesAddedPerWave = 1;

        [SerializeField]
        [Range(0f, 1f)]
        private float spitterRatio = DefaultSpitterRatio;

        [SerializeField]
        [Range(0f, 1f)]
        private float maxSpitterShare = DefaultMaxSpitterShare;

        [Header("Spawn Placement")]
        [SerializeField]
        [Min(0f)]
        private float spawnInterval = 0.45f;

        [SerializeField]
        [Min(1)]
        private int maxAliveAtOnce = 6;

        [SerializeField]
        [Min(0f)]
        private float minSpawnRadius = 5.5f;

        [SerializeField]
        [Min(0f)]
        private float maxSpawnRadius = 8.5f;

        [SerializeField]
        [Min(0f)]
        private float minEnemySpacing = 1.4f;

        [SerializeField]
        [Min(1)]
        private int spawnAttempts = DefaultSpawnAttempts;

        [SerializeField]
        [Min(0f)]
        private float spawnSurfaceProbeHeight = 30f;

        [SerializeField]
        [Min(0f)]
        private float spawnSurfaceProbeDepth = 80f;

        [SerializeField]
        [Min(0f)]
        private float spawnGroundOffset = 0.02f;

        [SerializeField]
        [Min(0f)]
        private float spawnFootprintRadius = 0.55f;

        [SerializeField]
        [Range(3, 16)]
        private int spawnFootprintSamples = 8;

        [SerializeField]
        [Min(0f)]
        private float maxSurfaceHeightDelta = 0.35f;

        [SerializeField]
        private bool drawDebugGizmos = true;

        [Header("Spawn Visuals")]
        [SerializeField]
        [Min(0f)]
        private float spawnIndicatorDuration = 1f;

        [SerializeField]
        [Min(0f)]
        private float spawnIndicatorRadius = 0.95f;

        [SerializeField]
        [Min(0f)]
        private float spawnIndicatorLineWidth = 0.055f;

        [SerializeField]
        [Min(0f)]
        private float spawnIndicatorPulseScale = 0.28f;

        [SerializeField]
        private Color spawnIndicatorColor = new Color(1f, 0.58f, 0.16f, 0.78f);

        [Header("Fallback Dummies")]
        [SerializeField]
        private bool spawnTargetDummies;

        [SerializeField]
        private Vector3[] targetDummyPositions =
        {
            new Vector3(2.3f, 0.75f, 5.8f),
            new Vector3(6.2f, 0.75f, 6.6f)
        };

        [SerializeField]
        private Vector3 targetDummyScale = new Vector3(0.8f, 1.5f, 0.8f);

        [SerializeField]
        private Color targetDummyColor = new Color(0.9f, 0.16f, 0.12f, 1f);

        [Header("Respawn")]
        [SerializeField]
        [Min(0.05f)]
        private float despawnDuration = 1.15f;

        [SerializeField]
        [Min(0f)]
        private float respawnDelay = 0.35f;

        [SerializeField]
        [Min(0f)]
        private float despawnDropDistance = 1.25f;

        [SerializeField]
        [Min(0f)]
        private float respawnRadius = 1.75f;

        [SerializeField]
        private bool clearLooseResourceDropsOnStop = true;

        private readonly List<GameObject> spawnedActors = new List<GameObject>();
        private readonly List<PrototypeEnemyController> aliveEnemies = new List<PrototypeEnemyController>();
        private readonly List<PrototypeEnemyType> waveBuildBuffer = new List<PrototypeEnemyType>();
        private readonly Queue<PrototypeQueuedEnemySpawn> queuedEnemies = new Queue<PrototypeQueuedEnemySpawn>();
        private readonly List<Vector3> recentSpawnPositions = new List<Vector3>();
        private readonly List<GameObject> spawnIndicators = new List<GameObject>();
        private Coroutine waveRoutine;
        private Coroutine spawnRoutine;
        private Coroutine activationRoutine;
        private PrototypePlayerController player;
        private bool isRunActive;
        private bool warnedMissingSpawnSurface;
        private bool warnedNoSpawnPosition;
        private float corruptionMeterValue;
        private float currentWaveCountdown;

        public int CurrentWave { get; private set; }

        public int AliveEnemyCount { get; private set; }

        public int TotalEnemiesKilled { get; private set; }

        public int CorruptionLevel { get; private set; }

        public bool IsRunActive => isRunActive;

        public float CorruptionMeterValue => Mathf.Clamp(corruptionMeterValue, 0f, corruptionMeterMax);

        public float CorruptionMeterMax => corruptionMeterMax;

        public float CorruptionMeterNormalized => corruptionMeterMax > 0f
            ? Mathf.Clamp01(corruptionMeterValue / corruptionMeterMax)
            : 0f;

        public float CurrentWaveCountdown => Mathf.Max(0f, currentWaveCountdown);

        public float CurrentWaveInterval => GetWaveIntervalForCorruptionLevel(
            CorruptionLevel,
            baseWaveInterval,
            waveIntervalReductionEveryLevels,
            minimumWaveInterval);

        public MeshCollider ResolvedSpawnSurfaceCollider
        {
            get
            {
                ResolveSpawnSurfaceCollider();
                return spawnSurfaceCollider;
            }
        }

        public static PrototypeWaveComposition GetWaveComposition(int wave)
        {
            return GetWaveComposition(
                wave,
                firstWaveEnemyCount: 2,
                enemiesAddedPerWave: 1,
                spitterRatio: DefaultSpitterRatio,
                maxSpitterShare: DefaultMaxSpitterShare);
        }

        public static PrototypeWaveComposition GetWaveComposition(
            int wave,
            int firstWaveEnemyCount,
            int enemiesAddedPerWave,
            float spitterRatio,
            float maxSpitterShare)
        {
            var safeWave = Mathf.Max(1, wave);
            var safeFirstWaveEnemyCount = Mathf.Max(1, firstWaveEnemyCount);
            var safeEnemiesAddedPerWave = Mathf.Max(0, enemiesAddedPerWave);
            var totalCount = safeFirstWaveEnemyCount + (safeWave - 1) * safeEnemiesAddedPerWave;
            return BuildWaveComposition(safeWave, totalCount, spitterRatio, maxSpitterShare);
        }

        public static PrototypeWaveComposition GetWaveCompositionWithEnemyBonus(
            int wave,
            int firstWaveEnemyCount,
            int enemiesAddedPerWave,
            int flatEnemyBonus,
            float spitterRatio,
            float maxSpitterShare)
        {
            var safeWave = Mathf.Max(1, wave);
            var safeFirstWaveEnemyCount = Mathf.Max(1, firstWaveEnemyCount);
            var safeEnemiesAddedPerWave = Mathf.Max(0, enemiesAddedPerWave);
            var safeEnemyBonus = Mathf.Max(0, flatEnemyBonus);
            var totalCount = safeFirstWaveEnemyCount + (safeWave - 1) * safeEnemiesAddedPerWave + safeEnemyBonus;
            return BuildWaveComposition(safeWave, totalCount, spitterRatio, maxSpitterShare);
        }

        public static int GetEnemyBonusForCorruptionLevel(int corruptionLevel, int enemiesAddedPerCorruptionLevel)
        {
            return Mathf.Max(0, corruptionLevel) * Mathf.Max(0, enemiesAddedPerCorruptionLevel);
        }

        public static float GetWaveIntervalForCorruptionLevel(
            int corruptionLevel,
            float baseWaveInterval,
            int waveIntervalReductionEveryLevels,
            float minimumWaveInterval)
        {
            var safeBaseWaveInterval = Mathf.Max(0f, baseWaveInterval);
            var safeMinimumWaveInterval = Mathf.Clamp(minimumWaveInterval, 0f, safeBaseWaveInterval);
            var safeLevelsPerReduction = Mathf.Max(1, waveIntervalReductionEveryLevels);
            var reductionSteps = Mathf.Max(0, corruptionLevel) / safeLevelsPerReduction;
            return Mathf.Max(safeMinimumWaveInterval, safeBaseWaveInterval - reductionSteps);
        }

        public static int AdvanceCorruption(
            float currentMeterValue,
            int currentCorruptionLevel,
            float deltaTime,
            float corruptionMeterMax,
            float secondsPerCorruptionLevel,
            out float nextMeterValue)
        {
            var safeCorruptionMeterMax = Mathf.Max(0.0001f, corruptionMeterMax);
            var safeSecondsPerLevel = Mathf.Max(0.0001f, secondsPerCorruptionLevel);
            var safeCorruptionLevel = Mathf.Max(0, currentCorruptionLevel);
            var safeCurrentMeterValue = Mathf.Clamp(currentMeterValue, 0f, safeCorruptionMeterMax);
            if (deltaTime <= 0f)
            {
                nextMeterValue = safeCurrentMeterValue;
                return safeCorruptionLevel;
            }

            var addedMeterValue = deltaTime * safeCorruptionMeterMax / safeSecondsPerLevel;
            var totalMeterValue = safeCurrentMeterValue + addedMeterValue;
            var levelsGained = Mathf.FloorToInt(totalMeterValue / safeCorruptionMeterMax);
            nextMeterValue = totalMeterValue - levelsGained * safeCorruptionMeterMax;
            return safeCorruptionLevel + levelsGained;
        }

        private static PrototypeWaveComposition BuildWaveComposition(
            int wave,
            int totalCount,
            float spitterRatio,
            float maxSpitterShare)
        {
            var safeTotalCount = Mathf.Max(1, totalCount);
            var maxSpitters = Mathf.FloorToInt(safeTotalCount * Mathf.Clamp01(maxSpitterShare));
            var spitterCount = Mathf.Min(Mathf.RoundToInt(safeTotalCount * Mathf.Clamp01(spitterRatio)), maxSpitters);
            if (spitterRatio > 0f && maxSpitters > 0 && safeTotalCount > 1)
            {
                spitterCount = Mathf.Max(1, spitterCount);
            }

            var chaserCount = safeTotalCount - spitterCount;
            return new PrototypeWaveComposition(Mathf.Max(1, wave), chaserCount, spitterCount);
        }

        public PrototypeWaveComposition GetConfiguredWaveComposition(int wave)
        {
            return GetWaveCompositionWithEnemyBonus(
                wave,
                firstWaveEnemyCount,
                enemiesAddedPerWave,
                GetEnemyBonusForCorruptionLevel(CorruptionLevel, enemiesAddedPerCorruptionLevel),
                spitterRatio,
                maxSpitterShare);
        }

        private void Awake()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            ResolveSpawnSurfaceCollider();

            if (onlySpawnWhenNoTargetsExist && PrototypeCombatTarget.Targets.Count > 0)
            {
                return;
            }

            if (spawnEnemies)
            {
                player = FindFirstObjectByType<PrototypePlayerController>();
                if (createRuntimeHud)
                {
                    PrototypeWaveHud.CreateFor(this);
                }

                ArmRunStart();
                return;
            }

            if (!spawnTargetDummies)
            {
                return;
            }

            for (var i = 0; i < targetDummyPositions.Length; i++)
            {
                SpawnDummy(i);
            }
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            StopRunRoutines();

            foreach (var enemy in aliveEnemies)
            {
                if (enemy != null && enemy.TryGetComponent<PrototypeHealth>(out var health))
                {
                    health.Died -= HandleWaveEnemyDied;
                }
            }

            foreach (var actor in spawnedActors)
            {
                if (actor != null)
                {
                    Destroy(actor);
                }
            }

            spawnedActors.Clear();
            aliveEnemies.Clear();
            queuedEnemies.Clear();
            ClearSpawnIndicators();
            ClearLooseResourceDrops();
            recentSpawnPositions.Clear();
            AliveEnemyCount = 0;
            ResetCorruptionTracking();
            isRunActive = false;
        }

        private void OnValidate()
        {
            spawnSurfaceObjectName = string.IsNullOrWhiteSpace(spawnSurfaceObjectName)
                ? "Expedition Island"
                : spawnSurfaceObjectName.Trim();
            firstWaveDelay = Mathf.Max(0f, firstWaveDelay);
            baseWaveInterval = Mathf.Max(0f, baseWaveInterval);
            corruptionMeterMax = Mathf.Max(0.01f, corruptionMeterMax);
            secondsPerCorruptionLevel = Mathf.Max(0.01f, secondsPerCorruptionLevel);
            minimumWaveInterval = Mathf.Clamp(minimumWaveInterval, 0f, baseWaveInterval);
            waveIntervalReductionEveryLevels = Mathf.Max(1, waveIntervalReductionEveryLevels);
            enemiesAddedPerCorruptionLevel = Mathf.Max(0, enemiesAddedPerCorruptionLevel);
            firstWaveEnemyCount = Mathf.Max(1, firstWaveEnemyCount);
            enemiesAddedPerWave = Mathf.Max(0, enemiesAddedPerWave);
            spitterRatio = Mathf.Clamp01(spitterRatio);
            maxSpitterShare = Mathf.Clamp01(maxSpitterShare);
            spawnInterval = Mathf.Max(0f, spawnInterval);
            maxAliveAtOnce = Mathf.Max(1, maxAliveAtOnce);
            minSpawnRadius = Mathf.Max(0f, minSpawnRadius);
            maxSpawnRadius = Mathf.Max(minSpawnRadius, maxSpawnRadius);
            minEnemySpacing = Mathf.Max(0f, minEnemySpacing);
            spawnAttempts = Mathf.Max(1, spawnAttempts);
            playerSurfaceProbeHeight = Mathf.Max(0f, playerSurfaceProbeHeight);
            playerSurfaceProbeDepth = Mathf.Max(0f, playerSurfaceProbeDepth);
            spawnSurfaceProbeHeight = Mathf.Max(0f, spawnSurfaceProbeHeight);
            spawnSurfaceProbeDepth = Mathf.Max(0f, spawnSurfaceProbeDepth);
            spawnGroundOffset = Mathf.Max(0f, spawnGroundOffset);
            spawnFootprintRadius = Mathf.Max(0f, spawnFootprintRadius);
            spawnFootprintSamples = Mathf.Clamp(spawnFootprintSamples, 3, 16);
            maxSurfaceHeightDelta = Mathf.Max(0f, maxSurfaceHeightDelta);
            spawnIndicatorDuration = Mathf.Max(0f, spawnIndicatorDuration);
            spawnIndicatorRadius = Mathf.Max(0f, spawnIndicatorRadius);
            spawnIndicatorLineWidth = Mathf.Max(0f, spawnIndicatorLineWidth);
            spawnIndicatorPulseScale = Mathf.Max(0f, spawnIndicatorPulseScale);
            targetDummyScale.x = Mathf.Max(0.1f, targetDummyScale.x);
            targetDummyScale.y = Mathf.Max(0.1f, targetDummyScale.y);
            targetDummyScale.z = Mathf.Max(0.1f, targetDummyScale.z);
            despawnDuration = Mathf.Max(0.05f, despawnDuration);
            respawnDelay = Mathf.Max(0f, respawnDelay);
            despawnDropDistance = Mathf.Max(0f, despawnDropDistance);
            respawnRadius = Mathf.Max(0f, respawnRadius);
        }

        private void Update()
        {
            if (!Application.isPlaying || !isRunActive)
            {
                return;
            }

            AdvanceCorruptionTracking(Time.deltaTime);
        }

        public void BeginRun()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || !spawnEnemies || isRunActive)
            {
                return;
            }

            if (activationRoutine != null)
            {
                StopCoroutine(activationRoutine);
                activationRoutine = null;
            }

            ResolveSpawnSurfaceCollider();
            player = player != null ? player : FindFirstObjectByType<PrototypePlayerController>();
            isRunActive = true;
            CurrentWave = 0;
            AliveEnemyCount = 0;
            TotalEnemiesKilled = 0;
            warnedNoSpawnPosition = false;
            queuedEnemies.Clear();
            waveBuildBuffer.Clear();
            recentSpawnPositions.Clear();
            ResetCorruptionTracking();

            waveRoutine = StartCoroutine(WaveRoutine());
            spawnRoutine = StartCoroutine(SpawnRoutine());
        }

        public void ArmRunStart()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || !spawnEnemies)
            {
                return;
            }

            StopRun();
            ResolveSpawnSurfaceCollider();
            player = player != null ? player : FindFirstObjectByType<PrototypePlayerController>();
            warnedNoSpawnPosition = false;

            if (startMode == PrototypeWaveStartMode.Immediate)
            {
                BeginRun();
            }
            else if (startMode == PrototypeWaveStartMode.OnPlayerEnteredSurface)
            {
                activationRoutine = StartCoroutine(WaitForPlayerOnSurfaceRoutine());
            }
        }

        public void StopRun()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            StopRunRoutines();

            foreach (var enemy in aliveEnemies)
            {
                if (enemy != null && enemy.TryGetComponent<PrototypeHealth>(out var health))
                {
                    health.Died -= HandleWaveEnemyDied;
                }
            }

            foreach (var actor in spawnedActors)
            {
                if (actor != null)
                {
                    Destroy(actor);
                }
            }

            spawnedActors.Clear();
            aliveEnemies.Clear();
            queuedEnemies.Clear();
            waveBuildBuffer.Clear();
            ClearSpawnIndicators();
            ClearLooseResourceDrops();
            recentSpawnPositions.Clear();
            AliveEnemyCount = 0;
            ResetCorruptionTracking();
            isRunActive = false;
        }

        public void CompleteRunAndKillRemainingEnemies()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            StopRunRoutines();

            queuedEnemies.Clear();
            waveBuildBuffer.Clear();
            ClearSpawnIndicators();
            recentSpawnPositions.Clear();
            ResetCorruptionTracking();
            isRunActive = false;

            var enemiesToKill = aliveEnemies.ToArray();
            foreach (var enemy in enemiesToKill)
            {
                if (enemy != null && enemy.TryGetComponent<PrototypeHealth>(out var health))
                {
                    health.Kill();
                }
            }

            AliveEnemyCount = aliveEnemies.Count;
        }

        private IEnumerator WaveRoutine()
        {
            while (isActiveAndEnabled)
            {
                yield return RunWaveCountdown();
                if (!isActiveAndEnabled || !isRunActive)
                {
                    yield break;
                }

                BeginNextWave();
                currentWaveCountdown = CurrentWaveInterval;
            }
        }

        private IEnumerator SpawnRoutine()
        {
            while (isActiveAndEnabled)
            {
                yield return WaitUntilPlayerCanSpawn();
                if (!isActiveAndEnabled || !isRunActive)
                {
                    yield break;
                }

                if (queuedEnemies.Count > 0 && AliveEnemyCount < maxAliveAtOnce)
                {
                    if (TryGetSpawnPosition(out var spawnPosition))
                    {
                        var queuedEnemy = queuedEnemies.Dequeue();
                        yield return SpawnWaveEnemyAfterIndicator(queuedEnemy.Wave, queuedEnemy.EnemyType, spawnPosition);
                        yield return WaitForPlayerReadyDelay(spawnInterval);
                    }
                    else
                    {
                        WarnNoSpawnPosition();
                        yield return WaitForPlayerReadyDelay(Mathf.Max(0.1f, spawnInterval));
                    }
                }
                else
                {
                    yield return null;
                }
            }
        }

        private void BeginNextWave()
        {
            CurrentWave++;
            waveBuildBuffer.Clear();

            var composition = GetConfiguredWaveComposition(CurrentWave);
            for (var i = 0; i < composition.ChaserCount; i++)
            {
                waveBuildBuffer.Add(PrototypeEnemyType.Chaser);
            }

            for (var i = 0; i < composition.SpitterCount; i++)
            {
                waveBuildBuffer.Add(PrototypeEnemyType.Spitter);
            }

            Shuffle(waveBuildBuffer);
            foreach (var enemyType in waveBuildBuffer)
            {
                queuedEnemies.Enqueue(new PrototypeQueuedEnemySpawn(CurrentWave, enemyType));
            }
        }

        private IEnumerator SpawnWaveEnemyAfterIndicator(int wave, PrototypeEnemyType enemyType, Vector3 position)
        {
            if (spawnIndicatorDuration <= 0f)
            {
                SpawnWaveEnemy(wave, enemyType, position);
                yield break;
            }

            var indicator = CreateSpawnIndicator(position, enemyType);
            var elapsed = 0f;
            while (elapsed < spawnIndicatorDuration)
            {
                if (!IsPlayerDead())
                {
                    elapsed += Time.deltaTime;
                }

                UpdateSpawnIndicator(indicator, Mathf.Clamp01(elapsed / spawnIndicatorDuration));
                yield return null;
            }

            DestroySpawnIndicator(indicator);
            if (isActiveAndEnabled && isRunActive)
            {
                SpawnWaveEnemy(wave, enemyType, position);
            }
        }

        private void SpawnWaveEnemy(int wave, PrototypeEnemyType enemyType, Vector3 position)
        {
            player = player != null ? player : FindFirstObjectByType<PrototypePlayerController>();

            var enemy = new GameObject($"Prototype Wave {wave} {enemyType}");
            enemy.SetActive(false);
            enemy.transform.position = position;

            enemy.AddComponent<Rigidbody>();
            ConfigureEnemyCollider(enemy, enemyType);
            var visualRoot = CreateEnemyVisual(enemy, enemyType);
            var health = enemy.AddComponent<PrototypeHealth>();
            enemy.AddComponent<PrototypeCombatTarget>();
            var controller = enemy.AddComponent<PrototypeEnemyController>();
            controller.Configure(enemyType, player, visualRoot);
            health.Died += HandleWaveEnemyDied;

            spawnedActors.Add(enemy);
            aliveEnemies.Add(controller);
            AliveEnemyCount++;
            enemy.SetActive(true);
        }

        private GameObject CreateSpawnIndicator(Vector3 position, PrototypeEnemyType enemyType)
        {
            var indicator = new GameObject($"Prototype {enemyType} Spawn Indicator");
            indicator.transform.position = position + Vector3.up * 0.06f;

            var ring = new GameObject("Ring");
            ring.transform.SetParent(indicator.transform, false);

            var line = ring.AddComponent<LineRenderer>();
            line.loop = true;
            line.useWorldSpace = false;
            line.positionCount = 48;
            line.widthMultiplier = spawnIndicatorLineWidth;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.material = CreateSpawnIndicatorMaterial();
            line.startColor = spawnIndicatorColor;
            line.endColor = spawnIndicatorColor;

            var radius = Mathf.Max(0.05f, spawnIndicatorRadius);
            for (var i = 0; i < line.positionCount; i++)
            {
                var angle = (Mathf.PI * 2f * i) / line.positionCount;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }

            spawnIndicators.Add(indicator);
            UpdateSpawnIndicator(indicator, 0f);
            return indicator;
        }

        private static Material CreateSpawnIndicatorMaterial()
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            return new Material(shader);
        }

        private void UpdateSpawnIndicator(GameObject indicator, float normalizedTime)
        {
            if (indicator == null)
            {
                return;
            }

            var eased = Mathf.SmoothStep(0f, 1f, normalizedTime);
            var pulse = 1f + Mathf.Sin((Time.time * 8f) + normalizedTime * Mathf.PI) * 0.08f;
            var scale = Mathf.Lerp(0.65f, 1f + spawnIndicatorPulseScale, eased) * pulse;
            indicator.transform.localScale = new Vector3(scale, 1f, scale);
            indicator.transform.Rotate(Vector3.up, 120f * Time.deltaTime, Space.World);

            var ring = indicator.GetComponentInChildren<LineRenderer>();
            if (ring != null)
            {
                var color = spawnIndicatorColor;
                color.a *= Mathf.Lerp(0.45f, 1f, Mathf.Sin(normalizedTime * Mathf.PI));
                ring.startColor = color;
                ring.endColor = color;
            }
        }

        private void DestroySpawnIndicator(GameObject indicator)
        {
            if (indicator == null)
            {
                return;
            }

            spawnIndicators.Remove(indicator);
            Destroy(indicator);
        }

        public static bool TryProjectToSurface(
            Collider surfaceCollider,
            Vector3 worldPoint,
            float probeHeight,
            float probeDepth,
            float groundOffset,
            out Vector3 surfacePoint)
        {
            surfacePoint = worldPoint;
            if (surfaceCollider == null)
            {
                return false;
            }

            var safeProbeHeight = Mathf.Max(0f, probeHeight);
            var safeProbeDepth = Mathf.Max(0f, probeDepth);
            var rayOrigin = worldPoint + Vector3.up * safeProbeHeight;
            var rayDistance = safeProbeHeight + safeProbeDepth;
            if (rayDistance <= 0f)
            {
                rayDistance = 0.01f;
            }

            if (!surfaceCollider.Raycast(new Ray(rayOrigin, Vector3.down), out var hit, rayDistance))
            {
                return false;
            }

            surfacePoint = hit.point + Vector3.up * Mathf.Max(0f, groundOffset);
            return true;
        }

        public static bool HasSurfaceFootprint(
            Collider surfaceCollider,
            Vector3 surfacePoint,
            float footprintRadius,
            int sampleCount,
            float probeHeight,
            float probeDepth,
            float maxHeightDelta)
        {
            if (surfaceCollider == null)
            {
                return false;
            }

            var safeRadius = Mathf.Max(0f, footprintRadius);
            if (safeRadius <= 0f)
            {
                return true;
            }

            var safeSampleCount = Mathf.Clamp(sampleCount, 3, 16);
            var safeHeightDelta = Mathf.Max(0f, maxHeightDelta);
            for (var i = 0; i < safeSampleCount; i++)
            {
                var angle = (Mathf.PI * 2f * i) / safeSampleCount;
                var offset = new Vector3(Mathf.Cos(angle) * safeRadius, 0f, Mathf.Sin(angle) * safeRadius);
                if (!TryProjectToSurface(surfaceCollider, surfacePoint + offset, probeHeight, probeDepth, 0f, out var projectedPoint))
                {
                    return false;
                }

                if (Mathf.Abs(projectedPoint.y - surfacePoint.y) > safeHeightDelta)
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryGetSpawnPosition(out Vector3 spawnPosition)
        {
            var center = GetSpawnCenter();
            var minRadius = Mathf.Min(minSpawnRadius, maxSpawnRadius);
            var maxRadius = Mathf.Max(minSpawnRadius, maxSpawnRadius);
            spawnPosition = center;

            ResolveSpawnSurfaceCollider();
            for (var i = 0; i < spawnAttempts; i++)
            {
                var angle = Random.Range(0f, Mathf.PI * 2f);
                var radius = Random.Range(minRadius, maxRadius);
                var candidate = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                candidate.y = center.y;

                if (!TryResolveSpawnCandidate(candidate, out var resolvedCandidate))
                {
                    continue;
                }

                if (HasEnemySpacing(resolvedCandidate))
                {
                    spawnPosition = resolvedCandidate;
                    RememberSpawnPosition(spawnPosition);
                    return true;
                }
            }

            return false;
        }

        private Vector3 GetSpawnCenter()
        {
            player = player != null ? player : FindFirstObjectByType<PrototypePlayerController>();
            return player != null ? player.transform.position : transform.position;
        }

        private bool TryResolveSpawnCandidate(Vector3 candidate, out Vector3 resolvedCandidate)
        {
            resolvedCandidate = candidate;
            if (spawnSurfaceCollider == null)
            {
                return HasEnemySpacing(candidate);
            }

            if (!TryProjectToSurface(
                    spawnSurfaceCollider,
                    candidate,
                    spawnSurfaceProbeHeight,
                    spawnSurfaceProbeDepth,
                    spawnGroundOffset,
                    out resolvedCandidate))
            {
                return false;
            }

            return HasSurfaceFootprint(
                spawnSurfaceCollider,
                resolvedCandidate,
                spawnFootprintRadius,
                spawnFootprintSamples,
                spawnSurfaceProbeHeight,
                spawnSurfaceProbeDepth,
                maxSurfaceHeightDelta + spawnGroundOffset);
        }

        private IEnumerator WaitForPlayerOnSurfaceRoutine()
        {
            while (isActiveAndEnabled && !isRunActive)
            {
                ResolveSpawnSurfaceCollider();
                if (!IsPlayerDead() && IsPlayerOnSpawnSurface())
                {
                    activationRoutine = null;
                    BeginRun();
                    yield break;
                }

                yield return null;
            }

            activationRoutine = null;
        }

        private IEnumerator RunWaveCountdown()
        {
            if (CurrentWave == 0 && currentWaveCountdown <= 0f)
            {
                currentWaveCountdown = Mathf.Max(0f, firstWaveDelay);
            }

            while (isActiveAndEnabled && isRunActive && currentWaveCountdown > 0f)
            {
                if (!IsPlayerDead())
                {
                    currentWaveCountdown = Mathf.Max(0f, currentWaveCountdown - Time.deltaTime);
                }

                yield return null;
            }
        }

        private bool IsPlayerOnSpawnSurface()
        {
            player = player != null ? player : FindFirstObjectByType<PrototypePlayerController>();
            if (player == null)
            {
                return false;
            }

            ResolveSpawnSurfaceCollider();
            if (spawnSurfaceCollider == null)
            {
                WarnMissingSpawnSurface();
                return false;
            }

            return TryProjectToSurface(
                spawnSurfaceCollider,
                player.transform.position,
                playerSurfaceProbeHeight,
                playerSurfaceProbeDepth,
                0f,
                out _);
        }

        private void ResolveSpawnSurfaceCollider()
        {
            if (spawnSurfaceCollider != null || !autoFindSpawnSurfaceByName || string.IsNullOrWhiteSpace(spawnSurfaceObjectName))
            {
                return;
            }

            var surfaceObject = GameObject.Find(spawnSurfaceObjectName);
            if (surfaceObject == null)
            {
                return;
            }

            spawnSurfaceCollider = surfaceObject.GetComponent<MeshCollider>();
        }

        private void WarnMissingSpawnSurface()
        {
            if (warnedMissingSpawnSurface)
            {
                return;
            }

            warnedMissingSpawnSurface = true;
            Debug.LogWarning(
                $"{nameof(PrototypeCombatSandbox)} on {name} is waiting for a spawn surface collider. Assign the Expedition Island MeshCollider or keep auto-find pointed at the island object.",
                this);
        }

        private void WarnNoSpawnPosition()
        {
            if (warnedNoSpawnPosition)
            {
                return;
            }

            warnedNoSpawnPosition = true;
            Debug.LogWarning(
                $"{nameof(PrototypeCombatSandbox)} on {name} could not find a valid island-bounded spawn point. Check spawn radii, footprint radius, and the assigned surface collider.",
                this);
        }

        private void RememberSpawnPosition(Vector3 spawnPosition)
        {
            recentSpawnPositions.Add(spawnPosition);
            while (recentSpawnPositions.Count > MaxRecentDebugPoints)
            {
                recentSpawnPositions.RemoveAt(0);
            }
        }

        private void StopRunRoutines()
        {
            if (activationRoutine != null)
            {
                StopCoroutine(activationRoutine);
                activationRoutine = null;
            }

            if (waveRoutine != null)
            {
                StopCoroutine(waveRoutine);
                waveRoutine = null;
            }

            if (spawnRoutine != null)
            {
                StopCoroutine(spawnRoutine);
                spawnRoutine = null;
            }
        }

        private void ResetCorruptionTracking()
        {
            CorruptionLevel = 0;
            corruptionMeterValue = 0f;
            currentWaveCountdown = 0f;
        }

        private void AdvanceCorruptionTracking(float deltaTime)
        {
            var previousCorruptionLevel = CorruptionLevel;
            CorruptionLevel = AdvanceCorruption(
                corruptionMeterValue,
                CorruptionLevel,
                deltaTime,
                corruptionMeterMax,
                secondsPerCorruptionLevel,
                out corruptionMeterValue);

            if (CorruptionLevel != previousCorruptionLevel && currentWaveCountdown > 0f)
            {
                currentWaveCountdown = Mathf.Min(currentWaveCountdown, CurrentWaveInterval);
            }
        }

        private void ClearSpawnIndicators()
        {
            foreach (var indicator in spawnIndicators)
            {
                if (indicator != null)
                {
                    Destroy(indicator);
                }
            }

            spawnIndicators.Clear();
        }

        private bool HasEnemySpacing(Vector3 candidate)
        {
            var minSqrDistance = minEnemySpacing * minEnemySpacing;
            for (var i = aliveEnemies.Count - 1; i >= 0; i--)
            {
                var enemy = aliveEnemies[i];
                if (enemy == null)
                {
                    aliveEnemies.RemoveAt(i);
                    continue;
                }

                var delta = enemy.transform.position - candidate;
                delta.y = 0f;
                if (delta.sqrMagnitude < minSqrDistance)
                {
                    return false;
                }
            }

            return true;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDebugGizmos)
            {
                return;
            }

            var center = Application.isPlaying ? GetSpawnCenter() : transform.position;
            Gizmos.color = new Color(1f, 0.82f, 0.18f, 0.35f);
            Gizmos.DrawWireSphere(center, minSpawnRadius);
            Gizmos.color = new Color(1f, 0.42f, 0.18f, 0.35f);
            Gizmos.DrawWireSphere(center, maxSpawnRadius);

            if (spawnSurfaceCollider != null)
            {
                Gizmos.color = new Color(0.25f, 0.8f, 1f, 0.45f);
                Gizmos.DrawWireCube(spawnSurfaceCollider.bounds.center, spawnSurfaceCollider.bounds.size);
            }

            Gizmos.color = new Color(0.35f, 1f, 0.35f, 0.85f);
            for (var i = 0; i < recentSpawnPositions.Count; i++)
            {
                Gizmos.DrawWireSphere(recentSpawnPositions[i] + Vector3.up * 0.05f, spawnFootprintRadius);
            }
        }

        private IEnumerator WaitUntilPlayerCanSpawn()
        {
            while (IsPlayerDead())
            {
                yield return null;
            }
        }

        private IEnumerator WaitForPlayerReadyDelay(float delay)
        {
            var elapsed = 0f;
            while (elapsed < delay)
            {
                if (!IsPlayerDead())
                {
                    elapsed += Time.deltaTime;
                }

                yield return null;
            }
        }

        private bool IsPlayerDead()
        {
            player = player != null ? player : FindFirstObjectByType<PrototypePlayerController>();
            return player != null && player.IsDead;
        }

        private void HandleWaveEnemyDied(PrototypeHealth deadHealth)
        {
            deadHealth.Died -= HandleWaveEnemyDied;
            TotalEnemiesKilled++;
            AliveEnemyCount = Mathf.Max(0, AliveEnemyCount - 1);

            var controller = deadHealth.GetComponent<PrototypeEnemyController>();
            if (controller != null)
            {
                aliveEnemies.Remove(controller);
            }
        }

        private void ClearLooseResourceDrops()
        {
            if (!clearLooseResourceDropsOnStop)
            {
                return;
            }

            var drops = FindObjectsByType<PrototypeResourceDrop>(FindObjectsSortMode.None);
            foreach (var drop in drops)
            {
                if (drop != null)
                {
                    Destroy(drop.gameObject);
                }
            }
        }

        private void Shuffle(List<PrototypeEnemyType> enemies)
        {
            for (var i = enemies.Count - 1; i > 0; i--)
            {
                var swapIndex = Random.Range(0, i + 1);
                (enemies[i], enemies[swapIndex]) = (enemies[swapIndex], enemies[i]);
            }
        }

        private void ConfigureEnemyCollider(GameObject enemy, PrototypeEnemyType enemyType)
        {
            var collider = enemy.AddComponent<CapsuleCollider>();
            if (enemyType == PrototypeEnemyType.Spitter)
            {
                collider.radius = 0.46f;
                collider.height = MinimumEnemyCombatHeight;
                collider.center = new Vector3(0f, MinimumEnemyCombatHeight * 0.5f, 0f);
                return;
            }

            collider.radius = 0.44f;
            collider.height = MinimumEnemyCombatHeight;
            collider.center = new Vector3(0f, MinimumEnemyCombatHeight * 0.5f, 0f);
        }

        private Transform CreateEnemyVisual(GameObject enemy, PrototypeEnemyType enemyType)
        {
            var root = new GameObject("Visual").transform;
            root.SetParent(enemy.transform, false);

            if (enemyType == PrototypeEnemyType.Spitter)
            {
                CreatePrimitiveVisual(root, "Body", PrimitiveType.Sphere, new Vector3(0f, 0.55f, 0f), new Vector3(0.82f, 0.82f, 0.82f), new Color(0.22f, 0.76f, 0.38f, 1f));
                var mouth = CreatePrimitiveVisual(root, "Spit Mouth", PrimitiveType.Cylinder, new Vector3(0f, 0.62f, 0.48f), new Vector3(0.18f, 0.24f, 0.18f), new Color(0.62f, 1f, 0.32f, 1f));
                mouth.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                CreatePrimitiveVisual(root, "Core", PrimitiveType.Sphere, new Vector3(0f, 0.78f, 0.03f), new Vector3(0.32f, 0.22f, 0.32f), new Color(0.12f, 0.28f, 0.18f, 1f));
                return root;
            }

            CreatePrimitiveVisual(root, "Body", PrimitiveType.Capsule, new Vector3(0f, 0.75f, 0f), new Vector3(0.72f, 0.72f, 0.72f), new Color(0.92f, 0.18f, 0.12f, 1f));
            CreatePrimitiveVisual(root, "Chest", PrimitiveType.Cube, new Vector3(0f, 0.72f, 0.38f), new Vector3(0.42f, 0.26f, 0.12f), new Color(1f, 0.56f, 0.16f, 1f));
            CreatePrimitiveVisual(root, "Crown", PrimitiveType.Cube, new Vector3(0f, 1.36f, 0f), new Vector3(0.38f, 0.16f, 0.38f), new Color(0.22f, 0.05f, 0.04f, 1f));
            return root;
        }

        private GameObject CreatePrimitiveVisual(Transform parent, string name, PrimitiveType primitiveType, Vector3 localPosition, Vector3 localScale, Color color)
        {
            var visual = GameObject.CreatePrimitive(primitiveType);
            visual.name = name;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localScale = localScale;

            if (visual.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.material.color = color;
            }

            var visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(visualCollider);
                }
                else
                {
                    DestroyImmediate(visualCollider);
                }
            }

            return visual;
        }

        private void SpawnDummy(int index)
        {
            var dummy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dummy.name = $"Prototype Target Dummy {index + 1}";
            dummy.transform.position = targetDummyPositions[index];
            dummy.transform.localScale = targetDummyScale;

            if (dummy.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.material.color = targetDummyColor;
            }

            var health = dummy.AddComponent<PrototypeHealth>();
            PrototypeFloatingHealthBar.EnsureFor(health, PrototypeFloatingHealthBar.Preset.Enemy);
            dummy.AddComponent<PrototypeCombatTarget>();
            var respawner = dummy.AddComponent<PrototypeRespawningCombatTarget>();
            respawner.Configure(
                health,
                targetDummyPositions[index],
                targetDummyScale,
                despawnDuration,
                respawnDelay,
                despawnDropDistance,
                respawnRadius);

            spawnedActors.Add(dummy);
        }
    }

    internal sealed class PrototypeRespawningCombatTarget : MonoBehaviour
    {
        private PrototypeHealth health;
        private Vector3 spawnAnchor;
        private Vector3 originalScale;
        private float despawnDuration = 1.15f;
        private float respawnDelay = 0.35f;
        private float despawnDropDistance = 1.25f;
        private float respawnRadius = 1.75f;
        private Coroutine respawnRoutine;

        public void Configure(
            PrototypeHealth newHealth,
            Vector3 newSpawnAnchor,
            Vector3 newOriginalScale,
            float newDespawnDuration,
            float newRespawnDelay,
            float newDespawnDropDistance,
            float newRespawnRadius)
        {
            health = newHealth;
            spawnAnchor = newSpawnAnchor;
            originalScale = newOriginalScale;
            despawnDuration = Mathf.Max(0.05f, newDespawnDuration);
            respawnDelay = Mathf.Max(0f, newRespawnDelay);
            despawnDropDistance = Mathf.Max(0f, newDespawnDropDistance);
            respawnRadius = Mathf.Max(0f, newRespawnRadius);
        }

        private void Awake()
        {
            health = health != null ? health : GetComponent<PrototypeHealth>();
            spawnAnchor = transform.position;
            originalScale = transform.localScale;
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.Died += HandleDied;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Died -= HandleDied;
            }

            if (respawnRoutine != null)
            {
                StopCoroutine(respawnRoutine);
                respawnRoutine = null;
            }
        }

        private void HandleDied(PrototypeHealth deadHealth)
        {
            if (respawnRoutine != null)
            {
                StopCoroutine(respawnRoutine);
            }

            respawnRoutine = StartCoroutine(RespawnRoutine());
        }

        private IEnumerator RespawnRoutine()
        {
            SetCollidersEnabled(false);

            var startPosition = transform.position;
            var endPosition = startPosition - Vector3.up * despawnDropDistance;
            var startScale = transform.localScale;
            var elapsed = 0f;

            while (elapsed < despawnDuration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / despawnDuration);
                var eased = Mathf.SmoothStep(0f, 1f, t);
                transform.position = Vector3.Lerp(startPosition, endPosition, eased);
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, eased);
                yield return null;
            }

            transform.position = endPosition;
            transform.localScale = Vector3.zero;

            if (respawnDelay > 0f)
            {
                yield return new WaitForSeconds(respawnDelay);
            }

            transform.position = GetRespawnPosition();
            transform.localScale = originalScale;
            health.ResetHealth();
            SetCollidersEnabled(true);
            respawnRoutine = null;
        }

        private Vector3 GetRespawnPosition()
        {
            if (respawnRadius <= 0f)
            {
                return spawnAnchor;
            }

            var offset = Random.insideUnitCircle * respawnRadius;
            return spawnAnchor + new Vector3(offset.x, 0f, offset.y);
        }

        private void SetCollidersEnabled(bool enabled)
        {
            var colliders = GetComponentsInChildren<Collider>();
            foreach (var targetCollider in colliders)
            {
                targetCollider.enabled = enabled;
            }
        }
    }
}
