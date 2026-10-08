using System;
using System.Collections;
using System.IO;
using System.Linq;
using Beast.Core;
using Beast.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Test-only: name plates behind walls, the lock-on bar, damage direction (Milestone 51). Runs with -hudharness.</summary>
public sealed class HudHarness : MonoBehaviour
{
    string dir;
    int failures;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-hudharness") < 0) return;
        var go = new GameObject("HudHarness");
        DontDestroyOnLoad(go);
        go.AddComponent<HudHarness>();
    }

    void Check(bool ok, string what)
    {
        if (!ok) failures++;
        Debug.Log($"[HudTest] {(ok ? "PASS" : "FAIL")} {what}");
    }

    IEnumerator Start()
    {
        var args = Environment.GetCommandLineArgs();
        dir = args[Array.IndexOf(args, "-shots") + 1];
        Directory.CreateDirectory(dir);
        while (SceneManager.GetActiveScene().name != "World_Test" || GameObject.FindWithTag("Player") == null) yield return null;
        yield return Wait(3f);

        var player = GameObject.FindWithTag("Player");
        var me = player.GetComponent<Combatant>();
        var motor = player.GetComponent<PlayerMotor>();
        var hud = FindFirstObjectByType<CombatDebugHUD>();
        var cam = Camera.main;
        me.Invulnerable = true;
        Check(hud != null, "the combat HUD is there");
        if (hud == null) { Finish(); yield break; }

        var bandit = EnemyController.Active.First(e => !e.Data.IsTrainingDummy && !e.Data.Ranged && !e.Data.Shield && !e.Data.PackHunter);
        var wolf = EnemyController.Active.FirstOrDefault(e => e.Data.PackHunter);
        Check(wolf == null || wolf.Data.PlateHeight < bandit.Data.PlateHeight, $"wolf plates sit lower than a bandit's ({wolf?.Data.PlateHeight} vs {bandit.Data.PlateHeight})");

        // ---- A plate behind a house is hidden ----
        var house = GameObject.Find("House_C");
        Vector3 housePos = house.transform.position;
        Vector3 standAt = housePos + new Vector3(0f, 0f, 9f);
        motor.Teleport(new Vector3(standAt.x, 1.2f, standAt.z), Quaternion.LookRotation(Vector3.back));
        // Point the camera at the house (south): set the orbit's yaw directly.
        typeof(ThirdPersonCamera).GetField("yaw", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(cam.GetComponent<ThirdPersonCamera>(), 180f);
        yield return Wait(1.2f);
        bandit.Combatant.ReceiveHit(new DamageInfo { Damage = 1f, Attacker = me }); // hurt: its plate shows at any range
        bandit.Warp(new Vector3(housePos.x, 1.2f, housePos.z - 6f), false);
        yield return Wait(0.6f);
        float hidden = hud.PlateAlpha(bandit);
        yield return Shot("h1_behind_house");
        Check(hidden < 0.2f, $"a bandit behind the house shows no plate (alpha {hidden:0.00})");
        bandit.Warp(new Vector3(standAt.x + 1.5f, 1.2f, standAt.z - 4f), false);
        yield return Wait(0.6f);
        Check(hud.PlateAlpha(bandit) > 0.8f, $"in the open it does ({hud.PlateAlpha(bandit):0.00})");

        // ---- Lock-on bar ----
        var lockOn = player.GetComponent<LockOnController>();
        lockOn.LockOnto(bandit.Combatant);
        yield return Wait(0.5f);
        Check(lockOn.Target == bandit.Combatant, "locked onto the bandit");
        yield return Shot("h2_lock_bar");
        lockOn.Release();

        // ---- Damage from the side ----
        me.Invulnerable = false;
        bandit.Warp(player.transform.position + cam.transform.right * 3f, false);
        yield return null;
        me.ReceiveHit(new DamageInfo { Damage = 1f, Attacker = bandit.Combatant });
        me.Heal(999f);
        me.Invulnerable = true;
        yield return Wait(0.15f);
        Check(hud.DamageMarksShowing == 1, $"a hit from the right shows a direction mark ({hud.DamageMarksShowing})");
        yield return Shot("h3_damage_direction");
        yield return Wait(1.5f);
        Check(hud.DamageMarksShowing == 0, "which fades after a moment");
        Finish();
    }

    void Finish()
    {
        Debug.Log($"[HudTest] done, failures={failures}");
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
