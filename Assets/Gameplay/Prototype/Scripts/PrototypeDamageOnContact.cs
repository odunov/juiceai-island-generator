using System.Collections.Generic;
using UnityEngine;

namespace Islands.Prototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class PrototypeDamageOnContact : MonoBehaviour
    {
        [SerializeField]
        [Min(0f)]
        private float damage = 1f;

        [SerializeField]
        [Min(0f)]
        private float repeatInterval = 0.75f;

        [SerializeField]
        private bool affectDeadTargets;

        private readonly Dictionary<PrototypeHealth, float> nextDamageTimes = new Dictionary<PrototypeHealth, float>();

        private void Reset()
        {
            var damageCollider = GetComponent<Collider>();
            damageCollider.isTrigger = true;
        }

        private void OnValidate()
        {
            damage = Mathf.Max(0f, damage);
            repeatInterval = Mathf.Max(0f, repeatInterval);
        }

        private void OnDisable()
        {
            nextDamageTimes.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            TryDamage(other, other.ClosestPoint(transform.position));
        }

        private void OnTriggerStay(Collider other)
        {
            TryDamage(other, other.ClosestPoint(transform.position));
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryDamage(collision.collider, GetContactPoint(collision));
        }

        private void OnCollisionStay(Collision collision)
        {
            TryDamage(collision.collider, GetContactPoint(collision));
        }

        private void TryDamage(Component other, Vector3 hitPoint)
        {
            if (damage <= 0f || other == null)
            {
                return;
            }

            var health = other.GetComponentInParent<PrototypeHealth>();
            if (health == null || (!affectDeadTargets && health.IsDead))
            {
                return;
            }

            var now = Time.time;
            if (nextDamageTimes.TryGetValue(health, out var nextTime) && now < nextTime)
            {
                return;
            }

            var hitDirection = health.transform.position - transform.position;
            if (health.ApplyDamage(new DamageInfo(damage, gameObject, hitPoint, hitDirection)))
            {
                nextDamageTimes[health] = now + repeatInterval;
            }
        }

        private Vector3 GetContactPoint(Collision collision)
        {
            return collision.contactCount > 0 ? collision.GetContact(0).point : collision.transform.position;
        }
    }
}
