using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Beast.Core;
using Beast.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Test-only: night danger and fatigue. Runs only with -nightharness.</summary>
public sealed class NightHarness : MonoBehaviour
{
    string dir;
    int failures;
    GameObject player;
    PlayerMotor motor;
    ThirdPersonCamera rig;
    WorldClock clock;
    readonly List<string> messages = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-nightharness") < 0) return;
        var go = new GameObject("NightHarness");
        DontDestroyOnLoad(go);
        go.AddComponent<NightHarness>();
    }

    void Check(bool ok, string what)
    {
        if (!ok) failures++;
        Debug.Log($"[NightTest] {(ok ? "PASS" : "FAIL")} {what}");
    }

    void OnEnable() => EventBus<HudMessageEvent>.Subscribe(OnMessage);
    void OnDisable() => EventBus<HudMessageEvent>.Unsubscribe(OnMessage);
    void OnMessage(HudMessageEvent evt) => messages.Add(evt.Text);

    void PlaceAt(Vector3 position, float yaw)
    {
        motor.Teleport(position, Quaternion.Euler(0f, yaw, 0f));
        typeof(ThirdPersonCamera).GetField("yaw", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(rig, yaw);
    }

    bool Said(string start) => messages.Exists(m => m.StartsWith(start));

    IEnumerator Start()
    {
        var args = Environment.GetCommandLineArgs();
        dir = args[Array.IndexOf(args, "-shots") + 1];
        Directory.CreateDirectory(dir);
        while (SceneManager.GetActiveScene().name != "World_Test" || GameObject.FindWithTag("Player") == null) yield return null;
        yield return new WaitForSecondsRealtime(3f);

        player = GameObject.FindWithTag("Player");
        motor = player.GetComponent<PlayerMotor>();
        rig = FindFirstObjectByType<ThirdPersonCamera>();
        clock = Services.Get<WorldClock>();
        var me = player.GetComponent<Combatant>();
        var stats = player.GetComponent<PlayerStats>();
        var stamina = player.GetComponent<Stamina>();
        var fatigue = player.GetComponent<PlayerFatigue>();
        EnemyController bandit = null;
        foreach (var e in EnemyController.Active) if (!e.Data.IsTrainingDummy) { bandit = e; break; }

        Check(fatigue != null, "player has PlayerFatigue");
        Check(fatigue.Level == FatigueLevel.Rested && fatigue.HoursAwake < 3f, $"starts rested ({fatigue.HoursAwake:0.0} h awake)");

        // ---- Day vs night bandits ----
        me.Invulnerable = true;
        Vector3 open = new Vector3(-12f, 1.1f, 20f);
        float probe = bandit.Data.AggroRange * 1.25f; // beyond day range, inside night range
        bandit.Warp(open + Vector3.right * probe, true);
        PlaceAt(open, 90f);
        yield return Wait(2f);
        Check(!bandit.Emboldened && Mathf.Approximately(bandit.Combatant.DamageMultiplier, 1f), "by day the bandit is normal");
        Check(bandit.StateName == "Idle", $"by day it ignores you at {probe:0.0} m ({bandit.StateName})");

        clock.AdvanceToHour(23);
        yield return Wait(2f);
        Check(bandit.Emboldened && bandit.Combatant.DamageMultiplier > 1.25f, $"at night it's bolder (damage x{bandit.Combatant.DamageMultiplier:0.00})");
        Check(bandit.StateName is "Chase" or "Attack", $"at night it spots you at {probe:0.0} m ({bandit.StateName})");
        Check(Said("Night falls"), "'Night falls' message");
        yield return Wait(1f);
        yield return Shot("1_night_bandit");

        // ---- Night loot: drop the table 40 times by night and 40 by day ----
        var dropper = bandit.GetComponent<LootDropper>();
        var drop = typeof(LootDropper).GetMethod("Drop", BindingFlags.Instance | BindingFlags.NonPublic);
        ClearPickups();
        for (int i = 0; i < 40; i++) drop.Invoke(dropper, null);
        yield return null;
        int nightDrops = FindObjectsByType<ItemPickup>(FindObjectsSortMode.None).Length;
        ClearPickups();
        clock.AdvanceToHour(12);
        yield return Wait(1f);
        Check(!bandit.Emboldened, "the bandit is back to normal at noon");
        Check(Said("Dawn breaks"), "'Dawn breaks' message");
        for (int i = 0; i < 40; i++) drop.Invoke(dropper, null);
        yield return null;
        int dayDrops = FindObjectsByType<ItemPickup>(FindObjectsSortMode.None).Length;
        ClearPickups();
        Check(nightDrops > dayDrops * 1.5f, $"night kills drop more: {nightDrops} pickups vs {dayDrops} by day (40 kills each)");
        bandit.Warp(new Vector3(42f, 1.1f, -42f), true);
        me.Invulnerable = false;

        // ---- Fatigue ----
        var bed = FindFirstObjectByType<SleepSpot>();
        var interactor = player.GetComponent<PlayerInteractor>();
        bed.Interact(interactor); // wake at 06:00, 0 h awake
        yield return Wait(0.5f);
        float baseStamina = stamina.Max, baseAttack = stats.Get(StatType.AttackPower);
        clock.AdvanceMinutes(19 * 60); // 01:00, 19 h awake
        yield return Wait(0.5f);
        Check(fatigue.Level == FatigueLevel.Tired, $"tired after 19 h awake ({fatigue.Level})");
        Check(stamina.Max < baseStamina * 0.9f && Mathf.Approximately(stats.Get(StatType.AttackPower), baseAttack), $"tired: stamina {baseStamina:0} -> {stamina.Max:0}, attack unchanged");
        Check(Said("You're getting tired"), "'getting tired' message");
        clock.AdvanceMinutes(4 * 60); // 23 h awake
        yield return Wait(0.5f);
        Check(fatigue.Level == FatigueLevel.Exhausted, $"exhausted after 23 h awake ({fatigue.Level})");
        Check(stamina.Max < baseStamina * 0.75f && stats.Get(StatType.AttackPower) < baseAttack * 0.9f,
            $"exhausted: stamina {stamina.Max:0}/{baseStamina:0}, attack {stats.Get(StatType.AttackPower):0}/{baseAttack:0}");
        PlaceAt(new Vector3(1f, 1.1f, -1f), 40f);
        yield return Wait(0.8f);
        yield return Shot("2_exhausted_badge");

        var save = Services.Get<SaveService>();
        Check(save.Save(1), "saved while exhausted");
        bed.Interact(interactor);
        yield return Wait(0.5f);
        Check(fatigue.Level == FatigueLevel.Rested && Mathf.Approximately(stamina.Max, baseStamina), $"sleeping cures it (stamina {stamina.Max:0})");
        save.Load(1);
        yield return Wait(1f);
        Check(fatigue.Level == FatigueLevel.Exhausted, $"load restores fatigue ({fatigue.Level}, {fatigue.HoursAwake:0.0} h)");
        Check(fatigue.HoursAwake < 25f, "loading doesn't count the clock jump as time awake");

        Debug.Log($"[NightTest] done, failures={failures}");
        Application.Quit();
    }

    static void ClearPickups()
    {
        foreach (var p in FindObjectsByType<ItemPickup>(FindObjectsSortMode.None)) DestroyImmediate(p.gameObject);
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
