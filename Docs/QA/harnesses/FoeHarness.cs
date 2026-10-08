using System;
using System.Collections;
using System.IO;
using System.Linq;
using Beast.Core;
using Beast.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Test-only: new enemy types (Milestone 40): wolves, the shieldbearer, the Blighted Brute. Runs with -foeharness.</summary>
public sealed class FoeHarness : MonoBehaviour
{
    string dir;
    int failures;
    Combatant me;
    PlayerMotor motor;
    WorldClock clock;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-foeharness") < 0) return;
        var go = new GameObject("FoeHarness");
        DontDestroyOnLoad(go);
        go.AddComponent<FoeHarness>();
    }

    void Check(bool ok, string what)
    {
        if (!ok) failures++;
        Debug.Log($"[FoeTest] {(ok ? "PASS" : "FAIL")} {what}");
    }

    IEnumerator Start()
    {
        var args = Environment.GetCommandLineArgs();
        dir = args[Array.IndexOf(args, "-shots") + 1];
        Directory.CreateDirectory(dir);
        while (SceneManager.GetActiveScene().name != "World_Test" || GameObject.FindWithTag("Player") == null) yield return null;
        yield return Wait(3f);

        var player = GameObject.FindWithTag("Player");
        me = player.GetComponent<Combatant>();
        motor = player.GetComponent<PlayerMotor>();
        clock = Services.Get<WorldClock>();
        GoTo(10f);
        yield return Wait(1f);

        var wolves = new[] { "Wolf_A", "Wolf_B", "Wolf_C" }.Select(n => GameObject.Find(n)?.GetComponent<EnemyController>()).ToArray();
        var shield = GameObject.Find("Bandit_Shieldbearer")?.GetComponent<EnemyController>();
        var bruteObject = GameObject.Find("Blighted_Brute");
        var brute = bruteObject != null ? bruteObject.GetComponent<EnemyController>() : null;
        Check(wolves.All(w => w != null && w.Data.PackHunter), "three pack-hunting wolves are in the world");
        Check(shield != null && shield.Data.Shield, "a shieldbearer is at the camp");
        Check(brute != null && brute.Data.SuperArmor && bruteObject.GetComponent<NightPresence>() != null, "a night-only Blighted Brute exists");
        var book = FindFirstObjectByType<Workbench>()?.Recipes;
        Check(book != null && book.Recipes.Any(r => r != null && r.Output != null && r.Output.DisplayName == "Fur Leggings") &&
              book.Recipes.Any(r => r != null && r.Output != null && r.Output.DisplayName == "Ichor Draught"), "Fur Leggings and Ichor Draught are in the recipe book");

        if (wolves.All(w => w != null)) yield return WolfTests(wolves);
        if (shield != null) yield return ShieldTests(shield);
        if (brute != null) yield return BruteTests(brute, bruteObject);
        Finish();
    }

    // ---------- Wolves: flank, bite, dart away ----------

    IEnumerator WolfTests(EnemyController[] wolves)
    {
        foreach (var e in EnemyController.Active) e.Warp(e.Home, true);
        Vector3 den = (wolves[0].Home + wolves[1].Home + wolves[2].Home) / 3f;
        // Stand 7 m from the den, facing it.
        Vector3 toDen = Flat(den - Vector3.zero).normalized;
        Vector3 stand = den - toDen * 7f;
        motor.Teleport(stand + Vector3.up * 1.2f, Quaternion.LookRotation(toDen));
        me.Invulnerable = false;
        yield return Wait(0.5f);
        Check(Mathf.Abs(motor.transform.position.y - den.y) < 2f, $"standing near the den ({motor.transform.position:F1})");

        float behindMax = 0f;
        bool bit = false, retreated = false, retreatOpened = false;
        float distanceAtBite = 0f;
        EnemyController biter = null;
        Action<DamageDealtEvent> onHit = evt =>
        {
            if (evt.Target != me || evt.Attacker == null || bit) return;
            var wolf = evt.Attacker.GetComponent<EnemyController>();
            if (wolf == null || !wolf.Data.PackHunter) return;
            bit = true;
            biter = wolf;
        };
        EventBus<DamageDealtEvent>.Subscribe(onHit);
        float start = Time.realtimeSinceStartup;
        bool shot = false;
        while (Time.realtimeSinceStartup - start < 20f && !(retreatOpened && behindMax > 100f))
        {
            me.Heal(999f);
            // Keep facing the den so "behind" means something.
            motor.transform.rotation = Quaternion.LookRotation(toDen);
            foreach (var w in wolves)
            {
                if (w.HasAttackTurn || w.Retreating) continue;
                Vector3 to = Flat(w.transform.position - motor.transform.position);
                if (to.magnitude < 7f) behindMax = Mathf.Max(behindMax, Vector3.Angle(toDen, to));
            }
            if (bit && biter != null && !retreated && biter.Retreating)
            {
                retreated = true;
                distanceAtBite = Flat(biter.transform.position - motor.transform.position).magnitude;
            }
            if (retreated && !retreatOpened && Flat(biter.transform.position - motor.transform.position).magnitude > distanceAtBite + 1f) retreatOpened = true;
            if (!shot && Time.realtimeSinceStartup - start > 5f)
            {
                shot = true;
                yield return Shot("f1_wolves");
            }
            yield return null;
        }
        EventBus<DamageDealtEvent>.Unsubscribe(onHit);
        Check(wolves.Count(w => w.StateName != "Idle") >= 2, "the pack gives chase together");
        Check(bit, "a wolf bites");
        Check(retreated && retreatOpened, $"and darts away afterwards (from {distanceAtBite:0.0} m)");
        Check(behindMax > 100f, $"waiting wolves circle round behind you (widest {behindMax:0}° from where you face)");
        me.Invulnerable = true;
    }

    // ---------- Shieldbearer: front blocked, guard breaks, side hits land ----------

    IEnumerator ShieldTests(EnemyController shield)
    {
        foreach (var e in EnemyController.Active) e.Warp(e.Home, true);
        Vector3 home = shield.Home;
        motor.Teleport(home + new Vector3(-6f, 1.2f, -2f), Quaternion.identity);
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < 6f && !shield.Combatant.IsBlocking) yield return null;
        Check(shield.Combatant.IsBlocking, "the shieldbearer raises its shield as it comes at you");
        yield return Shot("f2_shield_up");
        shield.enabled = false; // hold it still, shield up, for clean hits

        // Freeze it in place facing the player for clean hits.
        var target = shield.Combatant;
        Vector3 front = shield.transform.position + shield.transform.forward * 1.5f;
        motor.Teleport(front + Vector3.up * 0.2f, Quaternion.LookRotation(-shield.transform.forward));
        yield return null;
        float before = target.Health;
        var result = Hit(target, 20f, 10f);
        Check(result == HitResult.Blocked && before - target.Health <= 2.5f, $"a hit from the front is blocked ({result}, {before - target.Health:0.0} damage)");
        HitResult last = result;
        int hits = 1;
        while (last == HitResult.Blocked && hits < 12)
        {
            yield return null;
            if (!target.IsBlocking) break;
            last = Hit(target, 20f, 10f);
            hits++;
        }
        Check(last == HitResult.GuardBroken && target.IsStaggered, $"heavy blows break its guard after {hits} hits ({last}), staggering it");

        // Let it recover and raise the shield again, then hit it from behind.
        shield.enabled = true;
        start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < 10f && !shield.Combatant.IsBlocking) yield return null;
        Check(shield.Combatant.IsBlocking, "once its guard recovers the shield comes back up");
        shield.enabled = false;
        Vector3 behind = shield.transform.position - shield.transform.forward * 1.5f;
        motor.Teleport(behind + Vector3.up * 0.2f, Quaternion.LookRotation(shield.transform.forward));
        yield return null;
        before = target.Health;
        result = Hit(target, 20f, 10f);
        Check(result == HitResult.Hit && before - target.Health > 15f, $"a hit from behind lands in full ({result}, {before - target.Health:0.0})");
        target.Heal(999f);
        shield.enabled = true;
    }

    // ---------- Brute: night only, unstoppable mid-swing, loot ----------

    IEnumerator BruteTests(EnemyController brute, GameObject bruteObject)
    {
        var presence = bruteObject.GetComponent<NightPresence>();
        motor.Teleport(new Vector3(0f, 1.2f, 0f), Quaternion.identity);
        GoTo(11f);
        yield return Wait(1f);
        bool hidden = !EnemyController.Active.Contains(brute) && bruteObject.GetComponentsInChildren<Renderer>().All(r => !r.enabled);
        Check(!presence.Present && hidden, "by day the brute isn't there (hidden, not an active enemy)");

        GoTo(22.5f);
        yield return Wait(1f);
        Check(presence.Present && EnemyController.Active.Contains(brute) && brute.Combatant.Health >= brute.Combatant.MaxHealth,
            "at night it's out, at full health");

        foreach (var e in EnemyController.Active) if (e != brute) e.Warp(e.Home, true);
        me.Invulnerable = true;
        motor.Teleport(brute.Home + new Vector3(-4f, 1.2f, -4f), Quaternion.identity);
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < 10f && !brute.Attacks.IsAttacking) yield return null;
        Check(brute.Attacks.IsAttacking, "it attacks");
        yield return Shot("f3_brute_windup");
        var result = Hit(brute.Combatant, 10f, 500f);
        yield return null;
        Check(!brute.Combatant.IsStaggered && brute.Attacks.IsAttacking, $"a huge poise hit mid-swing doesn't stagger it ({result})");

        // Out of its swing it can be staggered again.
        start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < 5f && brute.Attacks.IsAttacking) yield return null;
        Hit(brute.Combatant, 1f, 500f);
        Check(brute.Combatant.IsStaggered, "between swings it can be staggered");

        int pickupsBefore = FindObjectsByType<ItemPickup>(FindObjectsSortMode.None).Length;
        Hit(brute.Combatant, 9999f, 0f);
        yield return Wait(1f);
        int pickupsAfter = FindObjectsByType<ItemPickup>(FindObjectsSortMode.None).Length;
        Check(brute.Combatant.IsDead && pickupsAfter > pickupsBefore, $"killing it drops loot ({pickupsAfter - pickupsBefore} pickups)");

        GoTo(10f);
        yield return Wait(1f);
        Check(!presence.Present, "it's gone by morning");
        GoTo(22.5f);
        yield return Wait(1f);
        Check(presence.Present && !brute.Combatant.IsDead && brute.Combatant.Health >= brute.Combatant.MaxHealth, "and back the next night");
        GoTo(10f);
    }

    HitResult Hit(Combatant target, float damage, float poise) =>
        target.ReceiveHit(new DamageInfo { Damage = damage, PoiseDamage = poise, Attacker = me, Knockback = Vector3.zero });

    void GoTo(float hour)
    {
        double now = clock.TotalMinutes % WorldClock.MinutesPerDay;
        double delta = hour * 60.0 - now;
        if (delta <= 0) delta += WorldClock.MinutesPerDay;
        clock.AdvanceMinutes(delta);
    }

    static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

    void Finish()
    {
        Debug.Log($"[FoeTest] done, failures={failures}");
        Application.Quit();
    }

    static IEnumerator Wait(float seconds) => new WaitForSecondsRealtime(seconds);

    IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();
        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
        Destroy(tex);
    }
}
