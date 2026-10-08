using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Beast.Core;
using Beast.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Test-only: checks enemy pathfinding, giving up, leashing and turn-taking. Runs only with -aiharness.</summary>
public sealed class AiHarness : MonoBehaviour
{
    static readonly Vector3 O = new(-20f, 0f, -13f); // climbing course origin
    static readonly Vector3 Parking = new(42f, 1.1f, -42f);

    string dir;
    int failures;
    GameObject player;
    PlayerMotor motor;
    Combatant me;
    ThirdPersonCamera rig;
    readonly List<EnemyController> bandits = new();
    EnemyController archer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-aiharness") < 0) return;
        var go = new GameObject("AiHarness");
        DontDestroyOnLoad(go);
        go.AddComponent<AiHarness>();
    }

    void Check(bool ok, string what)
    {
        if (!ok) failures++;
        Debug.Log($"[AiTest] {(ok ? "PASS" : "FAIL")} {what}");
    }

    void PlaceAt(Vector3 position, float yaw)
    {
        motor.Teleport(position, Quaternion.Euler(0f, yaw, 0f));
        typeof(ThirdPersonCamera).GetField("yaw", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(rig, yaw);
    }

    void ParkAll()
    {
        for (int i = 0; i < bandits.Count; i++) bandits[i].Warp(Parking + Vector3.right * (i * 3f), true);
        if (archer != null) archer.Warp(Parking + Vector3.forward * 6f, true);
    }

    IEnumerator Start()
    {
        var args = Environment.GetCommandLineArgs();
        dir = args[Array.IndexOf(args, "-shots") + 1];
        Directory.CreateDirectory(dir);
        while (SceneManager.GetActiveScene().name != "World_Test" || GameObject.FindWithTag("Player") == null) yield return null;
        yield return new WaitForSecondsRealtime(3f);

        player = GameObject.FindWithTag("Player");
        motor = player.GetComponent<PlayerMotor>();
        me = player.GetComponent<Combatant>();
        rig = FindFirstObjectByType<ThirdPersonCamera>();
        foreach (var e in EnemyController.Active)
        {
            if (e.Data.IsTrainingDummy) continue;
            if (e.Data.Ranged) archer = e; else bandits.Add(e);
        }

        Check(Services.TryGet(out WorldNavigation nav) && nav.IsReady, $"navigation mesh built ({(nav != null ? nav.TriangleCount : 0)} triangles)");
        Check(bandits.Count >= 3, $"{bandits.Count} bandits in the world (Bandit_C added)");

        // ---- A: path around the climbing course (ledge wall + ivy cliff block x {O.x-5}..{O.x+2}, z {O.z-4.5}..{O.z+4.5}) ----
        ParkAll();
        me.Invulnerable = true;
        var runner = bandits[0];
        PlaceAt(new Vector3(O.x + 5f, 1.1f, O.z), -90f);
        runner.Warp(new Vector3(O.x - 6.5f, 1.1f, O.z), true);
        yield return Wait(0.3f);
        float start = Time.time;
        float best = float.MaxValue;
        bool arrived = false;
        while (Time.time - start < 15f && !arrived)
        {
            float d = Flat(runner.transform.position - player.transform.position).magnitude;
            best = Mathf.Min(best, d);
            arrived = d < 2.6f;
            yield return null;
        }
        Check(arrived, $"bandit walked around the 7 m x 9 m block to reach the player in {Time.time - start:0.0} s (closest {best:0.0} m)");
        yield return Shot("a1_path_around");

        // ---- B: player climbs out of reach -> bandit waits below, then gives up and walks home ----
        ParkAll();
        me.Invulnerable = false;
        me.Heal(9999f);
        PlaceAt(new Vector3(O.x + 1f, 3.2f + 1.1f, O.z), 90f);
        runner.Warp(new Vector3(O.x + 6f, 1.1f, O.z + 1f), true);
        Vector3 home = runner.Home;
        yield return Wait(2.5f);
        float hp = me.Health;
        Check(!runner.PlayerReachable, "bandit knows the player on the wall is unreachable");
        float footDistance = Flat(runner.transform.position - new Vector3(O.x + 2f, 0f, runner.transform.position.z)).magnitude;
        Check(footDistance < 1.5f, $"bandit waits at the foot of the wall ({footDistance:0.0} m from it)");
        yield return Shot("b1_waiting_below");
        start = Time.time;
        bool gaveUp = false;
        while (Time.time - start < 8f && !gaveUp) { gaveUp = runner.StateName == "Return"; yield return null; }
        Check(gaveUp, $"bandit gives up after ~5 s and heads home ({runner.StateName})");
        start = Time.time;
        while (Time.time - start < 10f && runner.StateName != "Idle") yield return null;
        Check(runner.StateName == "Idle" && Flat(runner.transform.position - home).magnitude < 1.2f, $"bandit is back home ({Flat(runner.transform.position - home).magnitude:0.0} m away)");
        Check(Mathf.Approximately(me.Health, hp) && me.Health >= me.MaxHealth - 0.01f, "the player on the wall was never hit");

        // ---- C: leash - kite a wounded bandit away, it walks home and heals ----
        ParkAll();
        me.Invulnerable = true;
        Vector3 open = new Vector3(-12f, 1.1f, 20f);
        runner.Warp(open + new Vector3(4f, 0f, 0f), true);
        PlaceAt(open, 90f);
        yield return Wait(1.5f);
        runner.Combatant.ReceiveHit(new DamageInfo { Damage = 30f, Attacker = me });
        float wounded = runner.Combatant.Health;
        PlaceAt(new Vector3(-12f, 1.1f, 44f), 0f); // 24 m away: beyond 1.5x aggro
        start = Time.time;
        while (Time.time - start < 15f && !(runner.StateName == "Idle" && runner.Combatant.Health >= runner.Combatant.MaxHealth)) yield return null;
        Check(wounded < runner.Combatant.MaxHealth && runner.Combatant.Health >= runner.Combatant.MaxHealth && runner.StateName == "Idle",
            $"leashed bandit went home and healed ({wounded:0} -> {runner.Combatant.Health:0} HP)");

        // ---- D: turn-taking with three bandits ----
        ParkAll();
        PlaceAt(open, 0f);
        for (int i = 0; i < 3; i++)
        {
            var at = open + Quaternion.Euler(0f, i * 120f, 0f) * Vector3.forward * 6f;
            bandits[i].Warp(at, true);
        }
        yield return Wait(0.5f);
        int maxAttacking = 0, maxTurns = 0;
        var attacked = new bool[3];
        var wasAttacking = new bool[3];
        int swings = 0;
        float minGap = float.MaxValue, gapSum = 0f;
        int gapSamples = 0;
        start = Time.time;
        bool shot = false;
        while (Time.time - start < 16f)
        {
            int attacking = 0, turns = 0;
            for (int i = 0; i < 3; i++)
            {
                bool now = bandits[i].Attacks.IsAttacking;
                if (now) { attacking++; attacked[i] = true; }
                if (now && !wasAttacking[i]) swings++;
                wasAttacking[i] = now;
                if (bandits[i].HasAttackTurn) turns++;
            }
            maxAttacking = Math.Max(maxAttacking, attacking);
            maxTurns = Math.Max(maxTurns, turns);
            for (int i = 0; i < 3; i++)
                for (int j = i + 1; j < 3; j++)
                {
                    float g = Flat(bandits[i].transform.position - bandits[j].transform.position).magnitude;
                    minGap = Mathf.Min(minGap, g);
                    gapSum += g;
                    gapSamples++;
                }
            if (!shot && Time.time - start > 4f) { shot = true; yield return Shot("d1_group"); }
            yield return null;
        }
        Check(maxTurns <= EnemyDirector.MaxAttackers && maxAttacking <= EnemyDirector.MaxAttackers,
            $"at most {EnemyDirector.MaxAttackers} bandits had a turn at once (turns {maxTurns}, swinging {maxAttacking})");
        Check(attacked[0] && attacked[1] && attacked[2], $"every bandit got a turn ({swings} swings in 16 s)");
        Check(gapSum / gapSamples > 1.5f && minGap > 0.5f, $"bandits keep apart (average gap {gapSum / gapSamples:0.0} m, closest {minGap:0.0} m)");

        yield return JumpLinkAndArcherTests(nav, open);

        me.Invulnerable = false;
        Debug.Log($"[AiTest] done, failures={failures}");
        Application.Quit();
    }

    // ---------- Milestone 25: jump links & archers ----------

    IEnumerator JumpLinkAndArcherTests(WorldNavigation nav, Vector3 open)
    {
        Check(nav != null && nav.VaultLinkCount > 0 && nav.DropLinkCount > 0,
            $"jump links built ({nav?.VaultLinkCount} two-way vaults, {nav?.DropLinkCount} drops)");
        var runner = bandits[0];

        // ---- E: the player on the 2.3 m block: a bandit vaults up after them ----
        ParkAll();
        me.Invulnerable = true;
        PlaceAt(new Vector3(O.x + 5f, 2.3f + 1.1f, O.z - 0.5f), 90f);
        runner.Warp(new Vector3(O.x + 10f, 1.1f, O.z - 0.5f), true);
        bool vaulted = false, swung = false;
        float start = Time.time;
        while (Time.time - start < 10f && !swung)
        {
            vaulted |= runner.IsVaulting;
            swung |= runner.Attacks.IsAttacking && runner.transform.position.y > 2.8f;
            yield return null;
        }
        Check(vaulted && runner.transform.position.y > 2.8f, $"bandit vaults onto the 2.3 m block (y {runner.transform.position.y:0.00})");
        Check(swung, "and attacks the player up there");
        yield return Shot("e1_vaulted");

        // ---- F: a bandit on the 3.2 m wall jumps down to reach the player ----
        ParkAll();
        PlaceAt(new Vector3(O.x + 6f, 1.1f, O.z + 2f), -90f);
        runner.Warp(new Vector3(O.x + 1f, 3.2f + 1.1f, O.z + 2f), true);
        start = Time.time;
        bool reached = false;
        while (Time.time - start < 8f && !reached)
        {
            reached = runner.transform.position.y < 1.5f && Flat(runner.transform.position - player.transform.position).magnitude < 2.8f;
            yield return null;
        }
        Check(reached, $"bandit jumps down from the 3.2 m wall to reach the player (y {runner.transform.position.y:0.00})");

        // ---- G: the player on the 3.2 m wall is still out of melee reach (too high to vault) ----
        Check(!nav.CanReach(new Vector3(O.x + 6f, 0f, O.z + 2f), new Vector3(O.x + 1f, 3.2f, O.z + 2f)),
            "the 3.2 m wall top can't be reached on foot");

        if (archer == null)
        {
            Check(false, "Bandit_Archer exists");
            yield break;
        }
        Check(archer.Data.Ranged && archer.Attacks != null, "Bandit_Archer is ranged");

        // ---- H: the archer shoots the player standing on the wall (melee bandits can't reach) ----
        ParkAll();
        me.Invulnerable = false;
        me.Heal(9999f);
        PlaceAt(new Vector3(O.x + 1f, 3.2f + 1.1f, O.z), 90f);
        archer.Warp(new Vector3(O.x + 13f, 1.1f, O.z), true);
        float before = me.Health;
        int arrows = 0;
        bool drew = false, sawDraw = false;
        start = Time.time;
        while (Time.time - start < 9f)
        {
            bool drawing = archer.Attacks.CurrentPhase == AttackExecutor.Phase.Startup;
            if (drawing && !drew) arrows++;
            drew = drawing;
            if (drawing && archer.Combatant.Telegraphing && !sawDraw && Time.time - start > 1f)
            {
                sawDraw = true;
                yield return Shot("h1_archer_draws");
            }
            yield return null;
        }
        Check(archer.CanSeePlayer, "the archer sees the player on the wall");
        Check(arrows >= 2, $"the archer keeps shooting ({arrows} shots in 9 s)");
        Check(me.Health < before, $"arrows hit the player on the wall ({before:0} -> {me.Health:0} HP)");
        Check(archer.StateName != "Return", $"the archer doesn't give up while it can see you ({archer.StateName})");
        yield return Shot("h2_arrows");

        // ---- I: the archer backs off when you close in ----
        ParkAll();
        me.Invulnerable = true;
        PlaceAt(open, 90f);
        archer.Warp(open + new Vector3(4f, 0f, 0f), true);
        yield return Wait(4f);
        float gap = Flat(archer.transform.position - player.transform.position).magnitude;
        Check(gap > 6f, $"the archer backs away to shooting distance ({gap:0.0} m)");

        // ---- J: no line of sight -> it moves until it can see you ----
        ParkAll();
        me.Invulnerable = true;
        PlaceAt(new Vector3(O.x - 7f, 1.1f, O.z), 90f); // west of the ivy cliff
        archer.Warp(new Vector3(O.x + 5f, 1.1f, O.z), true); // east of it: the cliff and wall block the view
        bool blindAtStart = Physics.Linecast(archer.transform.position + Vector3.up * 0.6f, player.transform.position + Vector3.up * 0.3f, 1, QueryTriggerInteraction.Ignore);
        yield return Wait(0.3f);
        start = Time.time;
        while (Time.time - start < 12f && !archer.CanSeePlayer) yield return null;
        Check(blindAtStart && archer.CanSeePlayer, $"an archer without a view walks round until it has one ({Time.time - start:0.0} s)");

        me.Invulnerable = false;
        ParkAll();
    }

    static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

    static IEnumerator Wait(float seconds) => new WaitForSecondsRealtime(seconds);

    IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();
        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
        Destroy(tex);
    }
}
