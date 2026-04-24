using System.Collections;
using UnityEngine;

namespace Islands.Prototype
{
    public enum PrototypeEnemyType
    {
        Chaser,
        Spitter
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(PrototypeHealth))]
    public sealed class PrototypeEnemyController : MonoBehaviour
    {
        private const float AttackSquashDuration = 0.22f;

        [SerializeField]
        private PrototypeEnemyType enemyType;

        [SerializeField]
        private PrototypePlayerController player;

        [SerializeField]
        private Transform visualRoot;

        [Header("Vitals")]
        [SerializeField]
        [Min(1f)]
        private float maxHealth = 4f;

        [SerializeField]
        [Min(0f)]
        private float invulnerabilityDuration = 0.18f;

        [Header("Movement")]
        [SerializeField]
        [Min(0f)]
        private float moveSpeed = 3.2f;

        [SerializeField]
        [Min(0f)]
        private float strafeSpeed = 1.15f;

        [SerializeField]
        [Min(0f)]
        private float turnDegreesPerSecond = 720f;

        [SerializeField]
        [Min(0f)]
        private float knockbackStrength = 4.5f;

        [SerializeField]
        [Min(0f)]
        private float knockbackRecovery = 10f;

        [Header("Contact Attack")]
        [SerializeField]
        [Min(0f)]
        private float contactDamage = 1f;

        [SerializeField]
        [Min(0f)]
        private float contactRange = 0.9f;

        [SerializeField]
        [Min(0.05f)]
        private float contactCooldown = 0.85f;

        [Header("Spitter")]
        [SerializeField]
        [Min(0f)]
        private float preferredRange = 5.6f;

        [SerializeField]
        [Min(0f)]
        private float rangeLeash = 1.2f;

        [SerializeField]
        [Min(0f)]
        private float maxRetreatDistance = 2.4f;

        [SerializeField]
        [Min(0.05f)]
        private float fireCooldown = 1.55f;

        [SerializeField]
        [Min(0f)]
        private float fireRange = 8.5f;

        [SerializeField]
        [Min(0f)]
        private float projectileDamage = 1f;

        [SerializeField]
        [Min(0f)]
        private float projectileSpeed = 4.6f;

        [SerializeField]
        [Min(0.05f)]
        private float projectileLifetime = 4f;

        [SerializeField]
        [Min(0.05f)]
        private float projectileRadius = 0.18f;

        [SerializeField]
        private Color projectileColor = new Color(0.45f, 1f, 0.35f, 1f);

        [Header("Juice")]
        [SerializeField]
        [Min(0.01f)]
        private float spawnRiseDuration = 0.42f;

        [SerializeField]
        [Min(0f)]
        private float spawnRiseDistance = 0.8f;

        [SerializeField]
        [Range(0f, 0.5f)]
        private float movementSquash = 0.12f;

        [SerializeField]
        [Range(0f, 0.6f)]
        private float attackSquash = 0.28f;

        [SerializeField]
        private Color spawnBurstColor = new Color(1f, 0.42f, 0.22f, 1f);

        [SerializeField]
        private Color deathBurstColor = new Color(1f, 0.18f, 0.08f, 1f);

        [Header("Drops")]
        [SerializeField]
        [Min(0)]
        private int resourceDropCount = 2;

        [SerializeField]
        [Min(1)]
        private int resourceDropAmount = 1;

        [SerializeField]
        [Min(0f)]
        private float resourceDropSpread = 0.65f;

        [SerializeField]
        private Color resourceDropColor = new Color(1f, 0.82f, 0.18f, 1f);

        private Rigidbody body;
        private PrototypeHealth health;
        private PrototypeHealth playerHealth;
        private Vector3 visualBaseScale = Vector3.one;
        private Vector3 visualBaseLocalPosition;
        private Vector3 knockbackVelocity;
        private Vector3 lastMoveVelocity;
        private float nextContactTime;
        private float nextFireTime;
        private float movementPulse;
        private float attackSquashTimer;
        private Vector3 retreatAnchor;
        private bool hasRetreatAnchor;
        private bool spawnedIn;
        private bool dying;
        private Coroutine spawnRoutine;
        private Coroutine deathRoutine;

        public PrototypeEnemyType EnemyType => enemyType;

        public void Configure(PrototypeEnemyType newEnemyType, PrototypePlayerController newPlayer, Transform newVisualRoot)
        {
            enemyType = newEnemyType;
            player = newPlayer;
            visualRoot = newVisualRoot != null ? newVisualRoot : transform;

            ApplyPreset(enemyType);
            EnsureSetup();
            CaptureVisualDefaults();
            health.Configure(maxHealth, invulnerabilityDuration);
            PrototypeFloatingHealthBar.EnsureFor(health, PrototypeFloatingHealthBar.Preset.Enemy);
        }

        private void Awake()
        {
            EnsureSetup();
            CaptureVisualDefaults();
            PrototypeFloatingHealthBar.EnsureFor(health, PrototypeFloatingHealthBar.Preset.Enemy);
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.Damaged += HandleDamaged;
                health.Died += HandleDied;
            }

            dying = false;
            knockbackVelocity = Vector3.zero;
            lastMoveVelocity = Vector3.zero;
            hasRetreatAnchor = false;
            nextContactTime = Time.time + 0.25f;
            nextFireTime = Time.time + Random.Range(0.55f, 0.9f);
            BeginSpawn();
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= HandleDamaged;
                health.Died -= HandleDied;
            }

            if (spawnRoutine != null)
            {
                StopCoroutine(spawnRoutine);
                spawnRoutine = null;
            }

            if (deathRoutine != null)
            {
                StopCoroutine(deathRoutine);
                deathRoutine = null;
            }
        }

        private void Reset()
        {
            EnsureSetup();
            ApplyPreset(enemyType);
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            invulnerabilityDuration = Mathf.Max(0f, invulnerabilityDuration);
            moveSpeed = Mathf.Max(0f, moveSpeed);
            strafeSpeed = Mathf.Max(0f, strafeSpeed);
            turnDegreesPerSecond = Mathf.Max(0f, turnDegreesPerSecond);
            knockbackStrength = Mathf.Max(0f, knockbackStrength);
            knockbackRecovery = Mathf.Max(0f, knockbackRecovery);
            contactDamage = Mathf.Max(0f, contactDamage);
            contactRange = Mathf.Max(0f, contactRange);
            contactCooldown = Mathf.Max(0.05f, contactCooldown);
            preferredRange = Mathf.Max(0f, preferredRange);
            rangeLeash = Mathf.Max(0f, rangeLeash);
            maxRetreatDistance = Mathf.Max(0f, maxRetreatDistance);
            fireCooldown = Mathf.Max(0.05f, fireCooldown);
            fireRange = Mathf.Max(0f, fireRange);
            projectileDamage = Mathf.Max(0f, projectileDamage);
            projectileSpeed = Mathf.Max(0f, projectileSpeed);
            projectileLifetime = Mathf.Max(0.05f, projectileLifetime);
            projectileRadius = Mathf.Max(0.05f, projectileRadius);
            spawnRiseDuration = Mathf.Max(0.01f, spawnRiseDuration);
            spawnRiseDistance = Mathf.Max(0f, spawnRiseDistance);
            resourceDropCount = Mathf.Max(0, resourceDropCount);
            resourceDropAmount = Mathf.Max(1, resourceDropAmount);
            resourceDropSpread = Mathf.Max(0f, resourceDropSpread);
        }

        private void FixedUpdate()
        {
            if (!spawnedIn || dying || health == null || health.IsDead || !TryGetLivingPlayer(out var targetDirection, out var distance))
            {
                lastMoveVelocity = Vector3.zero;
                return;
            }

            var desiredVelocity = enemyType == PrototypeEnemyType.Chaser
                ? GetChaserVelocity(targetDirection)
                : GetSpitterVelocity(targetDirection, distance);

            knockbackVelocity = Vector3.MoveTowards(knockbackVelocity, Vector3.zero, knockbackRecovery * Time.fixedDeltaTime);
            var finalVelocity = desiredVelocity + knockbackVelocity;
            var targetPosition = body.position + finalVelocity * Time.fixedDeltaTime;
            targetPosition.y = body.position.y;
            body.MovePosition(targetPosition);

            var lookDirection = enemyType == PrototypeEnemyType.Spitter || desiredVelocity.sqrMagnitude <= 0.0001f
                ? targetDirection
                : desiredVelocity.normalized;
            RotateToward(lookDirection);
            lastMoveVelocity = desiredVelocity;
        }

        private void Update()
        {
            if (!spawnedIn || dying || health == null || health.IsDead)
            {
                return;
            }

            if (TryGetLivingPlayer(out var targetDirection, out var distance))
            {
                if (enemyType == PrototypeEnemyType.Chaser)
                {
                    TryContactAttack(targetDirection, distance);
                }
                else
                {
                    TryFire(targetDirection, distance);
                }
            }

            UpdateSquash();
        }

        private void EnsureSetup()
        {
            body = body != null ? body : GetComponent<Rigidbody>();
            health = health != null ? health : GetComponent<PrototypeHealth>();
            visualRoot = visualRoot != null ? visualRoot : transform;

            if (body == null)
            {
                return;
            }

            body.useGravity = false;
            body.isKinematic = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.FreezeRotation;
        }

        private void CaptureVisualDefaults()
        {
            if (visualRoot == null)
            {
                visualRoot = transform;
            }

            visualBaseScale = visualRoot.localScale;
            visualBaseLocalPosition = visualRoot.localPosition;
        }

        private void ApplyPreset(PrototypeEnemyType type)
        {
            resourceDropColor = new Color(1f, 0.82f, 0.18f, 1f);

            if (type == PrototypeEnemyType.Spitter)
            {
                maxHealth = 2f;
                invulnerabilityDuration = 0.16f;
                moveSpeed = 2.25f;
                strafeSpeed = 1.25f;
                knockbackStrength = 4.2f;
                contactDamage = 0f;
                preferredRange = 5.6f;
                rangeLeash = 1.15f;
                maxRetreatDistance = 2.4f;
                fireCooldown = 1.55f;
                fireRange = 8.5f;
                projectileDamage = 1f;
                projectileSpeed = 4.6f;
                projectileLifetime = 4f;
                projectileRadius = 0.18f;
                projectileColor = new Color(0.45f, 1f, 0.35f, 1f);
                spawnBurstColor = new Color(0.45f, 1f, 0.35f, 1f);
                deathBurstColor = new Color(0.56f, 1f, 0.28f, 1f);
                resourceDropCount = 1;
                return;
            }

            maxHealth = 4f;
            invulnerabilityDuration = 0.18f;
            moveSpeed = 3.25f;
            strafeSpeed = 0f;
            knockbackStrength = 4.8f;
            contactDamage = 1f;
            contactRange = 0.95f;
            contactCooldown = 0.85f;
            preferredRange = 0f;
            rangeLeash = 0f;
            maxRetreatDistance = 0f;
            fireCooldown = 1.55f;
            fireRange = 0f;
            projectileDamage = 0f;
            projectileSpeed = 0f;
            projectileLifetime = 1f;
            projectileRadius = 0.16f;
            projectileColor = new Color(1f, 0.42f, 0.2f, 1f);
            spawnBurstColor = new Color(1f, 0.42f, 0.22f, 1f);
            deathBurstColor = new Color(1f, 0.18f, 0.08f, 1f);
            resourceDropCount = 2;
        }

        private Vector3 GetChaserVelocity(Vector3 targetDirection)
        {
            return targetDirection * moveSpeed;
        }

        private Vector3 GetSpitterVelocity(Vector3 targetDirection, float distance)
        {
            var closeRange = Mathf.Max(0f, preferredRange - rangeLeash);
            var farRange = preferredRange + rangeLeash;

            if (distance < closeRange)
            {
                if (!hasRetreatAnchor)
                {
                    retreatAnchor = transform.position;
                    hasRetreatAnchor = true;
                }

                var retreated = transform.position - retreatAnchor;
                retreated.y = 0f;
                if (retreated.magnitude < maxRetreatDistance)
                {
                    return -targetDirection * moveSpeed;
                }
            }

            if (distance > farRange)
            {
                hasRetreatAnchor = false;
                return targetDirection * moveSpeed;
            }

            if (distance >= closeRange)
            {
                hasRetreatAnchor = false;
            }

            var strafeDirection = Vector3.Cross(Vector3.up, targetDirection).normalized;
            var strafeSign = Mathf.Sin(Time.time * 1.35f) >= 0f ? 1f : -1f;
            return strafeDirection * (strafeSpeed * strafeSign);
        }

        private void TryContactAttack(Vector3 targetDirection, float distance)
        {
            if (contactDamage <= 0f || distance > contactRange || Time.time < nextContactTime || playerHealth == null)
            {
                return;
            }

            nextContactTime = Time.time + contactCooldown;
            var hitPoint = playerHealth.transform.position - targetDirection * 0.45f + Vector3.up * 0.65f;
            playerHealth.ApplyDamage(new DamageInfo(contactDamage, gameObject, hitPoint, targetDirection));
            TriggerAttackSquash();
            PrototypeWeaponFeedback.SpawnBurst(hitPoint, spawnBurstColor, 0.55f, 8);
            PrototypeWeaponFeedback.PlaySfx(PrototypeWeaponSfx.MeleeHit, hitPoint, 0.22f);
        }

        private void TryFire(Vector3 targetDirection, float distance)
        {
            if (projectileDamage <= 0f || projectileSpeed <= 0f || distance > fireRange || Time.time < nextFireTime || playerHealth == null)
            {
                return;
            }

            nextFireTime = Time.time + fireCooldown;
            var spawnPosition = transform.position + Vector3.up * 0.75f + targetDirection * 0.58f;
            var projectile = CreateSpitProjectile(spawnPosition, targetDirection);
            projectile.RestrictDamageTo(playerHealth);
            projectile.Launch(targetDirection * projectileSpeed, projectileDamage, projectileLifetime, gameObject);

            TriggerAttackSquash();
            PrototypeWeaponFeedback.SpawnBurst(spawnPosition, projectileColor, 0.6f, 10);
            PrototypeWeaponFeedback.PlaySfx(PrototypeWeaponSfx.BoltFire, spawnPosition, 0.24f);
        }

        private PrototypeProjectile CreateSpitProjectile(Vector3 spawnPosition, Vector3 targetDirection)
        {
            var projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectileObject.name = "Prototype Spit Projectile";
            projectileObject.transform.SetPositionAndRotation(spawnPosition, Quaternion.LookRotation(targetDirection, Vector3.up));
            projectileObject.transform.localScale = Vector3.one * (projectileRadius * 2f);

            if (projectileObject.TryGetComponent<Renderer>(out var projectileRenderer))
            {
                projectileRenderer.material.color = projectileColor;
            }

            var projectileCollider = projectileObject.GetComponent<Collider>();
            if (projectileCollider != null)
            {
                projectileCollider.isTrigger = true;
            }

            if (projectileObject.GetComponent<Rigidbody>() == null)
            {
                projectileObject.AddComponent<Rigidbody>();
            }

            var projectile = projectileObject.AddComponent<PrototypeProjectile>();
            projectile.ConfigureFeedback(projectileColor, projectileRadius * 0.8f, 0.24f);
            return projectile;
        }

        private bool TryGetLivingPlayer(out Vector3 targetDirection, out float distance)
        {
            if (player == null)
            {
                player = FindFirstObjectByType<PrototypePlayerController>();
            }

            playerHealth = playerHealth != null ? playerHealth : player != null ? player.GetComponent<PrototypeHealth>() : null;

            if (player == null || playerHealth == null || playerHealth.IsDead)
            {
                targetDirection = transform.forward.ProjectedOnPlane();
                distance = 0f;
                return false;
            }

            var toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            distance = toPlayer.magnitude;
            targetDirection = distance > 0.0001f ? toPlayer / distance : transform.forward.ProjectedOnPlane();
            if (targetDirection.sqrMagnitude <= 0.0001f)
            {
                targetDirection = Vector3.forward;
            }

            return true;
        }

        private void RotateToward(Vector3 direction)
        {
            direction = direction.ProjectedOnPlane();
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            body.MoveRotation(Quaternion.RotateTowards(body.rotation, targetRotation, turnDegreesPerSecond * Time.fixedDeltaTime));
        }

        private void UpdateSquash()
        {
            if (visualRoot == null)
            {
                return;
            }

            movementPulse += lastMoveVelocity.magnitude * Time.deltaTime * 4.5f;
            attackSquashTimer = Mathf.Max(0f, attackSquashTimer - Time.deltaTime);

            var moveAmount = Mathf.Clamp01(lastMoveVelocity.magnitude / Mathf.Max(0.01f, moveSpeed));
            var moveWave = Mathf.Sin(movementPulse) * movementSquash * moveAmount;
            var attackPulse = attackSquashTimer > 0f
                ? Mathf.Sin((attackSquashTimer / AttackSquashDuration) * Mathf.PI) * attackSquash
                : 0f;

            var horizontalScale = Mathf.Max(0.1f, 1f - moveWave * 0.35f + attackPulse * 0.6f);
            var verticalScale = Mathf.Max(0.1f, 1f + moveWave * 0.5f - attackPulse * 0.35f);
            visualRoot.localScale = new Vector3(
                visualBaseScale.x * horizontalScale,
                visualBaseScale.y * verticalScale,
                visualBaseScale.z * horizontalScale);
            visualRoot.localPosition = visualBaseLocalPosition;
        }

        private void TriggerAttackSquash()
        {
            attackSquashTimer = AttackSquashDuration;
        }

        private void BeginSpawn()
        {
            if (spawnRoutine != null)
            {
                StopCoroutine(spawnRoutine);
            }

            spawnedIn = false;
            SetCollidersEnabled(false);
            spawnRoutine = StartCoroutine(SpawnRoutine());
        }

        private IEnumerator SpawnRoutine()
        {
            CaptureVisualDefaults();
            var startLocalPosition = visualBaseLocalPosition - Vector3.up * spawnRiseDistance;
            var elapsed = 0f;

            while (elapsed < spawnRiseDuration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / spawnRiseDuration);
                var eased = Mathf.SmoothStep(0f, 1f, t);
                var pop = Mathf.Sin(t * Mathf.PI) * 0.22f;

                visualRoot.localPosition = Vector3.Lerp(startLocalPosition, visualBaseLocalPosition, eased);
                visualRoot.localScale = new Vector3(
                    visualBaseScale.x * Mathf.Lerp(0.35f, 1f + pop, eased),
                    visualBaseScale.y * Mathf.Lerp(0.08f, 1f - pop * 0.2f, eased),
                    visualBaseScale.z * Mathf.Lerp(0.35f, 1f + pop, eased));
                yield return null;
            }

            visualRoot.localPosition = visualBaseLocalPosition;
            visualRoot.localScale = visualBaseScale;
            spawnedIn = true;
            SetCollidersEnabled(true);
            PrototypeWeaponFeedback.SpawnBurst(transform.position + Vector3.up * 0.35f, spawnBurstColor, 0.65f, 12);
            spawnRoutine = null;
        }

        private void HandleDamaged(PrototypeHealth damagedHealth, DamageInfo damageInfo)
        {
            if (dying)
            {
                return;
            }

            var direction = damageInfo.HitDirection;
            if (direction.sqrMagnitude <= 0.0001f && damageInfo.Source != null)
            {
                direction = (transform.position - damageInfo.Source.transform.position).ProjectedOnPlane();
            }

            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = -transform.forward.ProjectedOnPlane();
            }

            knockbackVelocity = direction.normalized * knockbackStrength;
        }

        private void HandleDied(PrototypeHealth deadHealth)
        {
            if (dying)
            {
                return;
            }

            dying = true;
            spawnedIn = false;
            if (spawnRoutine != null)
            {
                StopCoroutine(spawnRoutine);
                spawnRoutine = null;
            }

            SetCollidersEnabled(false);
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;

            var burstPosition = transform.position + Vector3.up * 0.65f;
            PrototypeWeaponFeedback.SpawnBurst(burstPosition, deathBurstColor, 1.1f, 18);
            PrototypeWeaponFeedback.PlaySfx(PrototypeWeaponSfx.EnemyDeath, burstPosition, 0.32f);
            SpawnResourceDrops();

            if (deathRoutine != null)
            {
                StopCoroutine(deathRoutine);
            }

            deathRoutine = StartCoroutine(DeathRoutine());
        }

        private IEnumerator DeathRoutine()
        {
            var startScale = visualRoot.localScale;
            var startPosition = visualRoot.localPosition;
            const float duration = 0.28f;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = Mathf.SmoothStep(0f, 1f, t);
                visualRoot.localScale = Vector3.Lerp(startScale, Vector3.zero, eased);
                visualRoot.localPosition = Vector3.Lerp(startPosition, startPosition - Vector3.up * 0.28f, eased);
                yield return null;
            }

            Destroy(gameObject);
        }

        private void SpawnResourceDrops()
        {
            for (var i = 0; i < resourceDropCount; i++)
            {
                var offset = Random.insideUnitCircle * resourceDropSpread;
                var drop = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                drop.name = "Prototype Currency Drop";
                drop.transform.position = transform.position + new Vector3(offset.x, 0.16f, offset.y);
                drop.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
                drop.transform.localScale = new Vector3(0.22f, 0.045f, 0.22f);

                if (drop.TryGetComponent<Renderer>(out var renderer))
                {
                    renderer.material.color = resourceDropColor;
                }

                var pickup = drop.AddComponent<PrototypeResourceDrop>();
                pickup.Configure(resourceDropAmount, resourceDropColor);
            }
        }

        private void SetCollidersEnabled(bool enabled)
        {
            var colliders = GetComponentsInChildren<Collider>(true);
            foreach (var targetCollider in colliders)
            {
                targetCollider.enabled = enabled;
            }
        }
    }
}
