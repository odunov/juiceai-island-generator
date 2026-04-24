using Islands.Prototype;
using NUnit.Framework;
using UnityEngine;

namespace Islands.EditorTools.Tests
{
    public sealed class PrototypeHealthTests
    {
        [Test]
        public void ApplyDamage_ReducesCurrentHealth()
        {
            var gameObject = new GameObject("health-test");
            try
            {
                var health = gameObject.AddComponent<PrototypeHealth>();

                Assert.That(health.ApplyDamage(2f), Is.True);

                Assert.That(health.CurrentHealth, Is.EqualTo(3f));
                Assert.That(health.IsDead, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ApplyDamage_RejectsNonPositiveDamage()
        {
            var gameObject = new GameObject("health-test");
            try
            {
                var health = gameObject.AddComponent<PrototypeHealth>();

                Assert.That(health.ApplyDamage(0f), Is.False);
                Assert.That(health.ApplyDamage(-1f), Is.False);

                Assert.That(health.CurrentHealth, Is.EqualTo(5f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ApplyDamage_BlocksRepeatedDamageDuringInvulnerabilityWindow()
        {
            var gameObject = new GameObject("health-test");
            try
            {
                var health = gameObject.AddComponent<PrototypeHealth>();

                Assert.That(health.ApplyDamage(1f), Is.True);
                Assert.That(health.ApplyDamage(1f), Is.False);

                Assert.That(health.CurrentHealth, Is.EqualTo(4f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ApplyDamage_FiresDeathOnce()
        {
            var gameObject = new GameObject("health-test");
            try
            {
                var health = gameObject.AddComponent<PrototypeHealth>();
                var deathCount = 0;
                health.Died += _ => deathCount++;

                health.ApplyDamage(99f);
                health.ApplyDamage(99f);

                Assert.That(health.IsDead, Is.True);
                Assert.That(deathCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ResetHealth_RestoresCurrentHealthAndLivingState()
        {
            var gameObject = new GameObject("health-test");
            try
            {
                var health = gameObject.AddComponent<PrototypeHealth>();

                health.ApplyDamage(99f);
                health.ResetHealth();

                Assert.That(health.CurrentHealth, Is.EqualTo(5f));
                Assert.That(health.IsDead, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void PlayerController_RestoreHealth_RefillsWithoutMoving()
        {
            var gameObject = new GameObject("player-health-test");
            try
            {
                gameObject.AddComponent<Rigidbody>();
                gameObject.AddComponent<CapsuleCollider>();
                var health = gameObject.AddComponent<PrototypeHealth>();
                var controller = gameObject.AddComponent<PrototypePlayerController>();
                gameObject.transform.position = new Vector3(3f, 1f, -2f);

                Assert.That(health.ApplyDamage(2f), Is.True);

                controller.RestoreHealth();

                Assert.That(health.CurrentHealth, Is.EqualTo(5f));
                Assert.That(health.IsDead, Is.False);
                Assert.That(gameObject.transform.position.x, Is.EqualTo(3f).Within(0.001f));
                Assert.That(gameObject.transform.position.y, Is.EqualTo(1f).Within(0.001f));
                Assert.That(gameObject.transform.position.z, Is.EqualTo(-2f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void FloatingHealthBar_EnsureForIsIdempotent()
        {
            var gameObject = new GameObject("health-bar-test");
            try
            {
                var health = gameObject.AddComponent<PrototypeHealth>();

                var first = PrototypeFloatingHealthBar.EnsureFor(health, PrototypeFloatingHealthBar.Preset.Enemy);
                var second = PrototypeFloatingHealthBar.EnsureFor(health, PrototypeFloatingHealthBar.Preset.Player);

                Assert.That(second, Is.SameAs(first));
                Assert.That(first.CurrentPreset, Is.EqualTo(PrototypeFloatingHealthBar.Preset.Player));
                Assert.That(gameObject.GetComponents<PrototypeFloatingHealthBar>(), Has.Length.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
