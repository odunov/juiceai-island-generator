using UnityEngine;

namespace Islands.Prototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PrototypeProjectile : MonoBehaviour
    {
        [SerializeField]
        [Min(0f)]
        private float damage = 1f;

        [SerializeField]
        [Min(0.05f)]
        private float lifetime = 4f;

        [Header("Feedback")]
        [SerializeField]
        private bool createDefaultTrail = true;

        [SerializeField]
        private Color trailColor = new Color(1f, 0.48f, 0.18f, 1f);

        [SerializeField]
        [Min(0.01f)]
        private float trailWidth = 0.12f;

        [SerializeField]
        [Min(0.01f)]
        private float trailTime = 0.16f;

        [SerializeField]
        [Min(0f)]
        private float impactBurstScale = 0.85f;

        private Rigidbody body;
        private TrailRenderer trail;
        private GameObject owner;
        private PrototypeHealth requiredTarget;
        private float despawnTime;
        private bool launched;

        private void Awake()
        {
            EnsureSetup();
        }

        private void OnEnable()
        {
            despawnTime = Time.time + lifetime;
        }

        private void Update()
        {
            if (launched && Time.time >= despawnTime)
            {
                Destroy(gameObject);
            }
        }

        private void Reset()
        {
            EnsureSetup();
        }

        private void OnValidate()
        {
            damage = Mathf.Max(0f, damage);
            lifetime = Mathf.Max(0.05f, lifetime);
            trailWidth = Mathf.Max(0.01f, trailWidth);
            trailTime = Mathf.Max(0.01f, trailTime);
            impactBurstScale = Mathf.Max(0f, impactBurstScale);
        }

        public void Launch(Vector3 velocity, float newDamage, float newLifetime, GameObject newOwner)
        {
            EnsureSetup();
            damage = Mathf.Max(0f, newDamage);
            lifetime = Mathf.Max(0.05f, newLifetime);
            owner = newOwner;
            despawnTime = Time.time + lifetime;
            launched = true;
            body.linearVelocity = velocity;
            ConfigureTrail();

            if (velocity.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
            }
        }

        public void ConfigureFeedback(Color newTrailColor, float newTrailWidth, float newTrailTime)
        {
            trailColor = newTrailColor;
            trailWidth = Mathf.Max(0.01f, newTrailWidth);
            trailTime = Mathf.Max(0.01f, newTrailTime);
            ConfigureTrail();
        }

        public void RestrictDamageTo(PrototypeHealth target)
        {
            requiredTarget = target;
        }

        private void OnTriggerEnter(Collider other)
        {
            TryResolveHit(other, other.ClosestPoint(transform.position));
        }

        private void OnCollisionEnter(Collision collision)
        {
            var hitPoint = collision.contactCount > 0 ? collision.GetContact(0).point : collision.transform.position;
            TryResolveHit(collision.collider, hitPoint);
        }

        private void EnsureSetup()
        {
            body = body != null ? body : GetComponent<Rigidbody>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody>();
            }

            body.useGravity = false;
            body.isKinematic = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.FreezeRotation;

            var projectileCollider = GetComponent<Collider>();
            if (projectileCollider == null)
            {
                projectileCollider = gameObject.AddComponent<SphereCollider>();
            }

            projectileCollider.isTrigger = true;
        }

        private void ConfigureTrail()
        {
            if (!createDefaultTrail)
            {
                return;
            }

            trail = trail != null ? trail : GetComponent<TrailRenderer>();
            if (trail == null)
            {
                trail = gameObject.AddComponent<TrailRenderer>();
            }

            trail.time = trailTime;
            trail.startWidth = trailWidth;
            trail.endWidth = 0f;
            trail.startColor = trailColor;
            trail.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
            trail.emitting = true;

            if (trail.sharedMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null)
                {
                    shader = Shader.Find("Sprites/Default");
                }

                if (shader != null)
                {
                    trail.material = new Material(shader);
                }
            }
        }

        private void TryResolveHit(Component other, Vector3 hitPoint)
        {
            if (!launched || other == null || other.gameObject == owner || other.transform.IsChildOf(transform))
            {
                return;
            }

            var health = other.GetComponentInParent<PrototypeHealth>();
            if (health == null || health.gameObject == owner || health.IsDead)
            {
                return;
            }

            if (requiredTarget != null && health != requiredTarget)
            {
                return;
            }

            var hitDirection = health.transform.position - transform.position;
            health.ApplyDamage(new DamageInfo(damage, owner != null ? owner : gameObject, hitPoint, hitDirection));
            PlayImpactFeedback(hitPoint);
            Destroy(gameObject);
        }

        private void PlayImpactFeedback(Vector3 hitPoint)
        {
            PrototypeWeaponFeedback.SpawnBurst(hitPoint, trailColor, impactBurstScale, 14);
            PrototypeWeaponFeedback.PlaySfx(PrototypeWeaponSfx.BoltImpact, hitPoint, 0.34f);
        }
    }
}
