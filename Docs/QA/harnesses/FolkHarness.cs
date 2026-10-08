using System;
using System.Collections;
using System.IO;
using System.Linq;
using Beast.Core;
using Beast.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Test-only: the new townsfolk (Milestone 43): Maren, Tobin, Captain Hale. Runs with -folkharness.</summary>
public sealed class FolkHarness : MonoBehaviour
{
    string dir;
    int failures;
    GameObject player;
    PlayerMotor motor;
    WorldClock clock;
    DialogueRunner runner;
    QuestLog log;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-folkharness") < 0) return;
        var go = new GameObject("FolkHarness");
        DontDestroyOnLoad(go);
        go.AddComponent<FolkHarness>();
    }

    void Check(bool ok, string what)
    {
        if (!ok) failures++;
        Debug.Log($"[FolkTest] {(ok ? "PASS" : "FAIL")} {what}");
    }

    IEnumerator Start()
    {
        var args = Environment.GetCommandLineArgs();
        dir = args[Array.IndexOf(args, "-shots") + 1];
        Directory.CreateDirectory(dir);
        while (SceneManager.GetActiveScene().name != "World_Test" || GameObject.FindWithTag("Player") == null) yield return null;
        yield return Wait(3f);

        player = GameObject.FindWithTag("Player");
        motor = player.GetComponent<PlayerMotor>();
        clock = Services.Get<WorldClock>();
        runner = Services.Get<DialogueRunner>();
        log = player.GetComponent<QuestLog>();
        player.GetComponent<Combatant>().Invulnerable = false;

        var maren = GameObject.Find("Healer");
        var tobin = GameObject.Find("Farmer");
        var hale = GameObject.Find("Guard");
        Check(maren != null && tobin != null && hale != null, "Maren, Tobin and Hale are in the world");
        if (maren == null || tobin == null || hale == null) { Finish(); yield break; }
        Check(new[] { maren, tobin, hale }.All(n => n.GetComponent<NpcSchedule>() != null && n.GetComponent<NpcSchedule>().EntryCount >= 4), "each keeps a daily routine");
        Check(maren.GetComponent<Shopkeeper>() != null && tobin.GetComponent<Shopkeeper>() == null && hale.GetComponent<Shopkeeper>() == null,
            "only Maren keeps a shop");

        // ---- Routines ----
        GoTo(10f);
        yield return Wait(1f);
        var haleSchedule = hale.GetComponent<NpcSchedule>();
        Check(haleSchedule.IsIndoors && hale.GetComponent<Interactable>().Unavailable, $"10:00 Hale sleeps in the tower ({haleSchedule.Activity})");
        Check(!maren.GetComponent<NpcSchedule>().IsIndoors && !tobin.GetComponent<NpcSchedule>().IsIndoors, "Maren and Tobin are out and about by day");
        GoTo(23f);
        yield return Wait(1f);
        Check(!haleSchedule.IsIndoors && haleSchedule.Activity == "Night watch", $"23:00 Hale keeps the night watch ({haleSchedule.Activity})");
        Check(maren.GetComponent<NpcSchedule>().IsIndoors && tobin.GetComponent<NpcSchedule>().IsIndoors, "Maren and Tobin are asleep at home");
        Stand(hale, 2f);
        yield return Wait(0.6f);
        yield return Shot("k1_hale_night");

        // ---- Hale: Shield Wall, then the Dead Wood ----
        yield return Talk(hale);
        Check(runner.IsActive && runner.CurrentSpeakerName == "Hale", $"Hale talks at night ({runner.CurrentSpeakerName})");
        yield return Pick("Need a hand");
        yield return Pick("take him on");
        var shieldWall = Quest("quest_shieldwall");
        Check(log.StatusOf(shieldWall) == QuestStatus.Active, "Shield Wall can be accepted");
        End();
        var shieldbearer = GameObject.Find("Bandit_Shieldbearer")?.GetComponent<EnemyController>();
        if (shieldbearer != null) shieldbearer.Combatant.ReceiveHit(new DamageInfo { Damage = 9999f, Attacker = player.GetComponent<Combatant>() });
        yield return Wait(0.5f);
        Check(log.StatusOf(shieldWall) == QuestStatus.Ready, "defeating the shieldbearer completes it");
        yield return Talk(hale);
        yield return SkipLines();
        Check(log.StatusOf(shieldWall) == QuestStatus.Completed, "Hale takes the report");
        Check(runner.Choices.Any(c => c.Contains("dead wood")), "and now mentions the dead wood");
        yield return Pick("dead wood");
        yield return Pick("hunt it");
        Check(log.StatusOf(Quest("quest_deadwood")) == QuestStatus.Active, "The Thing in the Dead Wood can be accepted");
        End();

        // ---- Maren: heals, sells, Herbs for Maren ----
        GoTo(10f);
        yield return Wait(1f);
        Stand(maren, 2f);
        yield return Wait(0.5f);
        var me = player.GetComponent<Combatant>();
        me.ReceiveWorldDamage(me.MaxHealth * 0.6f);
        yield return Talk(maren);
        Check(runner.CurrentSpeakerName == "Maren", "Maren talks by day");
        yield return Shot("k2_maren");
        yield return Pick("wounds");
        Check(me.Health >= me.MaxHealth - 0.5f, $"she tends your wounds ({me.Health:0}/{me.MaxHealth:0})");
        yield return Pick("help with anything");
        yield return Pick("bring you some");
        var herbs = Quest("quest_herbsformaren");
        Check(log.StatusOf(herbs) == QuestStatus.Active, "Herbs for Maren can be accepted");
        yield return Pick("for sale");
        yield return Wait(0.3f);
        Check(!runner.IsActive && Services.Get<GameStateService>().Current == GameState.InGameMenu, "she opens her shop");
        yield return Shot("k3_maren_shop");
        Services.Get<GameStateService>().SetState(GameState.Playing);
        yield return Wait(0.3f);

        var inventory = player.GetComponent<Inventory>();
        Services.Get<GameDatabase>().TryGet("item_healroot", out ItemData healroot);
        Services.Get<GameDatabase>().TryGet("item_healingdraught", out ItemData draught);
        inventory.Add(healroot, 3);
        yield return Wait(0.3f);
        Check(log.StatusOf(herbs) == QuestStatus.Ready, "three Healroot make it ready");
        int draughtsBefore = inventory.CountOf(draught);
        yield return Talk(maren);
        yield return SkipLines();
        Check(log.StatusOf(herbs) == QuestStatus.Completed && inventory.CountOf(healroot) == 0 && inventory.CountOf(draught) == draughtsBefore + 2,
            "handing in takes the Healroot and gives two draughts");
        End();

        // ---- Tobin: tips and Wolves at the Fold ----
        Stand(tobin, 2f);
        yield return Wait(0.5f);
        yield return Talk(tobin);
        Check(runner.CurrentSpeakerName == "Tobin", "Tobin talks by day");
        yield return Pick("advice");
        Check(runner.IsActive && runner.CurrentText.Length > 20, $"he shares a farming tip (\"{runner.CurrentText}\")");
        yield return SkipLines();
        yield return Pick("worried");
        yield return Pick("deal with them");
        var wolves = Quest("quest_wolvesatthefold");
        Check(log.StatusOf(wolves) == QuestStatus.Active, "Wolves at the Fold can be accepted");
        End();
        foreach (var name in new[] { "Wolf_A", "Wolf_B", "Wolf_C" })
            GameObject.Find(name)?.GetComponent<Combatant>().ReceiveHit(new DamageInfo { Damage = 9999f, Attacker = me });
        yield return Wait(0.5f);
        Check(log.StatusOf(wolves) == QuestStatus.Ready, "killing the three wolves completes it");
        yield return Talk(tobin);
        yield return SkipLines();
        Check(log.StatusOf(wolves) == QuestStatus.Completed, "Tobin rewards you");
        End();

        // Declining keeps the offer open (house rule for quest offers).
        Finish();
    }

    QuestData Quest(string id)
    {
        Services.Get<GameDatabase>().TryGet(id, out QuestData quest);
        return quest;
    }

    void Stand(GameObject npc, float distance)
    {
        motor.Teleport(npc.transform.position + npc.transform.forward * distance + Vector3.up * 0.1f, Quaternion.LookRotation(-npc.transform.forward));
    }

    IEnumerator Talk(GameObject npc)
    {
        npc.GetComponent<DialogueSpeaker>().Interact(player.GetComponent<PlayerInteractor>());
        yield return Wait(0.2f);
        yield return SkipLines();
    }

    /// <summary>Advances through lines until choices show (or the conversation ends).</summary>
    IEnumerator SkipLines()
    {
        float end = Time.realtimeSinceStartup + 10f;
        while (runner.IsActive && runner.Choices.Count == 0 && Time.realtimeSinceStartup < end)
        {
            runner.Advance();
            yield return null;
        }
    }

    /// <summary>Picks the first choice containing this text, then skips to the next choices.</summary>
    IEnumerator Pick(string text)
    {
        yield return SkipLines();
        int index = runner.Choices.ToList().FindIndex(c => c.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0);
        if (index < 0)
        {
            Check(false, $"choice \"{text}\" offered (had: {string.Join(" | ", runner.Choices)})");
            yield break;
        }
        runner.Choose(index);
        yield return null;
        if (!text.Contains("advice")) yield return SkipLines();
    }

    void End()
    {
        if (runner.IsActive) Services.Get<GameStateService>().SetState(GameState.Playing);
    }

    void GoTo(float hour)
    {
        double now = clock.TotalMinutes % WorldClock.MinutesPerDay;
        double delta = hour * 60.0 - now;
        if (delta <= 0) delta += WorldClock.MinutesPerDay;
        clock.AdvanceMinutes(delta);
    }

    void Finish()
    {
        Debug.Log($"[FolkTest] done, failures={failures}");
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
