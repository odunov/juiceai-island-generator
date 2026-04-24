using System.Collections.Generic;
using UnityEngine;

namespace Islands.Prototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PrototypeHealth))]
    public sealed class PrototypeCombatTarget : MonoBehaviour
    {
        private static readonly List<PrototypeCombatTarget> ActiveTargets = new List<PrototypeCombatTarget>();

        [SerializeField]
        private Transform aimPoint;

        [SerializeField]
        [Min(0f)]
        private float fallbackAimHeight = 0.75f;

        private PrototypeHealth health;

        public PrototypeHealth Health
        {
            get
            {
                health = health != null ? health : GetComponent<PrototypeHealth>();
                return health;
            }
        }

        public Vector3 AimPosition => aimPoint != null ? aimPoint.position : transform.position + Vector3.up * fallbackAimHeight;

        public static IReadOnlyList<PrototypeCombatTarget> Targets => ActiveTargets;

        private void Awake()
        {
            health = GetComponent<PrototypeHealth>();
        }

        private void OnEnable()
        {
            if (!ActiveTargets.Contains(this))
            {
                ActiveTargets.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveTargets.Remove(this);
        }

        private void OnValidate()
        {
            fallbackAimHeight = Mathf.Max(0f, fallbackAimHeight);
        }

        public bool IsTargetable(GameObject owner)
        {
            var targetHealth = Health;
            if (!isActiveAndEnabled || targetHealth == null || targetHealth.IsDead)
            {
                return false;
            }

            if (owner == null)
            {
                return true;
            }

            var ownerTransform = owner.transform;
            return gameObject != owner
                && !transform.IsChildOf(ownerTransform)
                && !ownerTransform.IsChildOf(transform);
        }

        public static PrototypeCombatTarget FindNearest(Vector3 origin, float range, GameObject owner)
        {
            PruneMissingTargets();
            EnsureActiveTargetsDiscovered();

            var maxSqrDistance = Mathf.Max(0f, range) * Mathf.Max(0f, range);
            var nearestSqrDistance = maxSqrDistance;
            PrototypeCombatTarget nearest = null;

            for (var i = ActiveTargets.Count - 1; i >= 0; i--)
            {
                var candidate = ActiveTargets[i];
                if (candidate == null)
                {
                    ActiveTargets.RemoveAt(i);
                    continue;
                }

                if (!candidate.IsTargetable(owner))
                {
                    continue;
                }

                var sqrDistance = (candidate.AimPosition - origin).sqrMagnitude;
                if (sqrDistance <= nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    nearest = candidate;
                }
            }

            return nearest;
        }

        private static void PruneMissingTargets()
        {
            for (var i = ActiveTargets.Count - 1; i >= 0; i--)
            {
                if (ActiveTargets[i] == null)
                {
                    ActiveTargets.RemoveAt(i);
                }
            }
        }

        private static void EnsureActiveTargetsDiscovered()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                RebuildActiveTargets();
                return;
            }
#endif

            if (ActiveTargets.Count > 0)
            {
                return;
            }

            RebuildActiveTargets();
        }

        private static void RebuildActiveTargets()
        {
            ActiveTargets.Clear();
            var targets = FindObjectsByType<PrototypeCombatTarget>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var target in targets)
            {
                if (target != null && target.isActiveAndEnabled && !ActiveTargets.Contains(target))
                {
                    ActiveTargets.Add(target);
                }
            }
        }
    }
}
