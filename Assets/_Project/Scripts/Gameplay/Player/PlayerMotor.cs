using Beast.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Beast.Gameplay
{
    /// <summary>
    /// Camera-relative third-person movement: walk, sprint, jump, gravity.
    /// Combat code can take over movement (attacks, dodges) through the public API, and climbing can
    /// suspend it entirely (no gravity, no input) while it moves the character itself.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] float walkSpeed = 4.5f;
        [SerializeField] float sprintSpeed = 7.5f;
        [SerializeField, Tooltip("How fast speed changes, in m/s per second.")] float acceleration = 40f;
        [SerializeField, Tooltip("Degrees per second.")] float turnSpeed = 900f;
        [SerializeField, Tooltip("Real-time seconds the sprint button must be held before sprinting. A shorter tap dodges instead.")]
        float sprintHoldDelay = 0.2f;

        [Header("Jump & Gravity")]
        [SerializeField] float jumpHeight = 1.2f;
        [SerializeField] float gravity = -25f;

        [Header("References")]
        [SerializeField, Tooltip("Defaults to the main camera.")] Transform cameraTransform;

        CharacterController controller;
        InputAction move;
        InputAction sprint;
        InputAction jump;
        Vector3 planarVelocity;
        Vector3 pendingDisplacement;
        float verticalVelocity;
        float sprintPressedAt = float.PositiveInfinity;

        /// <summary>False while combat controls movement (attacking, dodging, staggered).</summary>
        public bool InputMovementEnabled { get; set; } = true;
        public float SpeedMultiplier { get; set; } = 1f;
        /// <summary>When set, the player faces this target while moving (lock-on).</summary>
        public Transform FaceTarget { get; set; }
        /// <summary>When true and there's no FaceTarget, the player faces the camera direction (blocking).</summary>
        public bool Strafe { get; set; }
        public float WalkSpeed => walkSpeed;
        public float SprintSpeed => sprintSpeed;
        public float PlanarSpeed => planarVelocity.magnitude;
        /// <summary>Tap/hold boundary shared with PlayerCombat: released sooner = dodge, held longer = sprint.</summary>
        public float SprintHoldDelay => sprintHoldDelay;
        public bool IsSprinting { get; private set; }
        /// <summary>True while climbing controls the character: no input movement, no gravity.</summary>
        public bool Suspended { get; set; }
        public bool IsGrounded => controller.isGrounded;
        public float VerticalVelocity => verticalVelocity;
        public float Radius => controller.radius;
        public float Height => controller.height;
        /// <summary>Raw stick / WASD input (x = right, y = forward), not camera-relative.</summary>
        public Vector2 RawInput => move != null ? move.ReadValue<Vector2>() : Vector2.zero;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        }

        void Start()
        {
            var input = Services.Get<InputService>();
            move = input.Move;
            sprint = input.Sprint;
            jump = input.Jump;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (sprint.WasPressedThisFrame()) sprintPressedAt = Time.unscaledTime;
            IsSprinting = sprint.IsPressed() && Time.unscaledTime - sprintPressedAt >= sprintHoldDelay;

            if (Suspended)
            {
                planarVelocity = Vector3.zero;
                verticalVelocity = 0f;
                pendingDisplacement = Vector3.zero;
                return;
            }

            Vector3 desired = Vector3.zero;
            if (InputMovementEnabled)
            {
                desired = GetMoveDirection();
                float speed = (IsSprinting ? sprintSpeed : walkSpeed) * SpeedMultiplier;
                planarVelocity = Vector3.MoveTowards(planarVelocity, desired * speed, acceleration * dt);
            }
            else
            {
                planarVelocity = Vector3.zero;
            }

            if (controller.isGrounded)
            {
                if (verticalVelocity < 0f) verticalVelocity = -2f; // keeps the controller snapped to slopes
                if (InputMovementEnabled && jump.WasPressedThisFrame())
                    verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            verticalVelocity += gravity * dt;

            controller.Move((planarVelocity + Vector3.up * verticalVelocity) * dt + pendingDisplacement);
            pendingDisplacement = Vector3.zero;

            if (InputMovementEnabled) UpdateFacing(desired, dt);
        }

        /// <summary>Camera-relative move direction from input (length 0..1).</summary>
        public Vector3 GetMoveDirection()
        {
            Vector2 input = move.ReadValue<Vector2>();
            if (input.sqrMagnitude < 0.0001f || cameraTransform == null) return Vector3.zero;

            Vector3 forward = cameraTransform.forward;
            forward.y = 0f;
            forward.Normalize();
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);

            return Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
        }

        /// <summary>Extra movement applied on the next Update (lunges, dodges, knockback).</summary>
        public void AddDisplacement(Vector3 displacement) => pendingDisplacement += displacement;

        public void SetPlanarVelocity(Vector3 velocity)
        {
            velocity.y = 0f;
            planarVelocity = velocity;
        }

        public void SnapRotation(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(direction);
        }

        /// <summary>Moves with collisions, ignoring speed and gravity (climbing).</summary>
        public void MoveDirect(Vector3 displacement)
        {
            if (controller.enabled) controller.Move(displacement);
        }

        /// <summary>Places the character exactly, ignoring collisions (climbing over an edge).</summary>
        public void Place(Vector3 position)
        {
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
        }

        /// <summary>Starts a jump-like arc, e.g. leaping off a wall.</summary>
        public void Launch(Vector3 velocity)
        {
            planarVelocity = new Vector3(velocity.x, 0f, velocity.z);
            verticalVelocity = velocity.y;
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            controller.enabled = true;
            planarVelocity = Vector3.zero;
            pendingDisplacement = Vector3.zero;
            verticalVelocity = 0f;
        }

        void UpdateFacing(Vector3 moveDirection, float dt)
        {
            Vector3 face = moveDirection;
            if (FaceTarget != null) face = FaceTarget.position - transform.position;
            else if (Strafe && cameraTransform != null) face = cameraTransform.forward;
            face.y = 0f;

            if (face.sqrMagnitude < 0.0001f) return;
            var targetRotation = Quaternion.LookRotation(face, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * dt);
        }
    }
}
