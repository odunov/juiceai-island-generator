using UnityEngine;

namespace Islands.Prototype
{
    [DisallowMultipleComponent]
    public sealed class PrototypeFloatingBoltWeapon : MonoBehaviour
    {
        private const float FirePulseDuration = 0.18f;

        private PrototypeFloatingWeaponLoadout loadout;
        private PrototypeFloatingWeaponLoadout.BoltWeaponSettings settings;
        private readonly PrototypeFloatingWeaponMotor motor = new PrototypeFloatingWeaponMotor();
        private PrototypeProjectile projectilePrefab;
        private Transform visualRoot;
        private Vector3 baseVisualScale = Vector3.one;
        private Vector3 aimDirection = Vector3.forward;
        private float recoilVelocity;
        private float recoilOffset;
        private float nextFireTime;
        private float pulseTimer;

        public void Configure(
            PrototypeFloatingWeaponLoadout newLoadout,
            PrototypeFloatingWeaponLoadout.BoltWeaponSettings newSettings,
            PrototypeProjectile newProjectilePrefab,
            GameObject visualPrefab)
        {
            loadout = newLoadout;
            settings = newSettings;
            projectilePrefab = newProjectilePrefab;
            settings.Validate();
            motor.Configure(
                loadout.OwnerTransform,
                settings.idleOffset,
                settings.followSmoothTime,
                settings.followMaxSpeed,
                settings.bobAmplitude,
                settings.bobFrequency,
                1.25f);
            CreateVisual(visualPrefab);
            transform.position = motor.GetIdlePosition();
        }

        public void SnapToOwner()
        {
            if (loadout == null || settings == null)
            {
                return;
            }

            recoilVelocity = 0f;
            recoilOffset = 0f;
            pulseTimer = 0f;
            aimDirection = motor.OwnerForward;
            motor.SnapToIdle(transform);
            transform.rotation = Quaternion.LookRotation(aimDirection, Vector3.up);

            if (visualRoot != null)
            {
                visualRoot.localScale = baseVisualScale;
            }
        }

        private void Update()
        {
            if (loadout == null || settings == null)
            {
                return;
            }

            var target = loadout.FindNearestTarget(loadout.OwnerTransform.position, settings.attackRange);
            UpdateAim(target);
            UpdateMotion();

            if (loadout.CanAttack && target != null && Time.time >= nextFireTime)
            {
                Fire(target);
            }
        }

        private void UpdateAim(PrototypeCombatTarget target)
        {
            var desiredDirection = target != null
                ? (target.AimPosition - transform.position).ProjectedOnPlane()
                : loadout.OwnerTransform.forward.ProjectedOnPlane();

            if (desiredDirection.sqrMagnitude <= 0.0001f)
            {
                desiredDirection = aimDirection.sqrMagnitude > 0.0001f ? aimDirection : Vector3.forward;
            }

            aimDirection = Vector3.Slerp(aimDirection, desiredDirection.normalized, 1f - Mathf.Exp(-18f * Time.deltaTime)).ProjectedOnPlane();
            if (aimDirection.sqrMagnitude <= 0.0001f)
            {
                aimDirection = Vector3.forward;
            }

            transform.rotation = Quaternion.LookRotation(aimDirection, Vector3.up);
        }

        private void UpdateMotion()
        {
            recoilOffset = Mathf.SmoothDamp(recoilOffset, 0f, ref recoilVelocity, settings.recoilRecoverTime);
            motor.SmoothFollow(transform, -aimDirection * recoilOffset);

            if (visualRoot == null)
            {
                return;
            }

            pulseTimer = Mathf.Max(0f, pulseTimer - Time.deltaTime);
            var pulse = pulseTimer > 0f
                ? 1f + Mathf.Sin((pulseTimer / FirePulseDuration) * Mathf.PI) * settings.attackScalePulse
                : 1f;
            visualRoot.localScale = baseVisualScale * pulse;
        }

        private void Fire(PrototypeCombatTarget target)
        {
            var fireDirection = aimDirection.ProjectedOnPlane();
            if (fireDirection.sqrMagnitude <= 0.0001f)
            {
                fireDirection = Vector3.forward;
            }

            var spawnPosition = transform.position + fireDirection * 0.55f;
            var projectile = CreateProjectile(spawnPosition, Quaternion.LookRotation(fireDirection, Vector3.up));
            if (projectile == null)
            {
                return;
            }

            projectile.ConfigureFeedback(settings.boltColor, settings.projectileRadius * 0.8f, 0.18f);
            projectile.Launch(fireDirection * settings.projectileSpeed, settings.damage, settings.projectileLifetime, loadout.Owner);

            recoilOffset = settings.recoilDistance;
            pulseTimer = FirePulseDuration;
            nextFireTime = Time.time + settings.cooldown;

            PrototypeWeaponFeedback.SpawnBurst(spawnPosition, settings.boltColor, 0.7f, 10);
            PrototypeWeaponFeedback.PlaySfx(PrototypeWeaponSfx.BoltFire, spawnPosition, 0.28f);
        }

        private PrototypeProjectile CreateProjectile(Vector3 spawnPosition, Quaternion spawnRotation)
        {
            if (projectilePrefab != null)
            {
                var projectile = Instantiate(projectilePrefab, spawnPosition, spawnRotation);
                projectile.transform.localScale = Vector3.one * (settings.projectileRadius * 2f);
                return projectile;
            }

            Debug.LogWarning("Floating bolt weapon has no projectile prefab assigned.", this);
            return null;
        }

        private void CreateVisual(GameObject visualPrefab)
        {
            var root = new GameObject("Bolt Visual");
            root.transform.SetParent(transform, false);
            visualRoot = root.transform;

            if (visualPrefab == null)
            {
                Debug.LogWarning("Floating bolt weapon has no visual prefab assigned.", this);
                baseVisualScale = visualRoot.localScale;
                return;
            }

            var visual = Instantiate(visualPrefab, visualRoot);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            baseVisualScale = visualRoot.localScale;
        }
    }
}
