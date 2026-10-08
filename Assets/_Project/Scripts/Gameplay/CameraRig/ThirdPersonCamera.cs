using Beast.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Beast.Gameplay
{
    /// <summary>
    /// Lightweight orbit camera with collision, lock-on framing and screen shake.
    /// During a conversation it glides to an over-the-shoulder shot: behind and to the right of the player, at eye
    /// height, looking at the NPC (the player's back in the left foreground), and glides back afterwards.
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

        // Conversation shot (constants, so a scene saved with older values can't keep a bad framing).
        const float DialogueBlendTime = 0.7f;           // seconds to glide in and out
        const float DialogueBehind = 2.5f;              // metres behind the player
        const float DialogueRight = 1.0f;               // metres to the player's right
        const float DialogueEyeHeight = 1.7f;           // metres above the player's feet
        const float DialogueLookHeight = 0.95f;         // aim a little below the NPC's chest, so they stand clear of the text
        const float DialogueFieldOfView = 45f;

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
        DialogueRunner dialogue;
        Camera cam;
        float baseFieldOfView;
        float dialogueBlend;
        Vector3 dialoguePosition;
        Quaternion dialogueRotation;
        float conversationYaw;
        bool inConversation;

        /// <summary>0 = normal orbit, 1 = fully in the conversation shot.</summary>
        public float ConversationBlend => dialogueBlend;

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
            cam = GetComponent<Camera>();
            if (cam != null) baseFieldOfView = cam.fieldOfView;
        }

        void LateUpdate()
        {
            if (target == null) return;

            if (dialogue == null) Services.TryGet(out dialogue);
            var partner = dialogue != null && dialogue.IsActive ? dialogue.ConversationPartner : null;
            if (partner != null) AimConversationShot(partner);
            else if (inConversation)
            {
                // The shot is still fully on screen here, so the orbit can jump behind the player unseen.
                yaw = conversationYaw;
                pitch = 12f;
            }
            inConversation = partner != null;
            // Unscaled: the game is paused during conversations.
            dialogueBlend = Mathf.MoveTowards(dialogueBlend, partner != null ? 1f : 0f, Time.unscaledDeltaTime / DialogueBlendTime);

            Vector2 delta = partner != null ? Vector2.zero : look.ReadValue<Vector2>();
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

            Vector3 position = pivot + back * actualDistance;
            if (dialogueBlend > 0f)
            {
                float t = Mathf.SmoothStep(0f, 1f, dialogueBlend);
                position = Vector3.Lerp(position, dialoguePosition, t);
                rotation = Quaternion.Slerp(rotation, dialogueRotation, t);
            }
            if (cam != null) cam.fieldOfView = Mathf.Lerp(baseFieldOfView, DialogueFieldOfView, Mathf.SmoothStep(0f, 1f, dialogueBlend));
            transform.SetPositionAndRotation(position + GetShakeOffset(), rotation);
        }

        /// <summary>
        /// The over-the-shoulder shot for a conversation with <paramref name="partner"/>. The orbit's yaw is turned to
        /// match when the conversation ends, so leaving it glides back to a camera behind the player looking at the NPC.
        /// </summary>
        void AimConversationShot(Transform partner)
        {
            Vector3 from = target.position;
            Vector3 forward = partner.position - from;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = target.forward;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            Vector3 head = new Vector3(from.x, FeetY(target) + DialogueEyeHeight, from.z);
            Vector3 wanted = head - forward * DialogueBehind + right * DialogueRight;
            // Keep the shot out of walls: pull it in toward the player's head if something's in the way.
            Vector3 offset = wanted - head;
            if (Physics.SphereCast(head, collisionRadius, offset.normalized, out var hit, offset.magnitude, Mask, QueryTriggerInteraction.Ignore))
                wanted = head + offset.normalized * hit.distance;

            Vector3 lookAt = new Vector3(partner.position.x, FeetY(partner) + DialogueLookHeight, partner.position.z);
            dialoguePosition = wanted;
            dialogueRotation = Quaternion.LookRotation(lookAt - wanted);
            conversationYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        }

        /// <summary>Ground level under a character (their transform sits at the middle of their collider).</summary>
        static float FeetY(Transform character)
        {
            if (character.TryGetComponent(out CharacterController body))
                return character.position.y + body.center.y - body.height * 0.5f;
            return character.TryGetComponent(out Collider collider) ? collider.bounds.min.y : character.position.y;
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
