using UnityEngine;

namespace Islands.Prototype
{
    public enum PrototypeWeaponSfx
    {
        MeleeHit,
        BoltFire,
        BoltImpact,
        EnemyDeath,
        ResourcePickup
    }

    public static class PrototypeWeaponFeedback
    {
        private const int SampleRate = 22050;

        private static AudioClip meleeHitClip;
        private static AudioClip boltFireClip;
        private static AudioClip boltImpactClip;
        private static AudioClip enemyDeathClip;
        private static AudioClip resourcePickupClip;
        private static Material burstMaterial;

        public static void SpawnBurst(Vector3 position, Color color, float scale = 1f, int count = 12)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            var burst = new GameObject("Prototype Weapon Hit Burst");
            burst.transform.position = position;

            var particles = burst.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.playOnAwake = false;
            main.duration = 0.22f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f * scale, 3.8f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f * scale, 0.12f * scale);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = particles.emission;
            emission.enabled = false;

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f * scale;

            var renderer = burst.GetComponent<ParticleSystemRenderer>();
            renderer.material = GetBurstMaterial();

            particles.Emit(Mathf.Max(1, count));
            Object.Destroy(burst, 0.6f);
        }

        public static void PlaySfx(PrototypeWeaponSfx sfx, Vector3 position, float volume = 0.35f)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            AudioSource.PlayClipAtPoint(GetClip(sfx), position, Mathf.Clamp01(volume));
        }

        private static AudioClip GetClip(PrototypeWeaponSfx sfx)
        {
            switch (sfx)
            {
                case PrototypeWeaponSfx.BoltFire:
                    boltFireClip ??= CreateTone("Prototype Bolt Fire", 660f, 0.07f, 36f, 0.55f);
                    return boltFireClip;
                case PrototypeWeaponSfx.BoltImpact:
                    boltImpactClip ??= CreateTone("Prototype Bolt Impact", 180f, 0.12f, 24f, 0.7f);
                    return boltImpactClip;
                case PrototypeWeaponSfx.EnemyDeath:
                    enemyDeathClip ??= CreateTone("Prototype Enemy Death", 115f, 0.18f, 18f, 0.7f);
                    return enemyDeathClip;
                case PrototypeWeaponSfx.ResourcePickup:
                    resourcePickupClip ??= CreateTone("Prototype Resource Pickup", 920f, 0.08f, 34f, 0.5f);
                    return resourcePickupClip;
                default:
                    meleeHitClip ??= CreateTone("Prototype Melee Hit", 320f, 0.1f, 30f, 0.65f);
                    return meleeHitClip;
            }
        }

        private static AudioClip CreateTone(string name, float frequency, float duration, float decay, float gain)
        {
            var sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            var samples = new float[sampleCount];
            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)SampleRate;
                var envelope = Mathf.Exp(-decay * t) * Mathf.Clamp01(1f - t / duration);
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * gain;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static Material GetBurstMaterial()
        {
            if (burstMaterial != null)
            {
                return burstMaterial;
            }

            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Particles/Standard Unlit");
            }

            burstMaterial = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
            return burstMaterial;
        }
    }
}
