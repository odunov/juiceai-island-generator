using Islands.Prototype;
using NUnit.Framework;
using UnityEngine;

namespace Islands.EditorTools.Tests
{
    public sealed class PrototypeFloatingWeaponTests
    {
        [Test]
        public void FindNearest_ReturnsNearestMarkedLivingTarget()
        {
            var owner = new GameObject("owner");
            var near = CreateTarget("near-target", new Vector3(101f, 0f, 100f), true);
            var far = CreateTarget("far-target", new Vector3(104f, 0f, 100f), true);
            var unmarked = CreateTarget("unmarked-health", new Vector3(100.2f, 0f, 100f), false);
            var dead = CreateTarget("dead-target", new Vector3(100.4f, 0f, 100f), true);

            try
            {
                dead.GetComponent<PrototypeHealth>().ApplyDamage(99f);

                var result = PrototypeCombatTarget.FindNearest(new Vector3(100f, 0f, 100f), 5f, owner);

                Assert.That(result, Is.SameAs(near.GetComponent<PrototypeCombatTarget>()));
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(near);
                Object.DestroyImmediate(far);
                Object.DestroyImmediate(unmarked);
                Object.DestroyImmediate(dead);
            }
        }

        [Test]
        public void FindNearest_IgnoresOwnerEvenWhenOwnerIsMarked()
        {
            var owner = CreateTarget("owner", new Vector3(200f, 0f, 200f), true);
            var enemy = CreateTarget("enemy", new Vector3(201f, 0f, 200f), true);

            try
            {
                var result = PrototypeCombatTarget.FindNearest(owner.transform.position, 3f, owner);

                Assert.That(result, Is.SameAs(enemy.GetComponent<PrototypeCombatTarget>()));
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(enemy);
            }
        }

        [Test]
        public void Loadout_TargetQueryUsesOwnerFiltering()
        {
            var owner = CreateTarget("owner", new Vector3(300f, 0f, 300f), true);
            var enemy = CreateTarget("enemy", new Vector3(301f, 0f, 300f), true);
            var loadout = owner.AddComponent<PrototypeFloatingWeaponLoadout>();

            try
            {
                var found = loadout.TryFindNearestTarget(owner.transform.position, 3f, out var result);

                Assert.That(found, Is.True);
                Assert.That(result, Is.SameAs(enemy.GetComponent<PrototypeCombatTarget>()));
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(enemy);
            }
        }

        [Test]
        public void BladeSweepArc_AccumulatesFromLeftToRight()
        {
            var origin = Vector3.zero;
            var centerDirection = Vector3.forward;

            Assert.That(IsInsideSweep(DirectionFromYaw(-45f), origin, centerDirection, 0.5f), Is.True);
            Assert.That(IsInsideSweep(DirectionFromYaw(0f), origin, centerDirection, 0.5f), Is.True);
            Assert.That(IsInsideSweep(DirectionFromYaw(45f), origin, centerDirection, 0.5f), Is.False);
            Assert.That(IsInsideSweep(DirectionFromYaw(45f), origin, centerDirection, 1f), Is.True);
        }

        [Test]
        public void BladeSweepArc_RejectsPositionsOutsideBladeVolume()
        {
            var origin = Vector3.zero;
            var centerDirection = Vector3.forward;

            Assert.That(IsInsideSweep(Vector3.forward * 3f, origin, centerDirection, 1f), Is.False);
            Assert.That(IsInsideSweep(new Vector3(0f, 1f, 1f), origin, centerDirection, 1f), Is.False);
        }

        private static GameObject CreateTarget(string name, Vector3 position, bool marked)
        {
            var gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gameObject.name = name;
            gameObject.transform.position = position;
            gameObject.AddComponent<PrototypeHealth>();

            if (marked)
            {
                gameObject.AddComponent<PrototypeCombatTarget>();
            }

            return gameObject;
        }

        private static bool IsInsideSweep(Vector3 point, Vector3 origin, Vector3 centerDirection, float progress)
        {
            return PrototypeFloatingMeleeBlade.IsPointInsideSweepArc(
                point,
                origin,
                centerDirection,
                progress,
                60f,
                2f,
                0.15f);
        }

        private static Vector3 DirectionFromYaw(float degrees)
        {
            return Quaternion.AngleAxis(degrees, Vector3.up) * Vector3.forward * 1.25f;
        }
    }
}
