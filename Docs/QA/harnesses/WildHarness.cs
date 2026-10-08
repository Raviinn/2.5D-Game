using System;
using System.Collections;
using System.IO;
using System.Linq;
using Beast.Core;
using Beast.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Test-only: foraging and fishing (Milestone 47). Runs with -wildharness.</summary>
public sealed class WildHarness : MonoBehaviour
{
    string dir;
    int failures;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-wildharness") < 0) return;
        var go = new GameObject("WildHarness");
        DontDestroyOnLoad(go);
        go.AddComponent<WildHarness>();
    }

    void Check(bool ok, string what)
    {
        if (!ok) failures++;
        Debug.Log($"[WildTest] {(ok ? "PASS" : "FAIL")} {what}");
    }

    IEnumerator Start()
    {
        var args = Environment.GetCommandLineArgs();
        dir = args[Array.IndexOf(args, "-shots") + 1];
        Directory.CreateDirectory(dir);
        while (SceneManager.GetActiveScene().name != "World_Test" || GameObject.FindWithTag("Player") == null) yield return null;
        yield return Wait(3f);

        var player = GameObject.FindWithTag("Player");
        var motor = player.GetComponent<PlayerMotor>();
        var interactor = player.GetComponent<PlayerInteractor>();
        var inventory = player.GetComponent<Inventory>();
        var fishing = player.GetComponent<PlayerFishing>();
        var clock = Services.Get<WorldClock>();
        var database = Services.Get<GameDatabase>();
        player.GetComponent<Combatant>().Invulnerable = true;
        foreach (var e in EnemyController.Active.ToList()) e.enabled = false;

        // ---- Foraging ----
        var spots = FindObjectsByType<ForageSpot>(FindObjectsSortMode.None);
        Check(spots.Length >= 6, $"forage spots around the woods ({spots.Length})");
        if (spots.Length == 0) { Finish(); yield break; }
        var spot = spots[0];
        Check(spot.Ready && spot.TodaysFind(out var find) && find.Item.DisplayName == "Wild Garlic", "in spring there's wild garlic to gather");
        motor.Teleport(spot.transform.position + new Vector3(0f, 1.2f, -1.1f), Quaternion.identity);
        yield return Wait(0.6f);
        Check(interactor.Focus == spot && interactor.FocusPrompt.Text.StartsWith("Gather"), $"walking up offers \"{interactor.FocusPrompt.Text}\"");
        yield return Shot("w1_forage");
        database.TryGet("item_wildgarlic", out ItemData garlic);
        int before = inventory.CountOf(garlic);
        int got = spot.Gather(inventory);
        Check(got >= 1 && inventory.CountOf(garlic) == before + got, $"gathering gives {got} Wild Garlic");
        Check(!spot.Ready, "the spot is bare afterwards");
        clock.AdvanceMinutes(2 * 24 * 60);
        yield return Wait(0.3f);
        Check(!spot.Ready, "still bare two days later");
        clock.AdvanceMinutes(24 * 60);
        yield return Wait(1.2f);
        Check(spot.Ready, "and grown back on the third day");

        // ---- Fishing ----
        var pond = FindFirstObjectByType<FishingSpot>();
        Check(pond != null, "there's a fishing pond");
        if (pond == null) { Finish(); yield break; }
        Vector3 bank = pond.transform.position + new Vector3(0f, 1.2f, -(pond.Radius + 0.7f));
        motor.Teleport(bank, Quaternion.identity);
        yield return Wait(0.6f);
        Check(interactor.Focus == pond && !interactor.FocusPrompt.CanInteract && interactor.FocusPrompt.Text.Contains("Fishing Rod"),
            $"without a rod: \"{interactor.FocusPrompt.Text}\"");
        database.TryGet("item_fishingrod", out ItemData rod);
        inventory.Add(rod, 1);
        yield return Wait(0.3f);
        Check(interactor.FocusPrompt.CanInteract && interactor.FocusPrompt.Text == "Cast a line", "with a rod: Cast a line");

        // Too soon.
        pond.Interact(interactor);
        Check(fishing.Current == PlayerFishing.Phase.Waiting && interactor.Busy, "casting waits for a bite (other prompts are off)");
        yield return Wait(0.5f);
        fishing.Press();
        Check(!fishing.IsFishing && fishing.LastResult.Contains("Too soon"), $"striking too soon loses it (\"{fishing.LastResult}\")");

        // A good catch.
        int fishBefore = CountFish(inventory, database);
        pond.Interact(interactor);
        yield return WaitFor(() => fishing.Current == PlayerFishing.Phase.Bite, 8f);
        Check(fishing.Current == PlayerFishing.Phase.Bite, "a fish bites within a few seconds");
        yield return Shot("w2_bite");
        fishing.Press();
        Check(fishing.Current == PlayerFishing.Phase.Reeling, "pressing on the bite hooks it");
        for (int i = 0; i < 3 && fishing.IsFishing; i++)
        {
            yield return WaitFor(() => Mathf.Abs(fishing.Needle - fishing.MarkCentre) < fishing.MarkHalf * 0.5f, 6f);
            if (i == 1) yield return Shot("w3_reeling");
            fishing.Press();
            yield return null;
        }
        Check(!fishing.IsFishing && fishing.LastResult.StartsWith("Caught"), $"three presses on the gold land it (\"{fishing.LastResult}\")");
        Check(CountFish(inventory, database) == fishBefore + 1, "the fish is in the bag");
        Check(!interactor.Busy, "and Interact works normally again");

        // Two misses lose it.
        pond.Interact(interactor);
        yield return WaitFor(() => fishing.Current == PlayerFishing.Phase.Bite, 8f);
        fishing.Press();
        for (int i = 0; i < 2 && fishing.IsFishing; i++)
        {
            yield return WaitFor(() => Mathf.Abs(fishing.Needle - fishing.MarkCentre) > fishing.MarkHalf * 2f, 6f);
            fishing.Press();
            yield return null;
        }
        Check(!fishing.IsFishing && fishing.LastResult.Contains("slipped"), $"two misses and it gets away (\"{fishing.LastResult}\")");

        // Seasons and night: pike only after dark in autumn and winter.
        double now = clock.TotalMinutes;
        clock.AdvanceMinutes(28 * WorldClock.MinutesPerDay + 12 * 60 - now % WorldClock.MinutesPerDay + 0.0);
        yield return Wait(0.5f);
        Check(Calendar.Current == Season.Autumn, $"autumn now ({Calendar.DateText(clock.Day)})");
        int pikeByDay = 0, pikeByNight = 0, trout = 0;
        for (int i = 0; i < 300; i++)
        {
            if (pond.PickCatch(out var day, false) && day.Fish.DisplayName == "Night Pike") pikeByDay++;
            if (pond.PickCatch(out var night, true))
            {
                if (night.Fish.DisplayName == "Night Pike") pikeByNight++;
                if (night.Fish.DisplayName == "Silver Trout") trout++;
            }
        }
        Check(pikeByDay == 0 && pikeByNight > 30, $"night pike bite only at night ({pikeByDay} by day, {pikeByNight}/300 by night)");
        Check(trout == 0, "no trout in autumn");
        Check(spot.TodaysFind(out var autumn) && autumn.Item.DisplayName == "Forest Mushrooms", "autumn's forage is mushrooms");
        Finish();
    }

    static int CountFish(Inventory inventory, GameDatabase database)
    {
        int total = 0;
        foreach (var id in new[] { "item_riverperch", "item_silvertrout", "item_nightpike" })
            if (database.TryGet(id, out ItemData fish)) total += inventory.CountOf(fish);
        return total;
    }

    static IEnumerator WaitFor(Func<bool> condition, float timeout)
    {
        float end = Time.realtimeSinceStartup + timeout;
        while (!condition() && Time.realtimeSinceStartup < end) yield return null;
    }

    void Finish()
    {
        Debug.Log($"[WildTest] done, failures={failures}");
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
