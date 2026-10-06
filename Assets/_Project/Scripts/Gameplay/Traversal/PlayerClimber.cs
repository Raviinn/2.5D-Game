using System.Collections.Generic;
using Beast.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Beast.Gameplay
{
    public enum ClimbMode
    {
        None,
        /// <summary>Hanging from a ledge: shimmy sideways, pull up, let go.</summary>
        Hanging,
        /// <summary>On a ClimbableSurface wall: move up, down and sideways.</summary>
        Climbing,
        /// <summary>Scripted move over an edge onto the top (also used to vault low ledges).</summary>
        PullingUp,
    }

    /// <summary>
    /// Ledge grab and wall climbing for the player.
    /// - Jumping (or falling) into a wall while moving toward it grabs its top edge if your hands can reach it.
    ///   Edges low enough to stand on from the ground are vaulted straight away instead of hung from.
    /// - Hanging: move sideways to shimmy, Jump to pull up (if there's room), Jump + away to leap off, Dodge/Sprint to let go.
    /// - Walls with a ClimbableSurface (ivy) can be climbed: Jump at the wall to grab on, move to climb,
    ///   reach the top to pull up, touch the ground to step off.
    /// Everything costs stamina; at zero you fall. Taking a hit knocks you off.
    /// While active, combat and normal movement are suspended (PlayerCombat.Traversing, PlayerMotor.Suspended).
    /// </summary>
    [DefaultExecutionOrder(-20)] // before PlayerCombat (-10) and PlayerMotor (0): a Jump that starts a climb never also jumps
    [RequireComponent(typeof(PlayerMotor), typeof(Stamina), typeof(Combatant))]
    public sealed class PlayerClimber : MonoBehaviour
    {
        public struct Ledge
        {
            /// <summary>Point on the top edge, at the top surface's height.</summary>
            public Vector3 Point;
            /// <summary>Flat outward normal of the wall below the edge.</summary>
            public Vector3 Normal;
            public Collider Wall;
        }

        [Header("Ledge grab")]
        [SerializeField, Tooltip("Lowest edge (height above your feet) grabbed or vaulted while in the air.")] float grabMinHeight = 0.9f;
        [SerializeField, Tooltip("Highest edge (height above your feet) your hands can reach while in the air.")] float grabMaxHeight = 2.4f;
        [SerializeField, Tooltip("While hanging, your hands are this far above your feet.")] float handReach = 2.2f;
        [SerializeField] float shimmySpeed = 1.6f;
        [SerializeField, Tooltip("Seconds to pull up from a full hang (vaults are quicker).")] float pullUpDuration = 0.6f;
        [SerializeField, Tooltip("Seconds after letting go before you can grab again.")] float regrabDelay = 0.4f;
        [SerializeField, Tooltip("Speed of the leap when jumping away from a wall (m/s).")] float leapSpeed = 4.5f;

        [Header("Wall climbing (walls with a ClimbableSurface)")]
        [SerializeField] float climbSpeed = 1.4f;

        [Header("Stamina")]
        [SerializeField, Tooltip("Per second while hanging still.")] float hangCost = 3f;
        [SerializeField, Tooltip("Per second while shimmying.")] float shimmyCost = 6f;
        [SerializeField, Tooltip("Per second while holding still on a wall.")] float climbIdleCost = 2f;
        [SerializeField, Tooltip("Per second while climbing.")] float climbMoveCost = 9f;
        [SerializeField, Tooltip("Needed to grab on at all.")] float minStaminaToGrab = 5f;

        const float WallGap = 0.06f;       // space kept between the capsule and the wall
        const float WallProbe = 0.5f;      // how far ahead of the capsule walls are looked for
        const float StandInset = 0.25f;    // how far past the edge you end up after pulling up
        const float HintRefresh = 0.15f;

        static readonly RaycastHit[] hits = new RaycastHit[16];
        static readonly Collider[] overlaps = new Collider[16];

        PlayerMotor motor;
        PlayerCombat combat;
        Combatant combatant;
        Stamina stamina;
        CharacterController controller;
        GameStateService gameState;
        InputAction jumpAction, dodgeAction;
        int mask;

        Ledge ledge;
        Vector3 wallNormal;              // climbing: current wall
        Vector3 pullFrom, pullMid, pullTo;
        float pullTime, pullDuration;
        float regrabAt;
        float moveAmount;                // 0..1, drives the animation speed
        float nextHintCheck;
        bool climbableAhead;
        float noRoomUntil;
        readonly List<(string key, string label)> hints = new();

        public ClimbMode Mode { get; private set; }
        public bool IsActive => Mode != ClimbMode.None;
        public CharacterAnim CurrentAnim => Mode == ClimbMode.Hanging ? CharacterAnim.Hang : CharacterAnim.Climb;
        /// <summary>Animation playback speed: frozen while holding still on a wall, gentle sway while hanging.</summary>
        public float AnimationSpeed => Mode switch
        {
            ClimbMode.Hanging => Mathf.Lerp(0.4f, 1.2f, moveAmount),
            ClimbMode.Climbing => moveAmount,
            _ => 1.5f,
        };
        /// <summary>Key hints for the HUD (empty when there's nothing to show).</summary>
        public IReadOnlyList<(string key, string label)> Hints => hints;
        /// <summary>A short warning to show under the hints (e.g. "No room to climb up"), or null.</summary>
        public string Warning => Time.unscaledTime < noRoomUntil ? "No room to climb up" : null;

        float HalfHeight => controller.height * 0.5f;
        float Radius => controller.radius;
        float FeetY(Vector3 root) => root.y + controller.center.y - HalfHeight;

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            combat = GetComponent<PlayerCombat>();
            combatant = GetComponent<Combatant>();
            stamina = GetComponent<Stamina>();
            controller = GetComponent<CharacterController>();
        }

        void OnEnable()
        {
            combatant.Hit += OnHit;
            EventBus<GameLoadedEvent>.Subscribe(OnGameLoaded);
        }

        void OnDisable()
        {
            combatant.Hit -= OnHit;
            EventBus<GameLoadedEvent>.Unsubscribe(OnGameLoaded);
            if (IsActive) Release();
        }

        void Start()
        {
            var input = Services.Get<InputService>();
            jumpAction = input.Jump;
            dodgeAction = input.Dodge;
            gameState = Services.Get<GameStateService>();

            // World geometry only: not characters, not the billboard sprites.
            mask = Physics.DefaultRaycastLayers & ~CombatLayers.PlayerMask & ~CombatLayers.EnemyMask;
            int billboards = LayerMask.NameToLayer(EnvironmentLayers.Billboards);
            if (billboards >= 0) mask &= ~(1 << billboards);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || gameState.Current != GameState.Playing) return;

            if (IsActive && (combat == null || !combat.IsTraversing))
            {
                // Something else took over (death): just hand movement back.
                Mode = ClimbMode.None;
                motor.Suspended = false;
            }

            switch (Mode)
            {
                case ClimbMode.None: TryStart(); break;
                case ClimbMode.Hanging: UpdateHang(dt); break;
                case ClimbMode.Climbing: UpdateClimb(dt); break;
                case ClimbMode.PullingUp: UpdatePullUp(dt); break;
            }
            UpdateHints();
        }

        // ---------- Starting ----------

        void TryStart()
        {
            if (combat == null || !combat.CanStartTraversal || Time.time < regrabAt || stamina.Current < minStaminaToGrab) return;

            Vector3 root = transform.position;
            Vector3 input = motor.GetMoveDirection();
            Vector3 forward = input.sqrMagnitude > 0.01f ? input.normalized : transform.forward;

            // Ivy walls: Jump at them (or run into them in the air) to grab on.
            bool towardWall = input.sqrMagnitude > 0.1f;
            if ((jumpAction.WasPressedThisFrame() || (!motor.IsGrounded && towardWall)) &&
                FindClimbableWall(root, forward, out var wall))
            {
                StartClimb(wall);
                return;
            }

            // Ledges: only while airborne, moving toward the wall, and no longer rising fast (so jumps reach their height).
            if (motor.IsGrounded || !towardWall || motor.VerticalVelocity > 3f) return;
            if (!ProbeLedge(root, forward, grabMinHeight, grabMaxHeight, out var found)) return;
            if (Vector3.Dot(forward, -found.Normal) < 0.5f) return;

            Vector3 hang = HangRoot(found);
            bool feetWouldTouchGround = Cast(hang, Vector3.down, HalfHeight + 0.3f, out _);
            if (feetWouldTouchGround)
            {
                // Low enough to stand on from below: vault straight up instead of hanging with your feet on the floor.
                if (CanStand(StandRoot(found))) StartPullUp(found);
                return;
            }
            if (!IsClear(hang)) return;
            StartHang(found);
        }

        void Enter(ClimbMode mode)
        {
            if (Mode == ClimbMode.None) combat.BeginTraversal();
            Mode = mode;
            motor.Suspended = true;
            moveAmount = 0f;
        }

        void Release()
        {
            Mode = ClimbMode.None;
            motor.Suspended = false;
            if (combat != null) combat.EndTraversal();
        }

        /// <summary>Drop: gravity takes over. Can't regrab for a moment.</summary>
        public void LetGo(string reason = null)
        {
            if (!IsActive) return;
            Release();
            regrabAt = Time.time + regrabDelay;
            if (reason != null) EventBus<HudMessageEvent>.Raise(new HudMessageEvent(reason));
        }

        void LeapOff(Vector3 away)
        {
            Release();
            regrabAt = Time.time + regrabDelay;
            motor.Launch(away * leapSpeed + Vector3.up * leapSpeed);
            motor.SnapRotation(away);
        }

        void OnHit(HitResult result)
        {
            if (IsActive && result is HitResult.Hit or HitResult.GuardBroken) LetGo("Knocked off!");
        }

        void OnGameLoaded(GameLoadedEvent evt)
        {
            // The save put us somewhere else: never snap back to the old ledge.
            if (IsActive) Release();
        }

        // ---------- Hanging ----------

        void StartHang(Ledge found)
        {
            Enter(ClimbMode.Hanging);
            ledge = found;
            motor.Place(HangRoot(ledge));
            transform.rotation = Quaternion.LookRotation(-ledge.Normal);
        }

        void UpdateHang(float dt)
        {
            if (stamina.Current <= 0.01f) { LetGo("Too tired to hold on"); return; }
            if (dodgeAction.WasPressedThisFrame()) { LetGo(); return; }

            Vector3 facing = -ledge.Normal;
            Vector3 right = Vector3.Cross(Vector3.up, facing);
            Vector3 input = motor.GetMoveDirection();
            float lateral = Vector3.Dot(input, right);
            float toward = Vector3.Dot(input, facing);

            if (jumpAction.WasPressedThisFrame())
            {
                if (toward < -0.5f) { LeapOff(ledge.Normal); return; }
                if (CanStand(StandRoot(ledge))) { StartPullUp(ledge); return; }
                noRoomUntil = Time.unscaledTime + 1.5f;
            }

            moveAmount = 0f;
            if (Mathf.Abs(lateral) > 0.2f)
            {
                float direction = Mathf.Sign(lateral);
                float step = direction * shimmySpeed * Mathf.Abs(lateral) * dt;
                Vector3 root = transform.position;
                // The hands lead: the edge must continue a little past where the body will be.
                bool continues = ProbeLedgeNear(root + right * (step + direction * (Radius + 0.1f)), facing, out _);
                if (continues && ProbeLedgeNear(root + right * step, facing, out var next) && IsClear(HangRoot(next)))
                {
                    ledge = next;
                    motor.Place(HangRoot(ledge));
                    transform.rotation = Quaternion.LookRotation(-ledge.Normal);
                    moveAmount = Mathf.Abs(lateral);
                }
            }
            stamina.Spend((moveAmount > 0f ? shimmyCost : hangCost) * dt);
        }

        /// <summary>The same edge, a little further along (height and angle must match).</summary>
        bool ProbeLedgeNear(Vector3 root, Vector3 facing, out Ledge next)
        {
            return ProbeLedge(root, facing, handReach - 0.35f, handReach + 0.35f, out next) &&
                   Mathf.Abs(next.Point.y - ledge.Point.y) < 0.3f && Vector3.Dot(next.Normal, ledge.Normal) > 0.7f;
        }

        // ---------- Pulling up ----------

        void StartPullUp(Ledge top)
        {
            Enter(ClimbMode.PullingUp);
            ledge = top;
            pullFrom = transform.position;
            pullTo = StandRoot(top);
            pullMid = new Vector3(pullFrom.x, pullTo.y + 0.05f, pullFrom.z);
            pullTime = 0f;
            // Vaulting a low edge is quicker than hauling yourself up from a full hang.
            pullDuration = Mathf.Lerp(0.3f, pullUpDuration, Mathf.InverseLerp(0.5f, handReach, pullTo.y - pullFrom.y));
            transform.rotation = Quaternion.LookRotation(-top.Normal);
        }

        void UpdatePullUp(float dt)
        {
            pullTime += dt;
            float t = Mathf.Clamp01(pullTime / pullDuration);
            // Up first (hands on the edge), then forward onto the top.
            Vector3 position = t < 0.65f
                ? Vector3.Lerp(pullFrom, pullMid, Smooth(t / 0.65f))
                : Vector3.Lerp(pullMid, pullTo, Smooth((t - 0.65f) / 0.35f));
            motor.Place(position);
            moveAmount = 1f;
            if (t >= 1f) Release();
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);

        // ---------- Wall climbing ----------

        bool FindClimbableWall(Vector3 root, Vector3 forward, out RaycastHit wall)
        {
            forward.y = 0f;
            forward.Normalize();
            return Cast(root, forward, Radius + WallProbe, out wall) && IsWallLike(wall.normal) && IsClimbable(wall.collider) &&
                   Vector3.Dot(forward, -Flat(wall.normal)) > 0.5f;
        }

        void StartClimb(RaycastHit wall)
        {
            Enter(ClimbMode.Climbing);
            wallNormal = Flat(wall.normal);
            Vector3 root = transform.position;
            var target = wall.point + wallNormal * (Radius + WallGap);
            target.y = root.y + (motor.IsGrounded ? 0.15f : 0f); // step up off the floor
            if (IsClear(target)) motor.Place(target);
            transform.rotation = Quaternion.LookRotation(-wallNormal);
        }

        void UpdateClimb(float dt)
        {
            if (stamina.Current <= 0.01f) { LetGo("Too tired to hold on"); return; }
            if (dodgeAction.WasPressedThisFrame()) { LetGo(); return; }

            Vector2 raw = Vector2.ClampMagnitude(motor.RawInput, 1f);
            Vector3 facing = -wallNormal;
            Vector3 right = Vector3.Cross(Vector3.up, facing);
            Vector3 root = transform.position;

            if (jumpAction.WasPressedThisFrame() && raw.y < -0.5f) { LeapOff(wallNormal); return; }

            float up = raw.y * climbSpeed * dt;
            float side = raw.x * climbSpeed * 0.8f * dt;

            // Reaching the top: the wall ends above the head, so pull up over the edge (if there's room).
            if (up > 0f && !ClimbableAt(root + Vector3.up * (HalfHeight - 0.1f + up), facing, out _))
            {
                if (ProbeLedge(root, facing, 0.3f, handReach + 0.4f, out var top) && CanStand(StandRoot(top)))
                {
                    StartPullUp(top);
                    return;
                }
                up = 0f;
            }
            // Climbing down onto the floor: step off.
            if (up < 0f && Cast(root, Vector3.down, HalfHeight + 0.15f, out _))
            {
                Release();
                return;
            }

            moveAmount = 0f;
            Vector3 next = root + Vector3.up * up + right * side;
            if ((up != 0f || side != 0f) && ClimbableAt(next, facing, out var hold))
            {
                wallNormal = Flat(hold.normal);
                var glued = hold.point + wallNormal * (Radius + WallGap);
                glued.y = next.y;
                if (IsClear(glued))
                {
                    motor.Place(glued);
                    transform.rotation = Quaternion.LookRotation(-wallNormal);
                    moveAmount = Mathf.Clamp01(raw.magnitude);
                }
            }
            stamina.Spend((moveAmount > 0f ? climbMoveCost : climbIdleCost) * dt);
        }

        bool ClimbableAt(Vector3 root, Vector3 facing, out RaycastHit hit) =>
            Cast(root - facing * 0.1f, facing, Radius + WallProbe, out hit) && IsWallLike(hit.normal) && IsClimbable(hit.collider);

        // ---------- HUD hints ----------

        void UpdateHints()
        {
            hints.Clear();
            switch (Mode)
            {
                case ClimbMode.Hanging:
                    hints.Add(("Space", "Climb up"));
                    hints.Add(("A / D", "Shimmy"));
                    hints.Add(("Shift", "Let go"));
                    break;
                case ClimbMode.Climbing:
                    hints.Add(("W / S", "Climb"));
                    hints.Add(("A / D", "Sideways"));
                    hints.Add(("Shift", "Let go"));
                    break;
                case ClimbMode.None:
                    if (Time.unscaledTime >= nextHintCheck)
                    {
                        nextHintCheck = Time.unscaledTime + HintRefresh;
                        climbableAhead = motor.IsGrounded && combat != null && combat.CanStartTraversal &&
                                         FindClimbableWall(transform.position, transform.forward, out _);
                    }
                    if (climbableAhead) hints.Add(("Space", "Climb"));
                    break;
            }
        }

        // ---------- Geometry ----------

        /// <summary>
        /// Looks for a grabbable top edge on the wall in front of 'root', whose height above the feet is within [minAbove, maxAbove].
        /// Walls are searched from high to low, so the edge found is the one nearest your hands.
        /// </summary>
        public bool ProbeLedge(Vector3 root, Vector3 forward, float minAbove, float maxAbove, out Ledge found)
        {
            found = default;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) return false;
            forward.Normalize();
            float feet = FeetY(root);

            for (float h = maxAbove; h >= 0.3f; h -= 0.3f)
            {
                var origin = new Vector3(root.x, feet + h, root.z);
                if (!Cast(origin, forward, Radius + WallProbe, out var wall) || !IsWallLike(wall.normal) || IsBlocked(wall.collider)) continue;

                Vector3 normal = Flat(wall.normal);
                // Look down onto the top from just inside the wall, starting above the highest reachable edge.
                var above = wall.point - normal * 0.2f;
                above.y = feet + maxAbove + 0.25f;
                if (Physics.CheckSphere(above, 0.05f, mask, QueryTriggerInteraction.Ignore)) continue; // wall is taller than we can reach
                if (!Cast(above, Vector3.down, maxAbove + 0.25f - minAbove + 0.01f, out var surface)) continue;
                if (surface.normal.y < 0.7f || IsBlocked(surface.collider)) continue;

                float height = surface.point.y - feet;
                if (height < minAbove || height > maxAbove) continue;

                found = new Ledge { Point = new Vector3(wall.point.x, surface.point.y, wall.point.z), Normal = normal, Wall = wall.collider };
                return true;
            }
            return false;
        }

        /// <summary>Body position while hanging: hands on the edge, chest a hand's width from the wall.</summary>
        public Vector3 HangRoot(Ledge l) =>
            l.Point + l.Normal * (Radius + WallGap) + Vector3.up * (HalfHeight - controller.center.y - handReach);

        /// <summary>Body position standing on top, just past the edge.</summary>
        public Vector3 StandRoot(Ledge l) =>
            l.Point - l.Normal * (Radius + StandInset) + Vector3.up * (HalfHeight - controller.center.y + 0.05f);

        /// <summary>Room for the whole body here, with solid ground underneath (rules out posts and narrow tops).</summary>
        public bool CanStand(Vector3 root) => IsClear(root) && Cast(root, Vector3.down, HalfHeight + 0.3f, out var ground) && ground.normal.y > 0.7f;

        bool IsClear(Vector3 root)
        {
            Vector3 center = root + controller.center;
            float inner = Mathf.Max(0f, HalfHeight - Radius);
            int count = Physics.OverlapCapsuleNonAlloc(center + Vector3.down * (inner - 0.05f), center + Vector3.up * inner,
                Radius * 0.9f, overlaps, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (IsWorld(overlaps[i])) return false;
            return true;
        }

        /// <summary>Nearest hit on world geometry (ignores characters and our own collider).</summary>
        bool Cast(Vector3 origin, Vector3 direction, float distance, out RaycastHit best)
        {
            best = default;
            int count = Physics.RaycastNonAlloc(origin, direction, hits, distance, mask, QueryTriggerInteraction.Ignore);
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                if (!IsWorld(hits[i].collider) || (found && hits[i].distance >= best.distance)) continue;
                best = hits[i];
                found = true;
            }
            return found;
        }

        bool IsWorld(Collider c) =>
            c != null && c is not CharacterController && !c.transform.IsChildOf(transform) &&
            c.GetComponentInParent<Combatant>() == null && c.GetComponentInParent<NpcController>() == null;

        static bool IsWallLike(Vector3 normal) => Mathf.Abs(normal.y) < 0.35f;
        static bool IsBlocked(Collider c) => c.GetComponentInParent<NotClimbable>() != null;
        static bool IsClimbable(Collider c) => c.GetComponentInParent<ClimbableSurface>() != null && !IsBlocked(c);
        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z).normalized;
    }
}
