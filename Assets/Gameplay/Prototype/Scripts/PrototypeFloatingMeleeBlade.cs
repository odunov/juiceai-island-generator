using System.Collections.Generic;
using UnityEngine;

namespace Islands.Prototype
{
    [DisallowMultipleComponent]
    public sealed class PrototypeFloatingMeleeBlade : MonoBehaviour
    {
        private static readonly Quaternion IdleVisualRotation = Quaternion.identity;
        private static readonly Quaternion AttackVisualRotation = Quaternion.Euler(90f, 0f, 0f);
        private const float SweepDurationNormalized = 0.3f;

        private PrototypeFloatingWeaponLoadout loadout;
        private PrototypeFloatingWeaponLoadout.MeleeBladeSettings settings;
        private readonly PrototypeFloatingWeaponMotor motor = new PrototypeFloatingWeaponMotor();
        private readonly HashSet<PrototypeCombatTarget> hitTargets = new HashSet<PrototypeCombatTarget>();
        private Transform visualRoot;
        private Vector3 attackStartPosition;
        private Vector3 attackTargetPosition;
        private Vector3 attackDirection;
        private Vector3 baseVisualScale = Vector3.one;
        private PrototypeCombatTarget attackTarget;
        private float attackTimer;
        private float nextAttackTime;
        private bool isAttacking;
        private bool sweepCompleted;

        public void Configure(
            PrototypeFloatingWeaponLoadout newLoadout,
            PrototypeFloatingWeaponLoadout.MeleeBladeSettings newSettings,
            GameObject visualPrefab)
        {
            loadout = newLoadout;
            settings = newSettings;
            settings.Validate();
            motor.Configure(
                loadout.OwnerTransform,
                settings.idleOffset,
                settings.followSmoothTime,
                settings.followMaxSpeed,
                settings.bobAmplitude,
                settings.bobFrequency,
                0.35f);
            CreateVisual(visualPrefab);
            transform.position = motor.GetIdlePosition();
        }

        public void SnapToOwner()
        {
            if (loadout == null || settings == null)
            {
                return;
            }

            isAttacking = false;
            sweepCompleted = false;
            attackTarget = null;
            hitTargets.Clear();
            motor.SnapToIdle(transform);
            SetRotation(GetIdleDirection());

            if (visualRoot != null)
            {
                SetVisualPose(0f, 1f);
            }
        }

        private void Update()
        {
            if (loadout == null || settings == null)
            {
                return;
            }

            if (isAttacking)
            {
                UpdateAttack();
                return;
            }

            UpdateIdleMotion();

            if (!loadout.CanAttack || Time.time < nextAttackTime)
            {
                return;
            }

            var target = loadout.FindNearestTarget(loadout.OwnerTransform.position, settings.attackRange);
            if (target != null)
            {
                BeginAttack(target);
            }
        }

        private void BeginAttack(PrototypeCombatTarget target)
        {
            isAttacking = true;
            sweepCompleted = false;
            attackTimer = 0f;
            attackTarget = target;
            attackStartPosition = transform.position;
            attackTargetPosition = target.AimPosition;
            attackDirection = (attackTargetPosition - attackStartPosition).ProjectedOnPlane();
            if (attackDirection.sqrMagnitude <= 0.0001f)
            {
                attackDirection = loadout.OwnerTransform.forward.ProjectedOnPlane();
            }

            if (attackDirection.sqrMagnitude <= 0.0001f)
            {
                attackDirection = Vector3.forward;
            }

            hitTargets.Clear();
            nextAttackTime = Time.time + settings.cooldown;
        }

        private void UpdateIdleMotion()
        {
            motor.SmoothFollow(transform);
            SetRotation(GetIdleDirection());

            if (visualRoot != null)
            {
                SetVisualPose(0f, 1f);
            }
        }

        private void UpdateAttack()
        {
            attackTimer += Time.deltaTime;
            var normalizedTime = Mathf.Clamp01(attackTimer / settings.attackDuration);
            var sweepStartTime = GetSweepStartTime();
            var sweepEndTime = GetSweepEndTime(sweepStartTime);

            if (attackTarget != null && attackTarget.IsTargetable(loadout.Owner))
            {
                attackTargetPosition = attackTarget.AimPosition;
            }

            var sweepOrigin = GetSweepOrigin();
            var side = Vector3.Cross(Vector3.up, attackDirection).normalized;
            var facingDirection = attackDirection;
            var visualAttackAmount = 1f;

            if (normalizedTime <= sweepStartTime)
            {
                var approachTime = EaseOutCubic(normalizedTime / sweepStartTime);
                var lift = Vector3.up * Mathf.Sin(approachTime * Mathf.PI) * 0.18f;
                transform.position = Vector3.Lerp(attackStartPosition, sweepOrigin, approachTime) + lift;

                var windupStartTime = sweepStartTime * 0.65f;
                var windupTime = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(windupStartTime, sweepStartTime, normalizedTime));
                facingDirection = Vector3.Slerp(attackDirection, GetSweepDirection(0f), windupTime).ProjectedOnPlane();
                visualAttackAmount = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(normalizedTime / (sweepStartTime * 0.45f)));
            }
            else if (normalizedTime <= sweepEndTime)
            {
                var sweepTime = Mathf.SmoothStep(0f, 1f, (normalizedTime - sweepStartTime) / (sweepEndTime - sweepStartTime));
                var lateralShift = Mathf.Lerp(-settings.slashArc * 0.2f, settings.slashArc * 0.2f, sweepTime);
                var cutLift = Vector3.up * Mathf.Sin(sweepTime * Mathf.PI) * 0.06f;

                transform.position = sweepOrigin + side * lateralShift + cutLift;
                facingDirection = GetSweepDirection(sweepTime);
                ApplySweepHits(transform.position, sweepTime);

                if (sweepTime >= 1f)
                {
                    sweepCompleted = true;
                }
            }
            else
            {
                if (!sweepCompleted)
                {
                    ApplySweepHits(sweepOrigin, 1f);
                    sweepCompleted = true;
                }

                var returnTime = (normalizedTime - sweepEndTime) / (1f - sweepEndTime);
                var idlePosition = motor.GetIdlePosition();
                var overshootDirection = (idlePosition - sweepOrigin).ProjectedOnPlane();
                if (overshootDirection.sqrMagnitude <= 0.0001f)
                {
                    overshootDirection = -attackDirection;
                }

                var overshootPosition = idlePosition + overshootDirection.normalized * settings.returnOvershoot;
                var returnPosition = Vector3.LerpUnclamped(sweepOrigin, overshootPosition, EaseOutBack(returnTime));
                transform.position = Vector3.Lerp(returnPosition, idlePosition, Mathf.SmoothStep(0f, 1f, returnTime));

                facingDirection = Vector3.Slerp(GetSweepDirection(1f), GetIdleDirection(), Mathf.SmoothStep(0f, 1f, returnTime)).ProjectedOnPlane();
                visualAttackAmount = 1f - Mathf.SmoothStep(0f, 1f, returnTime);
            }

            SetRotation(facingDirection);

            if (visualRoot != null)
            {
                var pulse = 1f + Mathf.Sin(normalizedTime * Mathf.PI) * settings.attackScalePulse;
                SetVisualPose(visualAttackAmount, pulse);
            }

            if (normalizedTime >= 1f)
            {
                isAttacking = false;
                attackTarget = null;
                hitTargets.Clear();
                motor.ResetVelocity();
            }
        }

        private void ApplySweepHits(Vector3 sweepOrigin, float sweepProgress)
        {
            var targets = PrototypeCombatTarget.Targets;
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null || hitTargets.Contains(target) || !target.IsTargetable(loadout.Owner))
                {
                    continue;
                }

                var hitPoint = target.AimPosition;
                if (!IsPointInsideSweepArc(
                    hitPoint,
                    sweepOrigin,
                    attackDirection,
                    sweepProgress,
                    settings.sweepAngleDegrees,
                    settings.sweepReach,
                    settings.hitRadius))
                {
                    continue;
                }

                var hitDirection = (hitPoint - loadout.OwnerTransform.position).ProjectedOnPlane();
                if (hitDirection.sqrMagnitude <= 0.0001f)
                {
                    hitDirection = attackDirection;
                }

                if (target.Health.ApplyDamage(new DamageInfo(settings.damage, loadout.Owner, hitPoint, hitDirection)))
                {
                    hitTargets.Add(target);
                    PrototypeWeaponFeedback.SpawnBurst(hitPoint, settings.hitColor, 1.15f, 18);
                    PrototypeWeaponFeedback.PlaySfx(PrototypeWeaponSfx.MeleeHit, hitPoint, 0.42f);
                }
            }
        }

        private void CreateVisual(GameObject visualPrefab)
        {
            var root = new GameObject("Blade Visual");
            root.transform.SetParent(transform, false);
            visualRoot = root.transform;

            if (visualPrefab == null)
            {
                Debug.LogWarning("Floating melee blade has no visual prefab assigned.", this);
                baseVisualScale = visualRoot.localScale;
                return;
            }

            var visual = Instantiate(visualPrefab, visualRoot);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            baseVisualScale = visualRoot.localScale;
        }

        public static bool IsPointInsideSweepArc(
            Vector3 point,
            Vector3 sweepOrigin,
            Vector3 centerDirection,
            float sweepProgress,
            float halfAngleDegrees,
            float reach,
            float radius)
        {
            centerDirection = centerDirection.ProjectedOnPlane();
            if (centerDirection.sqrMagnitude <= 0.0001f)
            {
                centerDirection = Vector3.forward;
            }

            var toPoint = point - sweepOrigin;
            var verticalDistance = Mathf.Abs(toPoint.y);
            toPoint.y = 0f;

            var safeReach = Mathf.Max(0f, reach);
            var safeRadius = Mathf.Max(0f, radius);
            if (verticalDistance > safeRadius)
            {
                return false;
            }

            var horizontalDistance = toPoint.magnitude;
            if (horizontalDistance > safeReach + safeRadius)
            {
                return false;
            }

            if (horizontalDistance <= safeRadius)
            {
                return true;
            }

            var pointDirection = toPoint / horizontalDistance;
            var signedAngle = Vector3.SignedAngle(centerDirection, pointDirection, Vector3.up);
            var halfAngle = Mathf.Max(0f, halfAngleDegrees);
            var currentAngle = Mathf.Lerp(-halfAngle, halfAngle, Mathf.Clamp01(sweepProgress));
            var anglePadding = Mathf.Rad2Deg * Mathf.Asin(Mathf.Clamp01(safeRadius / horizontalDistance));

            return signedAngle >= -halfAngle - anglePadding
                && signedAngle <= currentAngle + anglePadding;
        }

        private Vector3 GetIdleDirection()
        {
            var ownerForward = motor.OwnerForward;
            if (settings.idleTurnDegrees <= 0f || settings.idleTurnFrequency <= 0f)
            {
                return ownerForward;
            }

            var yaw = Mathf.Sin(Time.time * settings.idleTurnFrequency * Mathf.PI * 2f) * settings.idleTurnDegrees;
            return (Quaternion.AngleAxis(yaw, Vector3.up) * ownerForward).ProjectedOnPlane();
        }

        private Vector3 GetSweepDirection(float sweepProgress)
        {
            var angle = Mathf.Lerp(-settings.sweepAngleDegrees, settings.sweepAngleDegrees, Mathf.Clamp01(sweepProgress));
            return (Quaternion.AngleAxis(angle, Vector3.up) * attackDirection).ProjectedOnPlane();
        }

        private Vector3 GetSweepOrigin()
        {
            var targetPosition = attackTargetPosition;
            var bladeReach = Mathf.Max(0.1f, settings.sweepReach * 0.72f);
            return targetPosition - attackDirection * bladeReach;
        }

        private float GetSweepStartTime()
        {
            return Mathf.Clamp(settings.hitTime, 0.08f, 0.82f);
        }

        private static float GetSweepEndTime(float sweepStartTime)
        {
            return Mathf.Min(0.94f, sweepStartTime + SweepDurationNormalized);
        }

        private void SetRotation(Vector3 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector3.forward;
            }

            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        private void SetVisualPose(float attackAmount, float scaleMultiplier)
        {
            visualRoot.localRotation = Quaternion.Slerp(IdleVisualRotation, AttackVisualRotation, Mathf.Clamp01(attackAmount));
            visualRoot.localScale = baseVisualScale * scaleMultiplier;
        }

        private static float EaseOutCubic(float value)
        {
            value = Mathf.Clamp01(value);
            return 1f - Mathf.Pow(1f - value, 3f);
        }

        private static float EaseOutBack(float value)
        {
            value = Mathf.Clamp01(value);
            const float overshoot = 1.35f;
            return 1f + (overshoot + 1f) * Mathf.Pow(value - 1f, 3f) + overshoot * Mathf.Pow(value - 1f, 2f);
        }
    }
}
