using System;
using UnityEngine;

namespace Islands.Prototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class PrototypeExpeditionResourceNode : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField]
        private string resourceId = "Expedition Resource";

        [SerializeField]
        private Color resourceColor = new Color(0.14f, 0.72f, 1f, 1f);

        [SerializeField]
        [Min(0)]
        private int resourcesAmount = 1;

        [SerializeField]
        private Transform visualRoot;

        [SerializeField]
        [Min(0f)]
        private float bobAmplitude = 0.14f;

        [SerializeField]
        [Min(0f)]
        private float bobFrequency = 2.8f;

        [SerializeField]
        [Min(0f)]
        private float spinDegreesPerSecond = 90f;

        private Vector3 visualBaseLocalPosition;
        private MaterialPropertyBlock propertyBlock;
        private bool hasVisualBasePosition;
        private float age;

        public event Action<PrototypeExpeditionResourceNode> Collected;

        public string ResourceId => resourceId;

        public bool IsCollected { get; private set; }

        public int ResourcesAmount => resourcesAmount;

        public void Configure(string newResourceId, Color newResourceColor)
        {
            resourceId = string.IsNullOrWhiteSpace(newResourceId) ? "Expedition Resource" : newResourceId.Trim();
            resourceColor = newResourceColor;
            ConfigureCollider();
            ApplyColor();
        }

        public void ResetNode()
        {
            IsCollected = false;
            age = 0f;
            SetCollectibleVisible(true);
            ConfigureCollider();
            CaptureVisualBasePosition();
        }

        private void Reset()
        {
            ConfigureCollider();
            ApplyColor();
        }

        private void Awake()
        {
            ConfigureCollider();
            ApplyColor();
            CaptureVisualBasePosition();
        }

        private void OnValidate()
        {
            resourceId = string.IsNullOrWhiteSpace(resourceId) ? "Expedition Resource" : resourceId.Trim();
            resourcesAmount = Mathf.Max(0, resourcesAmount);
            bobAmplitude = Mathf.Max(0f, bobAmplitude);
            bobFrequency = Mathf.Max(0f, bobFrequency);
            spinDegreesPerSecond = Mathf.Max(0f, spinDegreesPerSecond);
            ConfigureCollider();
            ApplyColor();
        }

        private void Update()
        {
            if (IsCollected)
            {
                return;
            }

            age += Time.deltaTime;
            AnimateVisual();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PrototypePlayerController>() != null)
            {
                TryCollect();
            }
        }

        public bool TryCollect()
        {
            if (IsCollected)
            {
                return false;
            }

            IsCollected = true;
            SetCollectibleVisible(false);
            PrototypePersistentInventory.AddResources(resourcesAmount);
            PrototypeWeaponFeedback.SpawnBurst(transform.position + Vector3.up * 0.55f, resourceColor, 0.75f, 14);
            PrototypeWeaponFeedback.PlaySfx(PrototypeWeaponSfx.ResourcePickup, transform.position, 0.28f);
            Collected?.Invoke(this);
            return true;
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
            var renderers = GetVisualRenderers();
            propertyBlock ??= new MaterialPropertyBlock();
            foreach (var targetRenderer in renderers)
            {
                if (targetRenderer != null)
                {
                    targetRenderer.GetPropertyBlock(propertyBlock);
                    propertyBlock.SetColor(BaseColorId, resourceColor);
                    propertyBlock.SetColor(ColorId, resourceColor);
                    targetRenderer.SetPropertyBlock(propertyBlock);
                }
            }
        }

        private void CaptureVisualBasePosition()
        {
            var root = GetVisualRoot();
            if (root == null)
            {
                return;
            }

            visualBaseLocalPosition = root.localPosition;
            hasVisualBasePosition = true;
        }

        private void AnimateVisual()
        {
            var root = GetVisualRoot();
            if (root == null)
            {
                return;
            }

            if (!hasVisualBasePosition)
            {
                CaptureVisualBasePosition();
            }

            var localPosition = visualBaseLocalPosition;
            localPosition.y += Mathf.Sin(age * bobFrequency) * bobAmplitude;
            root.localPosition = localPosition;
            root.Rotate(Vector3.up, spinDegreesPerSecond * Time.deltaTime, Space.World);
        }

        private void SetCollectibleVisible(bool visible)
        {
            var pickupCollider = GetComponent<Collider>();
            if (pickupCollider != null)
            {
                pickupCollider.enabled = visible;
            }

            var renderers = GetVisualRenderers();
            foreach (var targetRenderer in renderers)
            {
                if (targetRenderer != null)
                {
                    targetRenderer.enabled = visible;
                }
            }
        }

        private Transform GetVisualRoot()
        {
            return visualRoot != null ? visualRoot : transform;
        }

        private Renderer[] GetVisualRenderers()
        {
            var root = GetVisualRoot();
            return root != null ? root.GetComponentsInChildren<Renderer>(true) : Array.Empty<Renderer>();
        }
    }
}
