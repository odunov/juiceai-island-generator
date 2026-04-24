using UnityEngine;

namespace Islands.Prototype
{
    [DisallowMultipleComponent]
    public sealed class PrototypeProjectileThrower : MonoBehaviour
    {
        [SerializeField]
        private PrototypeProjectile projectilePrefab;

        [SerializeField]
        private Vector3 localSpawnOffset = new Vector3(0f, 0.5f, 0.85f);

        [SerializeField]
        [Min(0.05f)]
        private float fireInterval = 1.25f;

        [SerializeField]
        [Min(0f)]
        private float projectileSpeed = 7f;

        [SerializeField]
        [Min(0.05f)]
        private float projectileLifetime = 4f;

        [SerializeField]
        [Min(0f)]
        private float projectileDamage = 1f;

        [SerializeField]
        [Min(0.05f)]
        private float projectileRadius = 0.22f;

        [SerializeField]
        private bool fireOnStart = true;

        private float nextFireTime;

        private void OnEnable()
        {
            nextFireTime = Time.time + (fireOnStart ? 0f : fireInterval);
        }

        private void OnValidate()
        {
            fireInterval = Mathf.Max(0.05f, fireInterval);
            projectileSpeed = Mathf.Max(0f, projectileSpeed);
            projectileLifetime = Mathf.Max(0.05f, projectileLifetime);
            projectileDamage = Mathf.Max(0f, projectileDamage);
            projectileRadius = Mathf.Max(0.05f, projectileRadius);
        }

        private void Update()
        {
            if (Time.time < nextFireTime)
            {
                return;
            }

            Fire();
            nextFireTime = Time.time + fireInterval;
        }

        public void Fire()
        {
            var spawnPosition = transform.TransformPoint(localSpawnOffset);
            var direction = transform.forward.sqrMagnitude > 0.0001f ? transform.forward.normalized : Vector3.forward;
            var projectile = CreateProjectile(spawnPosition, Quaternion.LookRotation(direction, Vector3.up));
            projectile.Launch(direction * projectileSpeed, projectileDamage, projectileLifetime, gameObject);
        }

        private PrototypeProjectile CreateProjectile(Vector3 spawnPosition, Quaternion spawnRotation)
        {
            if (projectilePrefab != null)
            {
                var spawnedProjectile = Instantiate(projectilePrefab, spawnPosition, spawnRotation);
                spawnedProjectile.transform.localScale = Vector3.one * (projectileRadius * 2f);
                return spawnedProjectile;
            }

            var projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectileObject.name = "Prototype Projectile";
            projectileObject.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            projectileObject.transform.localScale = Vector3.one * (projectileRadius * 2f);

            if (projectileObject.GetComponent<Rigidbody>() == null)
            {
                projectileObject.AddComponent<Rigidbody>();
            }

            var projectile = projectileObject.AddComponent<PrototypeProjectile>();
            var sphereCollider = projectileObject.GetComponent<SphereCollider>();
            if (sphereCollider != null)
            {
                sphereCollider.radius = 0.5f;
                sphereCollider.isTrigger = true;
            }

            return projectile;
        }
    }
}
