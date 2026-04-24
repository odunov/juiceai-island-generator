using System;
using System.Collections;
using UnityEngine;

namespace Islands.Prototype
{
    [DisallowMultipleComponent]
    public sealed class PrototypeHealth : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField]
        [Min(1f)]
        private float maxHealth = 5f;

        [SerializeField]
        [Min(0f)]
        private float invulnerabilityDuration = 0.25f;

        [SerializeField]
        private Transform feedbackRoot;

        [SerializeField]
        private bool includeInactiveFeedbackRenderers = true;

        [SerializeField]
        private Renderer[] feedbackRenderers;

        [SerializeField]
        private Color hitFlashColor = new Color(1f, 0.22f, 0.13f, 1f);

        [SerializeField]
        private Color deathColor = new Color(0.17f, 0.17f, 0.17f, 1f);

        [SerializeField]
        [Min(0f)]
        private float hitFlashDuration = 0.12f;

        [SerializeField]
        [HideInInspector]
        private float currentHealth = 5f;

        private MaterialPropertyBlock propertyBlock;
        private Coroutine flashRoutine;
        private float nextDamageTime;
        private bool isDead;

        public event Action<PrototypeHealth, DamageInfo> Damaged;

        public event Action<PrototypeHealth> Died;

        public float MaxHealth => maxHealth;

        public float CurrentHealth => currentHealth;

        public bool IsDead => isDead;

        public float NormalizedHealth => maxHealth <= 0f ? 0f : Mathf.Clamp01(currentHealth / maxHealth);

        public void Configure(float newMaxHealth, float newInvulnerabilityDuration, bool refill = true)
        {
            maxHealth = Mathf.Max(1f, newMaxHealth);
            invulnerabilityDuration = Mathf.Max(0f, newInvulnerabilityDuration);

            if (refill)
            {
                isDead = false;
                currentHealth = maxHealth;
                nextDamageTime = 0f;
                ClearFeedbackColor();
                return;
            }

            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
            isDead = currentHealth <= 0f;
        }

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();

            if (currentHealth <= 0f || currentHealth > maxHealth)
            {
                currentHealth = maxHealth;
            }
        }

        private void OnEnable()
        {
            if (currentHealth <= 0f)
            {
                currentHealth = maxHealth;
            }
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            invulnerabilityDuration = Mathf.Max(0f, invulnerabilityDuration);
            hitFlashDuration = Mathf.Max(0f, hitFlashDuration);
            currentHealth = currentHealth <= 0f ? maxHealth : Mathf.Clamp(currentHealth, 0f, maxHealth);
        }

        public bool ApplyDamage(float amount)
        {
            return ApplyDamage(new DamageInfo(amount));
        }

        public bool Kill()
        {
            if (isDead)
            {
                return false;
            }

            currentHealth = 0f;
            nextDamageTime = 0f;
            Die();
            return true;
        }

        public bool ApplyDamage(DamageInfo damageInfo)
        {
            if (isDead || damageInfo.Amount <= 0f)
            {
                return false;
            }

            if (Time.time < nextDamageTime)
            {
                return false;
            }

            currentHealth = Mathf.Max(0f, currentHealth - damageInfo.Amount);
            nextDamageTime = Time.time + invulnerabilityDuration;

            Damaged?.Invoke(this, damageInfo);
            PlayHitFeedback();

            if (currentHealth <= 0f)
            {
                Die();
            }

            return true;
        }

        public void ResetHealth()
        {
            isDead = false;
            currentHealth = maxHealth;
            nextDamageTime = 0f;

            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
                flashRoutine = null;
            }

            ClearFeedbackColor();
        }

        private void Die()
        {
            if (isDead)
            {
                return;
            }

            isDead = true;
            ApplyFeedbackColor(deathColor);
            Died?.Invoke(this);
        }

        private void PlayHitFeedback()
        {
            if (!Application.isPlaying || GetFeedbackRenderers().Length == 0)
            {
                return;
            }

            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
            }

            flashRoutine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            ApplyFeedbackColor(hitFlashColor);

            if (hitFlashDuration > 0f)
            {
                yield return new WaitForSeconds(hitFlashDuration);
            }

            if (isDead)
            {
                ApplyFeedbackColor(deathColor);
            }
            else
            {
                ClearFeedbackColor();
            }

            flashRoutine = null;
        }

        private void ApplyFeedbackColor(Color color)
        {
            var renderers = GetFeedbackRenderers();
            if (renderers.Length == 0)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();

            foreach (var targetRenderer in renderers)
            {
                if (targetRenderer == null)
                {
                    continue;
                }

                targetRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(BaseColorId, color);
                propertyBlock.SetColor(ColorId, color);
                targetRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        private void ClearFeedbackColor()
        {
            var renderers = GetFeedbackRenderers();
            if (renderers.Length == 0)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            propertyBlock.Clear();

            foreach (var targetRenderer in renderers)
            {
                if (targetRenderer != null)
                {
                    targetRenderer.SetPropertyBlock(propertyBlock);
                }
            }
        }

        private Renderer[] GetFeedbackRenderers()
        {
            if (feedbackRoot != null)
            {
                return feedbackRoot.GetComponentsInChildren<Renderer>(includeInactiveFeedbackRenderers);
            }

            if (feedbackRenderers != null && feedbackRenderers.Length > 0)
            {
                return feedbackRenderers;
            }

            return GetComponentsInChildren<Renderer>(includeInactiveFeedbackRenderers);
        }
    }
}
