using Beast.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Beast.Gameplay
{
    /// <summary>
    /// Lightweight orbit camera with collision, lock-on framing and screen shake.
    /// Can be swapped for Cinemachine later without touching gameplay code.
    /// </summary>
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector3 pivotOffset = new(0f, 1.5f, 0f);
        [SerializeField] float distance = 5f;

        [Header("Look")]
        [SerializeField, Tooltip("Degrees per pixel of mouse movement.")] float mouseSensitivity = 0.12f;
        [SerializeField, Tooltip("Degrees per second at full stick.")] float stickSensitivity = 180f;
        [SerializeField] float minPitch = -30f;
        [SerializeField] float maxPitch = 70f;

        [Header("Lock-On")]
        [SerializeField, Tooltip("How quickly the camera swings behind the player toward the target.")]
        float lockOnSharpness = 10f;

        [Header("Collision")]
        [SerializeField] float collisionRadius = 0.25f;
        [SerializeField] LayerMask collisionMask = ~0;

        InputAction look;
        SettingsService settings;
        float yaw;
        float pitch = 15f;
        Transform lockTarget;
        float shakeStrength;
        float shakeDuration;
        float shakeTimeLeft;

        // Invisible world-edge walls live on Ignore Raycast: the camera never bumps into them.
        int Mask => collisionMask & ~(1 << 2);

        void OnEnable()
        {
            EventBus<LockOnChangedEvent>.Subscribe(OnLockOnChanged);
            EventBus<CameraShakeEvent>.Subscribe(OnCameraShake);
        }

        void OnDisable()
        {
            EventBus<LockOnChangedEvent>.Unsubscribe(OnLockOnChanged);
            EventBus<CameraShakeEvent>.Unsubscribe(OnCameraShake);
        }

        void Start()
        {
            look = Services.Get<InputService>().Look;
            Services.TryGet(out settings);
            if (target != null) yaw = target.eulerAngles.y;
        }

        void LateUpdate()
        {
            if (target == null) return;

            Vector2 delta = look.ReadValue<Vector2>();
            // Mouse delta is already per-frame; stick values need scaling by time.
            bool mouse = look.activeControl?.device is Pointer;
            if (mouse) delta *= mouseSensitivity;
            else delta *= stickSensitivity * Time.deltaTime;
            if (settings != null)
            {
                var options = settings.Current;
                delta *= mouse ? options.mouseSensitivity : options.stickSensitivity;
                if (options.invertY) delta.y = -delta.y;
            }

            if (lockTarget != null)
            {
                Vector3 toTarget = lockTarget.position - target.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.01f)
                {
                    float desiredYaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
                    yaw = Mathf.LerpAngle(yaw, desiredYaw, 1f - Mathf.Exp(-lockOnSharpness * Time.unscaledDeltaTime));
                }
            }
            else
            {
                yaw += delta.x;
            }
            pitch = Mathf.Clamp(pitch - delta.y, minPitch, maxPitch);

            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = target.position + pivotOffset;
            Vector3 back = rotation * Vector3.back;

            // SphereCast ignores colliders it starts inside, so the player's own collider isn't hit.
            float actualDistance = distance;
            if (Physics.SphereCast(pivot, collisionRadius, back, out var hit, distance, Mask, QueryTriggerInteraction.Ignore))
                actualDistance = hit.distance;

            transform.SetPositionAndRotation(pivot + back * actualDistance + GetShakeOffset(), rotation);
        }

        Vector3 GetShakeOffset()
        {
            if (shakeTimeLeft <= 0f) return Vector3.zero;
            shakeTimeLeft -= Time.unscaledDeltaTime; // keeps shaking during hit-stop
            return Random.insideUnitSphere * CurrentShake;
        }

        float CurrentShake => shakeTimeLeft > 0f ? shakeStrength * (shakeTimeLeft / shakeDuration) : 0f;

        void OnCameraShake(CameraShakeEvent evt)
        {
            float strength = evt.Strength * (settings != null ? settings.Current.cameraShake : 1f);
            if (strength <= 0f || strength < CurrentShake || evt.Duration <= 0f) return;
            shakeStrength = strength;
            shakeDuration = shakeTimeLeft = evt.Duration;
        }

        void OnLockOnChanged(LockOnChangedEvent evt) =>
            lockTarget = evt.Target != null ? evt.Target.transform : null;
    }
}
