using System.Collections.Generic;
using Beast.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Beast.Gameplay
{
    /// <summary>
    /// Melee AI: Idle → Chase → Attack (telegraphed wind-up) → cooldown, with stagger and respawn.
    /// - Paths around buildings, walls and trees on the WorldNavigation mesh (walks straight if there's none).
    /// - Takes turns: only EnemyDirector.MaxAttackers enemies close in at once; the rest hold a ring around the
    ///   player, circling, and step in when a turn frees up.
    /// - Can't reach you (you climbed a wall)? Waits at the closest spot, then gives up and walks home.
    /// - Never strays further than LeashRange from home; walking home ignores the player and heals on arrival.
    /// - Bolder at night (DayNightCycle): hits harder, spots you from further away, moves faster, drops more.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(Combatant))]
    public sealed class EnemyController : MonoBehaviour, ICharacterAnimationSource
    {
        /// <summary>All live enemy controllers (for HUD and future systems). Avoids FindObjectsOfType.</summary>
        public static readonly List<EnemyController> Active = new();

        const float RepathInterval = 0.25f;
        const float ReachCheckInterval = 0.5f;
        const float TokenTimeout = 5f;          // a turn that hasn't led to a swing is handed on
        const float TurnRest = 0.6f;            // a turn ends this long after the swing finishes
        const float Separation = 1.4f;          // enemies keep this far apart
        const float MaxAttackHeight = 1.2f;     // can't hit a player standing on something taller than this

        [SerializeField] EnemyData data;
        [SerializeField] float gravity = -25f;
        [SerializeField, Tooltip("Lunges stop this far from the player.")] float lungeStopDistance = 1.2f;

        enum State { Idle, Chase, Attack, Return, Staggered, Dead }

        CharacterController controller;
        Combatant combatant;
        AttackExecutor attacks;
        Renderer[] renderers;
        Combatant player;
        WorldNavigation navigation;
        DayNightCycle dayNight;

        State state;
        float cooldown;
        int nextAttack;
        float verticalVelocity;
        float respawnTimer;
        float planarSpeed;
        Vector3 spawnPosition;
        Quaternion spawnRotation;

        // Pathing
        NavMeshPath path;
        readonly Vector3[] corners = new Vector3[32];
        int cornerCount, cornerIndex;
        float nextRepath;
        Vector3 pathTarget;
        NavMeshPath reachPath;
        float nextReachCheck;
        bool playerReachable = true;
        float unreachableTime;

        // Turn-taking
        float tokenTakenAt;
        float tokenReleaseAt = -1f;
        float strafeSign = 1f;
        float nextStrafeSwitch;

        public EnemyData Data => data;
        public Combatant Combatant => combatant;
        public string StateName => state.ToString();
        public AttackExecutor Attacks => attacks;
        public Vector3 Home => spawnPosition;
        /// <summary>True while this enemy holds an attack turn.</summary>
        public bool HasAttackTurn => EnemyDirector.HasToken(this);
        /// <summary>False when the last check found no walkable way to the player.</summary>
        public bool PlayerReachable => playerReachable;
        /// <summary>Night: tougher and more alert (see EnemyData's Night settings). Training dummies never are.</summary>
        public bool Emboldened { get; private set; }
        float AggroRange => data.AggroRange * (Emboldened ? 1f + data.NightAggroBonus : 1f);
        float MoveSpeed => data.MoveSpeed * (Emboldened ? 1f + data.NightSpeedBonus : 1f);

        public CharacterAnim CurrentAnim =>
            state == State.Dead ? CharacterAnim.Dead :
            state == State.Staggered ? CharacterAnim.Hurt :
            attacks != null && attacks.IsAttacking ? CharacterAnim.Attack :
            planarSpeed > 0.2f ? CharacterAnim.Run :
            CharacterAnim.Idle;

        public float AnimationSpeed =>
            CurrentAnim == CharacterAnim.Run && data != null ? Mathf.Max(0.5f, planarSpeed / MoveSpeed) : 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Active.Clear();

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            combatant = GetComponent<Combatant>();
            renderers = GetComponentsInChildren<Renderer>();
            path = new NavMeshPath();
            reachPath = new NavMeshPath();

            if (data == null)
            {
                Debug.LogError($"[Enemy] {name} has no EnemyData.", this);
                enabled = false;
                return;
            }

            combatant.Initialize(data.MaxHealth, data.MaxPoise);
            attacks = new AttackExecutor(combatant, CombatLayers.OpponentMask(combatant.Team));
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            cooldown = data.AttackCooldown;
            strafeSign = Random.value < 0.5f ? -1f : 1f;
        }

        void OnEnable()
        {
            Active.Add(this);
            combatant.Died += OnDied;
        }

        void OnDisable()
        {
            Active.Remove(this);
            combatant.Died -= OnDied;
            EnemyDirector.ReleaseToken(this);
        }

        void Start()
        {
            var playerObject = GameObject.FindWithTag("Player");
            if (playerObject != null) player = playerObject.GetComponent<Combatant>();
            Services.TryGet(out navigation);
            Services.TryGet(out dayNight);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (state == State.Dead) { UpdateDead(dt); return; }
            UpdateNight();

            Vector3 displacement = combatant.ConsumeKnockback(dt);

            if (combatant.IsStaggered)
            {
                if (state != State.Staggered) attacks.Cancel();
                state = State.Staggered;
                planarSpeed = 0f;
            }
            else
            {
                if (state == State.Staggered) state = State.Chase;
                if (!data.IsTrainingDummy)
                {
                    Vector3 move = Think(dt);
                    planarSpeed = attacks.IsAttacking ? 0f : new Vector2(move.x, move.z).magnitude / dt;
                    displacement += move;
                }
            }
            combatant.Telegraphing = attacks.CurrentPhase == AttackExecutor.Phase.Startup;

            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * dt;
            displacement.y += verticalVelocity * dt;
            controller.Move(displacement);
        }

        /// <summary>Moves the enemy (and optionally its home), e.g. for spawners and tests.</summary>
        public void Warp(Vector3 position, bool makeHome)
        {
            controller.enabled = false;
            transform.position = position;
            controller.enabled = state != State.Dead;
            if (makeHome) spawnPosition = position;
            cornerCount = 0;
            nextRepath = 0f;
        }

        void UpdateNight()
        {
            if (dayNight == null) Services.TryGet(out dayNight);
            bool bold = dayNight != null && dayNight.IsNight && !data.IsTrainingDummy;
            if (bold == Emboldened) return;
            Emboldened = bold;
            combatant.DamageMultiplier = bold ? 1f + data.NightDamageBonus : 1f;
        }

        // ---------- Decisions ----------

        Vector3 Think(float dt)
        {
            if (navigation == null) Services.TryGet(out navigation);

            if (attacks.IsAttacking)
            {
                // Track the player during the wind-up only, so a late sidestep dodges the swing.
                if (attacks.CurrentPhase == AttackExecutor.Phase.Startup) FacePlayer(dt, 0.5f);
                Vector3 lunge = attacks.Tick(dt);
                if (!attacks.IsAttacking)
                {
                    cooldown = data.AttackCooldown;
                    state = State.Chase;
                    tokenReleaseAt = Time.time + TurnRest;
                }
                return lunge;
            }

            cooldown -= dt;
            if (tokenReleaseAt >= 0f && Time.time >= tokenReleaseAt) EndTurn();

            if (state == State.Return) return UpdateReturn(dt);

            if (player == null || player.IsDead)
            {
                EndTurn();
                return Flat(transform.position - spawnPosition).sqrMagnitude > 1f ? GoHome() : Idle();
            }

            Vector3 toPlayer = Flat(player.transform.position - transform.position);
            float distance = toPlayer.magnitude;

            if (state == State.Idle)
            {
                if (distance > AggroRange) return Vector3.zero;
                state = State.Chase;
                unreachableTime = 0f;
            }

            // Chasing.
            CheckReachable();
            unreachableTime = playerReachable ? 0f : unreachableTime + dt;
            if (distance > AggroRange * 1.5f || Flat(transform.position - spawnPosition).magnitude > data.LeashRange ||
                unreachableTime > data.GiveUpAfter)
                return GoHome();

            FacePlayer(dt, 1f);
            float heightGap = Mathf.Abs(player.transform.position.y - transform.position.y);

            bool hasTurn = EnemyDirector.HasToken(this);
            if (!hasTurn && cooldown <= 0f && playerReachable && distance <= data.HoldDistance + 4f && EnemyDirector.TryTakeToken(this))
            {
                hasTurn = true;
                tokenTakenAt = Time.time;
                tokenReleaseAt = -1f;
            }
            if (hasTurn && tokenReleaseAt < 0f && Time.time - tokenTakenAt > TokenTimeout)
            {
                EndTurn(); // took too long to land a swing: let someone else try
                hasTurn = false;
            }

            Vector3 move;
            if (hasTurn || !playerReachable)
            {
                bool canAttack = hasTurn && data.Attacks != null && data.Attacks.Length > 0 && cooldown <= 0f &&
                                 distance <= data.AttackRange && heightGap < MaxAttackHeight && Vector3.Angle(transform.forward, toPlayer) < 30f;
                if (canAttack)
                {
                    var attack = data.Attacks[nextAttack++ % data.Attacks.Length];
                    attacks.Begin(attack, Mathf.Max(0f, distance - lungeStopDistance));
                    state = State.Attack;
                    return Vector3.zero;
                }
                // With a turn: close in. Without a way to the player: wait at the closest reachable spot.
                move = Steer(player.transform.position, MoveSpeed, dt, hasTurn ? data.AttackRange * 0.8f : 0.5f);
            }
            else
            {
                move = HoldRing(toPlayer, distance, dt);
            }
            return move + SeparationPush(dt);
        }

        /// <summary>Waiting for a turn: keep HoldDistance from the player, circling slowly.</summary>
        Vector3 HoldRing(Vector3 toPlayer, float distance, float dt)
        {
            // Too far: approach along the path first.
            if (distance > data.HoldDistance + 1.5f)
                return Steer(player.transform.position, MoveSpeed, dt, data.HoldDistance);

            if (Time.time >= nextStrafeSwitch)
            {
                nextStrafeSwitch = Time.time + Random.Range(1.8f, 3.5f);
                if (Random.value < 0.5f) strafeSign = -strafeSign;
            }
            Vector3 radial = distance > 0.01f ? -toPlayer / distance : -transform.forward; // away from the player
            Vector3 tangent = Vector3.Cross(Vector3.up, radial) * strafeSign;
            float radialError = data.HoldDistance - distance; // positive = too close
            Vector3 velocity = radial * Mathf.Clamp(radialError * 2f, -1f, 1f) * MoveSpeed * 0.6f + tangent * MoveSpeed * 0.3f;

            // Don't strafe into walls: if the way ahead is blocked, turn around.
            if (Physics.Raycast(transform.position, velocity.normalized, controller.radius + 0.4f, ~(CombatLayers.PlayerMask | CombatLayers.EnemyMask), QueryTriggerInteraction.Ignore))
            {
                strafeSign = -strafeSign;
                nextStrafeSwitch = Time.time + 2f;
            }
            return velocity * dt;
        }

        Vector3 SeparationPush(float dt)
        {
            Vector3 push = Vector3.zero;
            foreach (var other in Active)
            {
                if (other == this || other.state == State.Dead) continue;
                Vector3 away = Flat(transform.position - other.transform.position);
                float d = away.magnitude;
                if (d < 0.001f) away = transform.right; // exactly on top of each other: split sideways
                if (d < Separation) push += away.normalized * (Separation - d);
            }
            return push * (MoveSpeed * 0.8f * dt);
        }

        void CheckReachable()
        {
            if (Time.time < nextReachCheck) return;
            nextReachCheck = Time.time + ReachCheckInterval;
            if (navigation == null || !navigation.IsReady)
            {
                playerReachable = Mathf.Abs(player.transform.position.y - transform.position.y) < MaxAttackHeight;
                return;
            }
            playerReachable = navigation.TryGetPath(transform.position, player.transform.position, reachPath, out bool complete) && complete;
        }

        Vector3 Idle()
        {
            state = State.Idle;
            return Vector3.zero;
        }

        Vector3 GoHome()
        {
            EndTurn();
            state = State.Return;
            unreachableTime = 0f;
            cornerCount = 0;
            nextRepath = 0f;
            return Vector3.zero;
        }

        Vector3 UpdateReturn(float dt)
        {
            if (Flat(transform.position - spawnPosition).magnitude < 0.8f)
            {
                combatant.Heal(combatant.MaxHealth); // back home: wounds tended, so kiting a bandit away doesn't pay
                transform.rotation = spawnRotation;
                return Idle();
            }
            FaceDirection(Flat(spawnPosition - transform.position), dt);
            return Steer(spawnPosition, MoveSpeed * 0.8f, dt, 0.3f);
        }

        void EndTurn()
        {
            EnemyDirector.ReleaseToken(this);
            tokenReleaseAt = -1f;
        }

        // ---------- Moving ----------

        /// <summary>Displacement toward 'target' along the navigation mesh (straight line if there's none).</summary>
        Vector3 Steer(Vector3 target, float speed, float dt, float stopDistance)
        {
            Vector3 position = transform.position;
            Vector3 next = target;
            float remaining = Flat(target - position).magnitude;

            if (navigation != null && navigation.IsReady)
            {
                if (Time.time >= nextRepath || (target - pathTarget).sqrMagnitude > 0.5f)
                {
                    nextRepath = Time.time + RepathInterval;
                    pathTarget = target;
                    cornerCount = navigation.TryGetPath(position, target, path, out _) ? path.GetCornersNonAlloc(corners) : 0;
                    cornerIndex = 1;
                }
                if (cornerCount > 0)
                {
                    while (cornerIndex < cornerCount - 1 && Flat(corners[cornerIndex] - position).magnitude < 0.3f) cornerIndex++;
                    Vector3 end = corners[cornerCount - 1];
                    next = cornerIndex < cornerCount ? corners[cornerIndex] : end;
                    // Distance left along the path.
                    remaining = Flat(next - position).magnitude;
                    for (int i = cornerIndex; i < cornerCount - 1; i++) remaining += Flat(corners[i + 1] - corners[i]).magnitude;
                    // A path that stops short of the target (it can't be reached) is walked to its very end.
                    if (Flat(target - end).magnitude > 0.6f) stopDistance = Mathf.Min(stopDistance, 0.3f);
                }
            }

            if (remaining <= stopDistance) return Vector3.zero;
            Vector3 direction = Flat(next - position);
            if (direction.sqrMagnitude < 0.0001f) return Vector3.zero;
            float step = Mathf.Min(speed * dt, remaining - stopDistance);
            return direction.normalized * step;
        }

        void FacePlayer(float dt, float speedMultiplier)
        {
            FaceDirection(Flat(player.transform.position - transform.position), dt * speedMultiplier);
        }

        void FaceDirection(Vector3 direction, float dt)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), data.TurnSpeed * dt);
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        // ---------- Death & respawn ----------

        void OnDied()
        {
            attacks.Cancel();
            EndTurn();
            state = State.Dead;
            controller.enabled = false;
            SetVisible(false);
            respawnTimer = data.RespawnDelay;
        }

        void UpdateDead(float dt)
        {
            if (data.RespawnDelay <= 0f) return;
            respawnTimer -= dt;
            if (respawnTimer > 0f) return;

            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            controller.enabled = true;
            SetVisible(true);
            combatant.Revive();
            verticalVelocity = 0f;
            cooldown = data.AttackCooldown;
            cornerCount = 0;
            state = State.Idle;
        }

        void SetVisible(bool visible)
        {
            foreach (var r in renderers) r.enabled = visible;
        }

        void OnDrawGizmos()
        {
            if (attacks != null) CombatGizmos.DrawHitbox(attacks);
            if (cornerCount < 2) return;
            Gizmos.color = HasAttackTurn ? Color.red : Color.yellow;
            for (int i = 0; i < cornerCount - 1; i++) Gizmos.DrawLine(corners[i] + Vector3.up * 0.1f, corners[i + 1] + Vector3.up * 0.1f);
        }

        void OnDrawGizmosSelected()
        {
            if (data == null) return;
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, data.AggroRange);
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, data.AttackRange);
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.4f);
            Gizmos.DrawWireSphere(Application.isPlaying ? spawnPosition : transform.position, data.LeashRange);
        }
    }
}
