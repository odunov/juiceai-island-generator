using System;
using UnityEngine;

namespace Islands.Prototype
{
    [DisallowMultipleComponent]
    public sealed class PrototypeFloatingWeaponLoadout : MonoBehaviour
    {
        [Serializable]
        public sealed class MeleeBladeSettings
        {
            public Vector3 idleOffset = new Vector3(-1.15f, 1.2f, 0.15f);
            [Min(0.01f)] public float followSmoothTime = 0.12f;
            [Min(0f)] public float followMaxSpeed = 24f;
            [Min(0f)] public float bobAmplitude = 0.12f;
            [Min(0f)] public float bobFrequency = 1.7f;
            [Min(0f)] public float idleTurnDegrees = 16f;
            [Min(0f)] public float idleTurnFrequency = 0.8f;
            [Min(0.1f)] public float attackRange = 3.2f;
            [Min(0f)] public float damage = 1.4f;
            [Min(0.05f)] public float cooldown = 0.9f;
            [Min(0.05f)] public float attackDuration = 0.52f;
            [Range(0.05f, 0.95f)] public float hitTime = 0.42f;
            [Min(0f)] public float slashArc = 0.75f;
            [Range(5f, 120f)] public float sweepAngleDegrees = 58f;
            [Min(0.1f)] public float sweepReach = 1.45f;
            [Min(0f)] public float hitRadius = 0.85f;
            [Min(0f)] public float returnOvershoot = 0.28f;
            [Min(0f)] public float attackScalePulse = 0.22f;
            public Color hitColor = new Color(1f, 0.38f, 0.12f, 1f);

            public void Validate()
            {
                followSmoothTime = Mathf.Max(0.01f, followSmoothTime);
                followMaxSpeed = Mathf.Max(0f, followMaxSpeed);
                bobAmplitude = Mathf.Max(0f, bobAmplitude);
                bobFrequency = Mathf.Max(0f, bobFrequency);
                idleTurnDegrees = Mathf.Max(0f, idleTurnDegrees);
                idleTurnFrequency = Mathf.Max(0f, idleTurnFrequency);
                attackRange = Mathf.Max(0.1f, attackRange);
                damage = Mathf.Max(0f, damage);
                cooldown = Mathf.Max(0.05f, cooldown);
                attackDuration = Mathf.Max(0.05f, attackDuration);
                hitTime = Mathf.Clamp(hitTime, 0.05f, 0.95f);
                slashArc = Mathf.Max(0f, slashArc);
                sweepAngleDegrees = Mathf.Clamp(sweepAngleDegrees, 5f, 120f);
                sweepReach = Mathf.Max(0.1f, sweepReach);
                hitRadius = Mathf.Max(0f, hitRadius);
                returnOvershoot = Mathf.Max(0f, returnOvershoot);
                attackScalePulse = Mathf.Max(0f, attackScalePulse);
            }
        }

        [Serializable]
        public sealed class BoltWeaponSettings
        {
            public Vector3 idleOffset = new Vector3(1.25f, 1.25f, -0.35f);
            [Min(0.01f)] public float followSmoothTime = 0.14f;
            [Min(0f)] public float followMaxSpeed = 24f;
            [Min(0f)] public float bobAmplitude = 0.1f;
            [Min(0f)] public float bobFrequency = 1.35f;
            [Min(0.1f)] public float attackRange = 8f;
            [Min(0f)] public float damage = 1f;
            [Min(0.05f)] public float cooldown = 0.72f;
            [Min(0f)] public float projectileSpeed = 11f;
            [Min(0.05f)] public float projectileLifetime = 2.5f;
            [Min(0.02f)] public float projectileRadius = 0.16f;
            [Min(0f)] public float recoilDistance = 0.38f;
            [Min(0.01f)] public float recoilRecoverTime = 0.11f;
            [Min(0f)] public float attackScalePulse = 0.16f;
            public Color boltColor = new Color(0.22f, 0.78f, 1f, 1f);

            public void Validate()
            {
                followSmoothTime = Mathf.Max(0.01f, followSmoothTime);
                followMaxSpeed = Mathf.Max(0f, followMaxSpeed);
                bobAmplitude = Mathf.Max(0f, bobAmplitude);
                bobFrequency = Mathf.Max(0f, bobFrequency);
                attackRange = Mathf.Max(0.1f, attackRange);
                damage = Mathf.Max(0f, damage);
                cooldown = Mathf.Max(0.05f, cooldown);
                projectileSpeed = Mathf.Max(0f, projectileSpeed);
                projectileLifetime = Mathf.Max(0.05f, projectileLifetime);
                projectileRadius = Mathf.Max(0.02f, projectileRadius);
                recoilDistance = Mathf.Max(0f, recoilDistance);
                recoilRecoverTime = Mathf.Max(0.01f, recoilRecoverTime);
                attackScalePulse = Mathf.Max(0f, attackScalePulse);
            }
        }

        [SerializeField]
        private PrototypeHealth ownerHealth;

        [SerializeField]
        private GameObject meleeBladeVisualPrefab;

        [SerializeField]
        private GameObject boltWeaponVisualPrefab;

        [SerializeField]
        private PrototypeProjectile boltProjectilePrefab;

        [SerializeField]
        private MeleeBladeSettings meleeBlade = new MeleeBladeSettings();

        [SerializeField]
        private BoltWeaponSettings boltWeapon = new BoltWeaponSettings();

        private PrototypeFloatingMeleeBlade meleeInstance;
        private PrototypeFloatingBoltWeapon boltInstance;
        private bool initialized;

        public GameObject Owner => ownerHealth != null ? ownerHealth.gameObject : gameObject;

        public Transform OwnerTransform => ownerHealth != null ? ownerHealth.transform : transform;

        public bool CanAttack => ownerHealth == null || !ownerHealth.IsDead;

        private void Awake()
        {
            EnsureSettings();
            ownerHealth = ownerHealth != null ? ownerHealth : GetComponentInParent<PrototypeHealth>();
            initialized = true;
            CreateWeapons();
        }

        private void OnEnable()
        {
            if (initialized)
            {
                CreateWeapons();
            }
        }

        private void OnValidate()
        {
            EnsureSettings();
            meleeBlade.Validate();
            boltWeapon.Validate();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (meleeInstance != null)
            {
                Destroy(meleeInstance.gameObject);
                meleeInstance = null;
            }

            if (boltInstance != null)
            {
                Destroy(boltInstance.gameObject);
                boltInstance = null;
            }
        }

        public PrototypeCombatTarget FindNearestTarget(Vector3 origin, float range)
        {
            return PrototypeCombatTarget.FindNearest(origin, range, Owner);
        }

        public bool TryFindNearestTarget(Vector3 origin, float range, out PrototypeCombatTarget target)
        {
            target = FindNearestTarget(origin, range);
            return target != null;
        }

        public void SnapWeaponsToOwner()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            CreateWeapons();
            meleeInstance?.SnapToOwner();
            boltInstance?.SnapToOwner();
        }

        private void CreateWeapons()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (meleeInstance == null)
            {
                var meleeObject = new GameObject("Prototype Floating Melee Blade");
                meleeInstance = meleeObject.AddComponent<PrototypeFloatingMeleeBlade>();
                meleeInstance.Configure(this, meleeBlade, meleeBladeVisualPrefab);
            }

            if (boltInstance == null)
            {
                var boltObject = new GameObject("Prototype Floating Bolt Weapon");
                boltInstance = boltObject.AddComponent<PrototypeFloatingBoltWeapon>();
                boltInstance.Configure(this, boltWeapon, boltProjectilePrefab, boltWeaponVisualPrefab);
            }
        }

        private void EnsureSettings()
        {
            meleeBlade ??= new MeleeBladeSettings();
            boltWeapon ??= new BoltWeaponSettings();
        }
    }

    internal sealed class PrototypeFloatingWeaponMotor
    {
        private Transform owner;
        private Vector3 idleOffset;
        private Vector3 followVelocity;
        private float followSmoothTime;
        private float followMaxSpeed;
        private float bobAmplitude;
        private float bobFrequency;
        private float bobPhase;

        public Vector3 OwnerForward
        {
            get
            {
                if (owner == null)
                {
                    return Vector3.forward;
                }

                var forward = owner.forward.ProjectedOnPlane();
                return forward.sqrMagnitude > 0.0001f ? forward : Vector3.forward;
            }
        }

        public void Configure(
            Transform newOwner,
            Vector3 newIdleOffset,
            float newFollowSmoothTime,
            float newFollowMaxSpeed,
            float newBobAmplitude,
            float newBobFrequency,
            float newBobPhase)
        {
            owner = newOwner;
            idleOffset = newIdleOffset;
            followSmoothTime = Mathf.Max(0.01f, newFollowSmoothTime);
            followMaxSpeed = Mathf.Max(0f, newFollowMaxSpeed);
            bobAmplitude = Mathf.Max(0f, newBobAmplitude);
            bobFrequency = Mathf.Max(0f, newBobFrequency);
            bobPhase = newBobPhase;
            followVelocity = Vector3.zero;
        }

        public Vector3 GetIdlePosition()
        {
            if (owner == null)
            {
                return Vector3.zero;
            }

            var forward = OwnerForward;
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            var bob = Mathf.Sin((Time.time * bobFrequency) + bobPhase) * bobAmplitude;
            return owner.position
                + right * idleOffset.x
                + Vector3.up * (idleOffset.y + bob)
                + forward * idleOffset.z;
        }

        public Vector3 SmoothFollow(Transform target, Vector3 worldOffset = default)
        {
            var targetPosition = GetIdlePosition() + worldOffset;
            target.position = Vector3.SmoothDamp(
                target.position,
                targetPosition,
                ref followVelocity,
                followSmoothTime,
                followMaxSpeed,
                Time.deltaTime);

            return target.position;
        }

        public void ResetVelocity()
        {
            followVelocity = Vector3.zero;
        }

        public Vector3 SnapToIdle(Transform target, Vector3 worldOffset = default)
        {
            var targetPosition = GetIdlePosition() + worldOffset;
            target.position = targetPosition;
            followVelocity = Vector3.zero;
            return targetPosition;
        }
    }
}
