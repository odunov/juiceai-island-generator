using UnityEngine;
using UnityEngine.InputSystem;

namespace Islands.Prototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(PrototypeHealth))]
    public sealed class PrototypePlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField]
        [Min(0f)]
        private float moveSpeed = 6f;

        [SerializeField]
        [Min(0f)]
        private float dashDistance = 3.5f;

        [SerializeField]
        [Min(0.01f)]
        private float dashDuration = 0.14f;

        [SerializeField]
        [Min(0f)]
        private float dashCooldown = 0.65f;

        [Header("Aim")]
        [SerializeField]
        private Camera aimCamera;

        [SerializeField]
        private float groundPlaneY;

        [Header("Respawn")]
        [SerializeField]
        private Transform respawnPoint;

        private Rigidbody body;
        private PrototypeHealth health;
        private Vector2 moveInput;
        private Vector3 facingDirection = Vector3.forward;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private Vector3 dashDirection;
        private float dashTimer;
        private float nextDashTime;

        public bool IsDashing => dashTimer > 0f;

        public bool IsDead => health != null && health.IsDead;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            health = GetComponent<PrototypeHealth>();
            PrototypeFloatingHealthBar.EnsureFor(health, PrototypeFloatingHealthBar.Preset.Player);
            ConfigureBody();
            CaptureSpawn();
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.Died += HandleDied;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Died -= HandleDied;
            }
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            dashDistance = Mathf.Max(0f, dashDistance);
            dashDuration = Mathf.Max(0.01f, dashDuration);
            dashCooldown = Mathf.Max(0f, dashCooldown);
        }

        private void Reset()
        {
            body = GetComponent<Rigidbody>();
            ConfigureBody();

            var capsule = GetComponent<CapsuleCollider>();
            capsule.radius = 0.45f;
            capsule.height = 2f;
            capsule.center = Vector3.zero;
        }

        private void Update()
        {
            if (IsDead)
            {
                moveInput = Vector2.zero;

                if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                {
                    Respawn();
                }

                return;
            }

            moveInput = ReadMoveInput();
            UpdateFacing();

            if (DashWasPressed() && Time.time >= nextDashTime)
            {
                StartDash();
            }
        }

        private void FixedUpdate()
        {
            if (IsDead)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                return;
            }

            var velocity = GetCurrentPlanarVelocity();
            var targetPosition = body.position + velocity * Time.fixedDeltaTime;
            targetPosition.y = body.position.y;
            body.MovePosition(targetPosition);

            if (facingDirection.sqrMagnitude > 0.0001f)
            {
                body.MoveRotation(Quaternion.LookRotation(facingDirection, Vector3.up));
            }
        }

        public void CaptureSpawn()
        {
            if (respawnPoint != null)
            {
                spawnPosition = respawnPoint.position;
                spawnRotation = respawnPoint.rotation;
            }
            else
            {
                spawnPosition = transform.position;
                spawnRotation = transform.rotation;
            }
        }

        public void Respawn()
        {
            RestoreHealth();
            TeleportTo(spawnPosition, spawnRotation);
        }

        public void RestoreHealth()
        {
            health = health != null ? health : GetComponent<PrototypeHealth>();
            if (health == null)
            {
                return;
            }

            health.ResetHealth();
            nextDashTime = 0f;
        }

        public void TeleportTo(Vector3 targetPosition, Quaternion targetRotation, bool captureSpawnAtDestination = false)
        {
            body = body != null ? body : GetComponent<Rigidbody>();
            dashTimer = 0f;

            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.position = targetPosition;
                body.rotation = targetRotation;
            }

            transform.SetPositionAndRotation(targetPosition, targetRotation);
            facingDirection = transform.forward.ProjectedOnPlane();
            if (facingDirection.sqrMagnitude <= 0.0001f)
            {
                facingDirection = Vector3.forward;
            }

            SnapFloatingWeapons();

            if (captureSpawnAtDestination)
            {
                CaptureSpawn();
            }
        }

        private void ConfigureBody()
        {
            if (body == null)
            {
                return;
            }

            body.useGravity = false;
            body.isKinematic = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.FreezeRotation;
        }

        private Vector2 ReadMoveInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return Vector2.zero;
            }

            var input = Vector2.zero;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            {
                input.y += 1f;
            }

            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            {
                input.y -= 1f;
            }

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                input.x += 1f;
            }

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                input.x -= 1f;
            }

            return input.sqrMagnitude > 1f ? input.normalized : input;
        }

        private bool DashWasPressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.leftShiftKey.wasPressedThisFrame);
        }

        private void UpdateFacing()
        {
            var cameraToUse = aimCamera != null ? aimCamera : Camera.main;
            var mouse = Mouse.current;
            if (cameraToUse == null || mouse == null)
            {
                return;
            }

            var ray = cameraToUse.ScreenPointToRay(mouse.position.ReadValue());
            var plane = new Plane(Vector3.up, new Vector3(0f, groundPlaneY, 0f));
            if (!plane.Raycast(ray, out var distance))
            {
                return;
            }

            var aimPoint = ray.GetPoint(distance);
            var direction = aimPoint - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.0001f)
            {
                facingDirection = direction.normalized;
            }
        }

        private void StartDash()
        {
            dashDirection = new Vector3(moveInput.x, 0f, moveInput.y);
            if (dashDirection.sqrMagnitude <= 0.0001f)
            {
                dashDirection = facingDirection;
            }

            dashDirection = dashDirection.sqrMagnitude > 0.0001f ? dashDirection.normalized : Vector3.forward;
            dashTimer = dashDuration;
            nextDashTime = Time.time + dashCooldown;
        }

        private Vector3 GetCurrentPlanarVelocity()
        {
            if (dashTimer > 0f)
            {
                dashTimer = Mathf.Max(0f, dashTimer - Time.fixedDeltaTime);
                return dashDirection * (dashDistance / dashDuration);
            }

            var move = new Vector3(moveInput.x, 0f, moveInput.y);
            return move.sqrMagnitude > 1f ? move.normalized * moveSpeed : move * moveSpeed;
        }

        private void HandleDied(PrototypeHealth deadHealth)
        {
            moveInput = Vector2.zero;
            dashTimer = 0f;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        private void SnapFloatingWeapons()
        {
            var loadouts = GetComponentsInChildren<PrototypeFloatingWeaponLoadout>(true);
            foreach (var loadout in loadouts)
            {
                if (loadout != null)
                {
                    loadout.SnapWeaponsToOwner();
                }
            }
        }
    }

    internal static class PrototypeVectorExtensions
    {
        public static Vector3 ProjectedOnPlane(this Vector3 value)
        {
            value.y = 0f;
            return value.sqrMagnitude > 0.0001f ? value.normalized : Vector3.zero;
        }
    }
}
