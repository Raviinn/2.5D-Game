using Beast.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Beast.Gameplay
{
    /// <summary>
    /// Player combat state machine: light combos, heavy, dodge (i-frames), block/parry, stagger, death.
    /// While climbing (Traversing) combat is off: the PlayerClimber drives the character and its animation.
    /// Inputs are buffered so presses during an attack chain into the next action.
    /// All tuning lives in ClassData / MovesetData / AttackData assets.
    /// </summary>
    [DefaultExecutionOrder(-10)] // before PlayerMotor, so lunges/dodges move the same frame
    [RequireComponent(typeof(PlayerMotor), typeof(Combatant), typeof(Stamina))]
    public sealed class PlayerCombat : MonoBehaviour, ICharacterAnimationSource
    {
        [SerializeField] ClassData classData;

        [Header("Feel")]
        [SerializeField, Tooltip("Seconds a button press is remembered while busy.")]
        float inputBufferTime = 0.25f;
        [SerializeField] float softAimRange = 4.5f;
        [SerializeField, Tooltip("Degrees either side of the aim direction.")] float softAimAngle = 75f;
        [SerializeField, Tooltip("Lunges stop this far from the target.")] float lungeStopDistance = 1.3f;
        [SerializeField, Tooltip("Holding attack this long (real seconds) queues a charged (heavy) attack.")]
        float chargeTime = 0.35f;

        [Header("Respawn (prototype)")]
        [SerializeField] float respawnDelay = 2f;

        enum State { Free, Attacking, Dodging, Blocking, Staggered, Dead, Traversing }
        enum Command { None, Light, Heavy, Dodge }

        PlayerMotor motor;
        Combatant combatant;
        Stamina stamina;
        LockOnController lockOn;
        PlayerEquipment equipment;
        PlayerStats stats;
        PlayerClimber climber;
        HitStop hitStop;
        AttackExecutor attacks;
        InputAction attackAction, heavyAction, dodgeAction, blockAction, switchStyleAction;

        State state;
        Command buffered;
        float bufferedTime;
        float dodgePressedAt = float.PositiveInfinity;
        bool ignoreDodgeUntilReleased; // Shift that let go of a ledge mustn't also dodge when released
        float attackPressedAt = float.PositiveInfinity;
        bool chargeQueued;
        int movesetIndex;
        MovesetData moveset;
        int comboIndex;
        float lastAttackEndTime = float.NegativeInfinity;
        float dodgeTimer;
        Vector3 dodgeDirection;
        float respawnTimer;
        Vector3 spawnPosition;
        Quaternion spawnRotation;

        public ClassData Class => classData;
        public MovesetData Moveset => moveset;
        public AttackExecutor Attacks => attacks;
        public string StateName => state.ToString();
        public bool IsDead => state == State.Dead;
        /// <summary>Seconds until the prototype respawn (0 when alive).</summary>
        public float RespawnTimeLeft => state == State.Dead ? Mathf.Max(0f, respawnTimer) : 0f;
        /// <summary>Items can't be used mid-attack, mid-dodge, staggered or dead.</summary>
        public bool CanUseItems => state is State.Free or State.Blocking;
        /// <summary>Climbing can only start from free movement (not mid-attack, dodge, block, stagger or death).</summary>
        public bool CanStartTraversal => state == State.Free;
        public bool IsTraversing => state == State.Traversing;

        public CharacterAnim CurrentAnim => state switch
        {
            State.Dead => CharacterAnim.Dead,
            State.Staggered => CharacterAnim.Hurt,
            State.Dodging => CharacterAnim.Dodge,
            State.Blocking => CharacterAnim.Block,
            State.Attacking => CharacterAnim.Attack,
            State.Traversing => climber != null ? climber.CurrentAnim : CharacterAnim.Idle,
            _ => motor.PlanarSpeed > 0.2f ? CharacterAnim.Run : CharacterAnim.Idle,
        };

        public float AnimationSpeed =>
            state == State.Traversing && climber != null ? climber.AnimationSpeed :
            state == State.Free && motor.PlanarSpeed > 0.2f ? Mathf.Max(0.5f, motor.PlanarSpeed / motor.WalkSpeed) : 1f;

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            combatant = GetComponent<Combatant>();
            stamina = GetComponent<Stamina>();
            lockOn = GetComponent<LockOnController>();
            equipment = GetComponent<PlayerEquipment>();
            stats = GetComponent<PlayerStats>();
            climber = GetComponent<PlayerClimber>();

            if (classData == null || classData.Movesets == null || classData.Movesets.Length == 0)
            {
                Debug.LogError("[PlayerCombat] Assign a ClassData with at least one moveset.", this);
                enabled = false;
                return;
            }

            combatant.Initialize(classData.MaxHealth, classData.MaxPoise);
            stamina.Initialize(classData.MaxStamina, classData.StaminaRegen, classData.StaminaRegenDelay);
            combatant.Stamina = stamina;

            attacks = new AttackExecutor(combatant, CombatLayers.OpponentMask(combatant.Team));
            attacks.Landed += OnAttackLanded;
            RefreshMoveset();

            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        void OnEnable()
        {
            combatant.Hit += OnHitReceived;
            combatant.Died += OnDied;
            if (equipment != null) equipment.Changed += RefreshMoveset;
        }

        void OnDisable()
        {
            combatant.Hit -= OnHitReceived;
            combatant.Died -= OnDied;
            if (equipment != null) equipment.Changed -= RefreshMoveset;
        }

        void Start()
        {
            var input = Services.Get<InputService>();
            attackAction = input.Attack;
            heavyAction = input.Heavy;
            dodgeAction = input.Dodge;
            blockAction = input.Block;
            switchStyleAction = input.SwitchStyle;
            hitStop = Services.Get<HitStop>();
            RefreshMoveset(); // equipment has finished its own Awake by now
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            ReadInput();
            motor.AddDisplacement(combatant.ConsumeKnockback(dt));

            switch (state)
            {
                case State.Free: UpdateFree(); break;
                case State.Attacking: UpdateAttacking(dt); break;
                case State.Dodging: UpdateDodging(dt); break;
                case State.Blocking: UpdateBlocking(); break;
                case State.Staggered: if (!combatant.IsStaggered) EnterFree(); break;
                case State.Dead: UpdateDead(dt); break;
                case State.Traversing: buffered = Command.None; break; // presses made while climbing don't fire later
            }
        }

        // ---------- Input buffer ----------

        void ReadInput()
        {
            // Tap attack = light (fires immediately); keep holding = charged heavy, queued as the follow-up.
            if (attackAction.WasPressedThisFrame())
            {
                Buffer(Command.Light);
                attackPressedAt = Time.unscaledTime;
                chargeQueued = false;
            }
            if (!chargeQueued && attackAction.IsPressed() && Time.unscaledTime - attackPressedAt >= chargeTime)
            {
                Buffer(Command.Heavy);
                chargeQueued = true;
            }
            if (heavyAction.WasPressedThisFrame()) Buffer(Command.Heavy);
            // Dodge shares a button with sprint: it fires on release, and only for a short tap.
            if (ignoreDodgeUntilReleased)
            {
                dodgePressedAt = float.PositiveInfinity;
                if (!dodgeAction.IsPressed()) ignoreDodgeUntilReleased = false;
            }
            else
            {
                if (dodgeAction.WasPressedThisFrame()) dodgePressedAt = Time.unscaledTime;
                if (dodgeAction.WasReleasedThisFrame() && Time.unscaledTime - dodgePressedAt < motor.SprintHoldDelay)
                    Buffer(Command.Dodge);
            }
            if (switchStyleAction.WasPressedThisFrame() && state == State.Free)
            {
                // With equipment, the style comes from the weapon: swap weapons. Without, cycle class styles.
                if (equipment != null) equipment.SwapWeapons();
                else SetMoveset(movesetIndex + 1);
            }
        }

        void Buffer(Command command)
        {
            buffered = command;
            bufferedTime = Time.unscaledTime;
        }

        Command PeekBuffered()
        {
            if (buffered != Command.None && Time.unscaledTime - bufferedTime > inputBufferTime)
                buffered = Command.None;
            return buffered;
        }

        bool TryStartBuffered(bool allowAttack, bool allowDodge)
        {
            var command = PeekBuffered();
            if (command == Command.Dodge && allowDodge)
            {
                buffered = Command.None;
                return StartDodge();
            }
            if (command is Command.Light or Command.Heavy && allowAttack)
            {
                buffered = Command.None;
                return StartAttack(command == Command.Light);
            }
            return false;
        }

        // ---------- States ----------

        void UpdateFree()
        {
            if (combatant.IsStaggered) { EnterStagger(); return; }
            if (blockAction.IsPressed()) { EnterBlock(); return; }
            TryStartBuffered(allowAttack: true, allowDodge: true);
        }

        bool StartAttack(bool light)
        {
            AttackData attack;
            if (light)
            {
                var combo = moveset.LightCombo;
                if (combo == null || combo.Length == 0) return false;

                bool chaining = state == State.Attacking || Time.time - lastAttackEndTime <= moveset.ComboResetTime;
                if (!chaining || comboIndex >= combo.Length) comboIndex = 0;
                attack = combo[comboIndex++];
            }
            else
            {
                attack = moveset.Heavy;
                comboIndex = 0;
            }
            if (attack == null) return false;

            BeginAttack(attack);
            return true;
        }

        /// <summary>
        /// Starts a skill's special attack. Allowed when free, blocking, or once the current attack's strike
        /// frames are over (skills cancel recovery, like combos do).
        /// </summary>
        public bool TryStartSkill(AttackData attack)
        {
            if (attack == null) return false;
            bool canStart = state is State.Free or State.Blocking ||
                            (state == State.Attacking && attacks.IsAttacking && attacks.Elapsed >= attacks.Current.ActiveEnd);
            if (!canStart) return false;

            comboIndex = 0;
            buffered = Command.None;
            BeginAttack(attack);
            return true;
        }

        void BeginAttack(AttackData attack)
        {
            float maxLunge = AimAtTarget();
            state = State.Attacking;
            combatant.EndBlock();
            motor.InputMovementEnabled = false;
            motor.SpeedMultiplier = 1f;
            motor.Strafe = false;
            attacks.Begin(attack, maxLunge);
        }

        void UpdateAttacking(float dt)
        {
            if (combatant.IsStaggered) { EnterStagger(); return; }

            motor.AddDisplacement(attacks.Tick(dt));

            if (!attacks.IsAttacking)
            {
                lastAttackEndTime = Time.time;
                EnterFree();
                return;
            }

            var attack = attacks.Current;
            float t = attacks.Elapsed;
            var command = PeekBuffered();

            if (command == Command.Dodge && t >= attack.DodgeCancelAfter)
            {
                TryStartBuffered(allowAttack: false, allowDodge: true);
                return;
            }
            if (command is Command.Light or Command.Heavy && t >= attack.ActiveEnd + attack.ComboCancelDelay)
            {
                TryStartBuffered(allowAttack: true, allowDodge: false);
                return;
            }
            if (t >= attack.ActiveEnd && blockAction.IsPressed())
                EnterBlock();
        }

        bool StartDodge()
        {
            if (!stamina.HasAny) return false;
            stamina.Spend(moveset.DodgeStaminaCost);

            attacks.Cancel();
            combatant.EndBlock();

            Vector3 direction = motor.GetMoveDirection();
            if (direction.sqrMagnitude < 0.01f) direction = -transform.forward; // no input = backstep
            else motor.SnapRotation(direction);

            dodgeDirection = direction.normalized;
            dodgeTimer = 0f;
            state = State.Dodging;
            motor.InputMovementEnabled = false;
            motor.Strafe = false;
            motor.SpeedMultiplier = 1f;
            return true;
        }

        void UpdateDodging(float dt)
        {
            if (combatant.IsStaggered) { EndDodge(); EnterStagger(); return; }

            float previous = dodgeTimer;
            dodgeTimer += dt;
            combatant.Invulnerable = dodgeTimer >= moveset.IFrameStart && dodgeTimer <= moveset.IFrameEnd;

            // Ease-out curve: fast burst, soft landing. Integrates exactly to DodgeDistance.
            float duration = moveset.DodgeDuration;
            float travelled = EaseOut(Mathf.Clamp01(dodgeTimer / duration)) - EaseOut(Mathf.Clamp01(previous / duration));
            motor.AddDisplacement(dodgeDirection * (moveset.DodgeDistance * travelled));

            // After the i-frames, a buffered attack cancels the rest of the dodge (dodge-attack).
            if (dodgeTimer >= moveset.IFrameEnd && PeekBuffered() is Command.Light or Command.Heavy)
            {
                EndDodge();
                TryStartBuffered(allowAttack: true, allowDodge: false);
                return;
            }
            if (dodgeTimer >= duration) EndDodge();
        }

        void EndDodge()
        {
            bool forward = Vector3.Dot(dodgeDirection, transform.forward) > 0.5f;
            EnterFree();
            // Keep some momentum so a forward dodge flows into running instead of stopping dead.
            if (forward) motor.SetPlanarVelocity(dodgeDirection * (motor.SprintSpeed * 0.8f));
        }

        static float EaseOut(float x) => 1f - (1f - x) * (1f - x);

        void EnterBlock()
        {
            attacks.Cancel();
            state = State.Blocking;
            combatant.StartBlock(moveset);
            motor.InputMovementEnabled = true;
            motor.SpeedMultiplier = moveset.BlockMoveSpeedMultiplier;
            motor.Strafe = true;
        }

        void UpdateBlocking()
        {
            if (combatant.IsStaggered) { EnterStagger(); return; } // guard broken
            if (!blockAction.IsPressed()) { EnterFree(); return; }
            TryStartBuffered(allowAttack: true, allowDodge: true); // counter-attack or dodge out of a block
        }

        void EnterFree()
        {
            state = State.Free;
            combatant.EndBlock();
            combatant.Invulnerable = false;
            motor.InputMovementEnabled = true;
            motor.SpeedMultiplier = 1f;
            motor.Strafe = false;
        }

        // ---------- Climbing ----------

        /// <summary>Called by PlayerClimber when it takes over (hang, climb, pull-up).</summary>
        public void BeginTraversal()
        {
            attacks.Cancel();
            buffered = Command.None;
            combatant.EndBlock();
            state = State.Traversing;
            motor.InputMovementEnabled = false;
            if (lockOn != null) lockOn.Release();
        }

        /// <summary>Called by PlayerClimber when it lets go. Ignored if something else (death) already took over.</summary>
        public void EndTraversal()
        {
            if (state != State.Traversing) return;
            // Letting go uses the dodge button: its release must not fire a dodge.
            ignoreDodgeUntilReleased = dodgeAction != null && (dodgeAction.IsPressed() || dodgeAction.WasPressedThisFrame());
            EnterFree();
        }

        void EnterStagger()
        {
            attacks.Cancel();
            combatant.Invulnerable = false;
            buffered = Command.None;
            state = State.Staggered;
            motor.InputMovementEnabled = false;
        }

        void UpdateDead(float dt)
        {
            respawnTimer -= dt;
            if (respawnTimer > 0f) return;

            motor.Teleport(spawnPosition, spawnRotation);
            combatant.Revive();
            stamina.Refill();
            EnterFree();
        }

        // ---------- Targeting ----------

        /// <summary>Faces the lock-on or best soft-aim target. Returns the max lunge so we stop short of it.</summary>
        float AimAtTarget()
        {
            Vector3 input = motor.GetMoveDirection();
            Combatant target = lockOn != null ? lockOn.Target : null;
            if (target == null)
            {
                Vector3 aim = input.sqrMagnitude > 0.01f ? input : transform.forward;
                target = TargetFinder.FindBest(transform.position, aim, softAimRange, softAimAngle,
                    CombatLayers.OpponentMask(combatant.Team), combatant);
            }

            if (target == null)
            {
                motor.SnapRotation(input); // attack where the stick points (no-op without input)
                return float.PositiveInfinity;
            }

            Vector3 toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            motor.SnapRotation(toTarget);
            return Mathf.Max(0f, toTarget.magnitude - lungeStopDistance);
        }

        void SetMoveset(int index)
        {
            var list = classData.Movesets;
            movesetIndex = (index % list.Length + list.Length) % list.Length;
            moveset = list[movesetIndex];
            comboIndex = 0;
        }

        /// <summary>The active weapon's moveset, or the class's current style when no weapon is equipped.</summary>
        void RefreshMoveset()
        {
            var weapon = equipment != null ? equipment.ActiveWeapon : null;
            if (weapon != null && weapon.Moveset != null)
            {
                moveset = weapon.Moveset;
                comboIndex = 0;
            }
            else
            {
                SetMoveset(movesetIndex);
            }
        }

        // ---------- Feedback ----------

        void OnAttackLanded(HitResult result, Combatant target)
        {
            var attack = attacks.Current;
            if (attack == null || result == HitResult.Parried) return;

            float scale = result == HitResult.Blocked ? 0.5f : result == HitResult.Killed ? 1.5f : 1f;
            Feedback(attack.HitStop * scale, attack.CameraShake * scale);
        }

        void OnHitReceived(HitResult result)
        {
            switch (result)
            {
                case HitResult.Hit:
                case HitResult.Killed: Feedback(0.05f, 0.25f); break;
                case HitResult.Blocked: Feedback(0f, 0.08f); break;
                case HitResult.Parried:
                    Feedback(0.15f, 0.3f);
                    if (stats != null) stamina.Restore(stats.Get(StatType.ParryStaminaRestore));
                    break;
                case HitResult.GuardBroken: Feedback(0.08f, 0.3f); break;
            }
        }

        void Feedback(float hitStopSeconds, float shake)
        {
            if (hitStopSeconds > 0f && hitStop != null) hitStop.Trigger(hitStopSeconds);
            if (shake > 0f) EventBus<CameraShakeEvent>.Raise(new CameraShakeEvent(shake, 0.2f));
        }

        void OnDied()
        {
            attacks.Cancel();
            buffered = Command.None;
            state = State.Dead;
            motor.InputMovementEnabled = false;
            respawnTimer = respawnDelay;
            if (lockOn != null) lockOn.Release();
        }

        void OnDrawGizmos()
        {
            if (attacks != null) CombatGizmos.DrawHitbox(attacks);
        }
    }
}
