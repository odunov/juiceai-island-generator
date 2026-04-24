using UnityEngine;

namespace Islands.Prototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class PrototypeResourceDrop : MonoBehaviour
    {
        [SerializeField]
        [Min(1)]
        private int amount = 1;

        [SerializeField]
        private Color dropColor = new Color(1f, 0.82f, 0.18f, 1f);

        [SerializeField]
        [Min(0.05f)]
        private float pickupRadius = 0.55f;

        [SerializeField]
        [Min(0f)]
        private float attractRadius = 2.4f;

        [SerializeField]
        [Min(0f)]
        private float attractSpeed = 5.5f;

        [SerializeField]
        [Min(0f)]
        private float spinDegreesPerSecond = 240f;

        [SerializeField]
        [Min(0f)]
        private float bobAmplitude = 0.08f;

        [SerializeField]
        [Min(0f)]
        private float bobFrequency = 3.2f;

        private PrototypePlayerController player;
        private Vector3 baseScale;
        private float spawnY;
        private float age;
        private bool collected;

        public static int SessionTotal { get; private set; }

        public int Amount => amount;

        public void Configure(int newAmount, Color newDropColor)
        {
            amount = Mathf.Max(1, newAmount);
            dropColor = newDropColor;
            ConfigureCollider();
            ApplyColor();
        }

        private void Awake()
        {
            baseScale = transform.localScale;
            spawnY = transform.position.y;
            ConfigureCollider();
            ApplyColor();
        }

        private void OnValidate()
        {
            amount = Mathf.Max(1, amount);
            pickupRadius = Mathf.Max(0.05f, pickupRadius);
            attractRadius = Mathf.Max(0f, attractRadius);
            attractSpeed = Mathf.Max(0f, attractSpeed);
            spinDegreesPerSecond = Mathf.Max(0f, spinDegreesPerSecond);
            bobAmplitude = Mathf.Max(0f, bobAmplitude);
            bobFrequency = Mathf.Max(0f, bobFrequency);
        }

        private void Update()
        {
            if (collected)
            {
                return;
            }

            age += Time.deltaTime;
            RefreshPlayer();
            Animate();
            UpdateAttraction();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (collected || other.GetComponentInParent<PrototypePlayerController>() == null)
            {
                return;
            }

            Collect();
        }

        private void ConfigureCollider()
        {
            var pickupCollider = GetComponent<Collider>();
            if (pickupCollider != null)
            {
                pickupCollider.isTrigger = true;
            }
        }

        private void ApplyColor()
        {
            if (TryGetComponent<Renderer>(out var renderer))
            {
                renderer.material.color = dropColor;
            }
        }

        private void RefreshPlayer()
        {
            if (player == null)
            {
                player = FindFirstObjectByType<PrototypePlayerController>();
            }
        }

        private void Animate()
        {
            var position = transform.position;
            position.y = spawnY + Mathf.Sin(age * bobFrequency) * bobAmplitude;
            transform.position = position;
            transform.Rotate(Vector3.up, spinDegreesPerSecond * Time.deltaTime, Space.World);
            transform.localScale = baseScale * (1f + Mathf.Sin(age * 8f) * 0.05f);
        }

        private void UpdateAttraction()
        {
            if (player == null)
            {
                return;
            }

            var targetPosition = player.transform.position + Vector3.up * 0.5f;
            var toPlayer = targetPosition - transform.position;
            var distance = toPlayer.magnitude;

            if (distance <= pickupRadius)
            {
                Collect();
                return;
            }

            if (distance <= attractRadius && distance > 0.0001f)
            {
                transform.position = Vector3.MoveTowards(transform.position, targetPosition, attractSpeed * Time.deltaTime);
            }
        }

        private void Collect()
        {
            if (collected)
            {
                return;
            }

            collected = true;
            SessionTotal += amount;
            PrototypePersistentInventory.AddCurrency(amount);
            PrototypeWeaponFeedback.SpawnBurst(transform.position, dropColor, 0.45f, 8);
            PrototypeWeaponFeedback.PlaySfx(PrototypeWeaponSfx.ResourcePickup, transform.position, 0.22f);
            Destroy(gameObject);
        }
    }
}
