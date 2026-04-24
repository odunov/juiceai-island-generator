using System;
using UnityEngine;

namespace Islands.Prototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class PrototypeIslandTravelGate : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");

        [SerializeField]
        private Transform destination;

        [SerializeField]
        private bool unlocked = true;

        [SerializeField]
        private bool captureRespawnAtDestination = true;

        [SerializeField]
        [Min(0f)]
        private float cooldown = 1f;

        [SerializeField]
        [Min(0f)]
        private float destinationGroundOffset = 0.05f;

        [Header("Visual State")]
        [SerializeField]
        private bool autoFindVisualRenderers = true;

        [SerializeField]
        private Renderer[] visualRenderers;

        [SerializeField]
        private Color unlockedTint = Color.white;

        [SerializeField]
        private Color lockedTint = new Color(0.45f, 0.45f, 0.45f, 0.5f);

        private float nextTravelTime;

        public event Action<PrototypeIslandTravelGate, PrototypePlayerController> Traveled;

        public bool IsUnlocked => unlocked;

        public void SetUnlocked(bool value)
        {
            if (unlocked == value)
            {
                RefreshVisuals();
                return;
            }

            unlocked = value;
            RefreshVisuals();
        }

        private void Reset()
        {
            ConfigureCollider();
            RefreshVisuals();
        }

        private void OnValidate()
        {
            cooldown = Mathf.Max(0f, cooldown);
            destinationGroundOffset = Mathf.Max(0f, destinationGroundOffset);
            ConfigureCollider();
            RefreshVisuals();
        }

        private void Awake()
        {
            ConfigureCollider();
            RefreshVisuals();
        }

        private void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponentInParent<PrototypePlayerController>();
            if (player != null)
            {
                TryTravel(player);
            }
        }

        public bool TryTravel(PrototypePlayerController player)
        {
            if (!unlocked || Time.time < nextTravelTime || destination == null || player == null || player.IsDead)
            {
                return false;
            }

            Travel(player);
            return true;
        }

        private void ConfigureCollider()
        {
            var trigger = GetComponent<Collider>();
            if (trigger != null)
            {
                trigger.isTrigger = true;
            }
        }

        private void RefreshVisuals()
        {
            var tint = unlocked ? unlockedTint : lockedTint;
            var renderers = GetVisualRenderers();
            foreach (var targetRenderer in renderers)
            {
                if (targetRenderer == null)
                {
                    continue;
                }

                ApplyTint(targetRenderer, tint);
            }
        }

        private Renderer[] GetVisualRenderers()
        {
            if (!autoFindVisualRenderers && visualRenderers != null)
            {
                return visualRenderers;
            }

            if (visualRenderers != null && visualRenderers.Length > 0)
            {
                return visualRenderers;
            }

            return GetComponentsInChildren<Renderer>(true);
        }

        private static void ApplyTint(Renderer targetRenderer, Color tint)
        {
            var materials = Application.isPlaying ? targetRenderer.materials : targetRenderer.sharedMaterials;
            foreach (var material in materials)
            {
                if (material == null)
                {
                    continue;
                }

                if (material.HasProperty(BaseColorId))
                {
                    material.SetColor(BaseColorId, tint);
                }

                if (material.HasProperty(ColorId))
                {
                    material.SetColor(ColorId, tint);
                }

                ApplyTransparencyState(material, tint.a < 0.999f);
            }
        }

        private static void ApplyTransparencyState(Material material, bool transparent)
        {
            if (transparent)
            {
                if (material.HasProperty(SurfaceId))
                {
                    material.SetFloat(SurfaceId, 1f);
                }

                if (material.HasProperty(SrcBlendId))
                {
                    material.SetFloat(SrcBlendId, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                }

                if (material.HasProperty(DstBlendId))
                {
                    material.SetFloat(DstBlendId, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                }

                if (material.HasProperty(ZWriteId))
                {
                    material.SetFloat(ZWriteId, 0f);
                }

                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                return;
            }

            if (material.HasProperty(SurfaceId))
            {
                material.SetFloat(SurfaceId, 0f);
            }

            if (material.HasProperty(ZWriteId))
            {
                material.SetFloat(ZWriteId, 1f);
            }

            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = -1;
        }

        private void Travel(PrototypePlayerController player)
        {
            nextTravelTime = Time.time + cooldown;

            var targetPosition = destination.position + Vector3.up * destinationGroundOffset;
            var targetRotation = destination.rotation;
            player.TeleportTo(targetPosition, targetRotation, captureRespawnAtDestination);

            Traveled?.Invoke(this, player);
        }
    }
}
