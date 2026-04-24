using UnityEngine;

namespace Islands.Prototype
{
    public readonly struct DamageInfo
    {
        public DamageInfo(float amount, GameObject source = null, Vector3 hitPoint = default, Vector3 hitDirection = default)
        {
            Amount = amount;
            Source = source;
            HitPoint = hitPoint;
            HitDirection = hitDirection.sqrMagnitude > 0.0001f ? hitDirection.normalized : Vector3.zero;
        }

        public float Amount { get; }

        public GameObject Source { get; }

        public Vector3 HitPoint { get; }

        public Vector3 HitDirection { get; }
    }
}
