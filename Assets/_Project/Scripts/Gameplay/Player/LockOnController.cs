using Beast.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Beast.Gameplay
{
    /// <summary>Optional hard lock-on. Toggle with the LockOn button; breaks on death or distance.</summary>
    [RequireComponent(typeof(PlayerMotor), typeof(Combatant))]
    public sealed class LockOnController : MonoBehaviour
    {
        [SerializeField] float acquireRange = 15f;
        [SerializeField] float breakRange = 20f;
        [SerializeField, Tooltip("Max degrees from the camera's view direction.")] float acquireAngle = 60f;

        PlayerMotor motor;
        Combatant self;
        Transform cameraTransform;
        InputAction lockOnAction;

        public Combatant Target { get; private set; }

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            self = GetComponent<Combatant>();
        }

        void Start()
        {
            lockOnAction = Services.Get<InputService>().LockOn;
            cameraTransform = Camera.main != null ? Camera.main.transform : transform;
        }

        void OnDisable() => Release();

        void Update()
        {
            if (lockOnAction.WasPressedThisFrame())
            {
                if (Target != null) Release();
                else Acquire();
            }

            if (Target != null && (Target.IsDead || !Target.isActiveAndEnabled ||
                (Target.transform.position - transform.position).sqrMagnitude > breakRange * breakRange))
                Release();
        }

        /// <summary>Locks onto a particular target (tests, scripted moments).</summary>
        public void LockOnto(Combatant target)
        {
            if (target != null && !target.IsDead) SetTarget(target);
        }

        public void Release()
        {
            if (Target != null) SetTarget(null);
        }

        void Acquire()
        {
            var target = TargetFinder.FindBest(transform.position, cameraTransform.forward, acquireRange, acquireAngle,
                CombatLayers.OpponentMask(self.Team), self);
            if (target != null) SetTarget(target);
        }

        void SetTarget(Combatant target)
        {
            Target = target;
            if (motor != null) motor.FaceTarget = target != null ? target.transform : null;
            EventBus<LockOnChangedEvent>.Raise(new LockOnChangedEvent(target));
        }
    }
}
