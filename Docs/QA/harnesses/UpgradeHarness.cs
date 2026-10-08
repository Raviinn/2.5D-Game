using System;
using System.Collections;
using System.IO;
using System.Linq;
using Beast.Core;
using Beast.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Test-only: homestead upgrades (Milestone 49). Runs with -upgradeharness.</summary>
public sealed class UpgradeHarness : MonoBehaviour
{
    string dir;
    int failures;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-upgradeharness") < 0) return;
        var go = new GameObject("UpgradeHarness");
        DontDestroyOnLoad(go);
        go.AddComponent<UpgradeHarness>();
    }

    void Check(bool ok, string what)
    {
        if (!ok) failures++;
        Debug.Log($"[UpgradeTest] {(ok ? "PASS" : "FAIL")} {what}");
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
        var bag = player.GetComponent<Inventory>();
        var clock = Services.Get<WorldClock>();
        var database = Services.Get<GameDatabase>();
        player.GetComponent<Combatant>().Invulnerable = true;
        foreach (var e in EnemyController.Active.ToList()) e.enabled = false;

        var plans = FindFirstObjectByType<HomesteadUpgrades>();
        var garden = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == "Kitchen_Garden" && t.parent == null)?.gameObject;
        Check(plans != null && plans.All.Count == 4, $"the homestead plans offer four improvements ({plans?.All.Count})");
        Check(garden != null && !garden.activeSelf, "the kitchen garden is hidden until built");
        if (plans == null || garden == null) { Finish(); yield break; }

        // The prompt and the window.
        motor.Teleport(plans.transform.position + plans.transform.forward * 1.3f + Vector3.up * 1.1f, Quaternion.LookRotation(-plans.transform.forward));
        yield return Wait(0.6f);
        Check(interactor.Focus == plans, $"walking up offers \"{interactor.FocusPrompt.Text}\"");
        database.TryGet("item_ironscrap", out ItemData scrap);
        database.TryGet("item_banditcloth", out ItemData cloth);
        Check(!plans.Build(HomesteadUpgradeKind.KitchenGarden, bag), "nothing is built without the gold and materials");

        // Pay partly from the chest.
        var chest = FindFirstObjectByType<StorageChest>().Contents;
        bag.AddGold(2000);
        bag.Add(scrap, 10);
        chest.Add(scrap, 20);
        bag.Add(cloth, 10);
        plans.Open();
        yield return Wait(0.5f);
        yield return Shot("u1_plans");
        Services.Get<GameStateService>().SetState(GameState.Playing);
        yield return Wait(0.3f);

        int gold = bag.Gold, bagScrap = bag.CountOf(scrap), chestScrap = chest.CountOf(scrap);
        Check(plans.Build(HomesteadUpgradeKind.KitchenGarden, bag), "build the Kitchen Garden");
        Check(bag.Gold == gold - 250 && bag.CountOf(scrap) + chest.CountOf(scrap) == bagScrap + chestScrap - 6, "it costs 250 gold and 6 Iron Scrap");
        Check(garden.activeSelf, "the garden appears");
        var gardenPlot = garden.GetComponentInChildren<FarmPlot>();
        Check(gardenPlot != null && gardenPlot.SaveId == "farm.garden", "with its own field");
        motor.Teleport(gardenPlot.transform.position + new Vector3(1.5f, 1.2f, -0.6f), Quaternion.identity);
        yield return Wait(0.6f);
        Check(interactor.Focus == gardenPlot && interactor.FocusPrompt.Text.StartsWith("Till"), $"its soil can be worked (\"{interactor.FocusPrompt.Text}\")");
        gardenPlot.Interact(interactor);
        yield return Wait(0.3f);
        motor.Teleport(gardenPlot.transform.position + new Vector3(2f, 1.2f, -3.5f), Quaternion.LookRotation(Vector3.forward));
        yield return Wait(0.8f);
        yield return Shot("u2_garden");
        Check(!plans.Build(HomesteadUpgradeKind.KitchenGarden, bag), "it can't be built twice");

        // Larger chest.
        int slots = chest.SlotCount;
        Check(plans.Build(HomesteadUpgradeKind.LargerChest, bag) && chest.SlotCount == 100, $"the Larger Chest holds 100 stacks ({slots} → {chest.SlotCount})");

        // Copper still: one extra serving.
        Check(plans.Build(HomesteadUpgradeKind.CopperStill, bag), "build the Copper Still");
        database.TryGet("item_healroot", out ItemData healroot);
        database.TryGet("item_healingdraught", out ItemData draught);
        var book = FindFirstObjectByType<Workbench>().Recipes;
        var recipe = book.Recipes.First(r => r != null && r.Output == draught);
        bag.Add(healroot, 2);
        int before = bag.CountOf(draught);
        Crafting.Craft(recipe, bag, player.GetComponent<PlayerEquipment>());
        Check(bag.CountOf(draught) == before + 2, $"alchemy now makes two draughts ({bag.CountOf(draught) - before})");

        // Hay loft: one fill, three days.
        Check(plans.Build(HomesteadUpgradeKind.HayLoft, bag), "build the Hay Loft");
        var trough = FindFirstObjectByType<FeedTrough>();
        database.TryGet("item_animalfeed", out ItemData feed);
        bag.Add(feed, 1);
        Check(trough.Fill(bag) && trough.DaysLeft == 3, "one fill lasts three days");
        clock.AdvanceMinutes(WorldClock.MinutesPerDay);
        yield return Wait(0.3f);
        Check(FarmAnimal.All.All(a => a.Fed), "the next day they're still fed");
        clock.AdvanceMinutes(WorldClock.MinutesPerDay);
        yield return Wait(0.3f);
        Check(FarmAnimal.All.All(a => a.Fed), "and the day after");
        clock.AdvanceMinutes(WorldClock.MinutesPerDay);
        yield return Wait(0.3f);
        Check(!trough.Filled && FarmAnimal.All.All(a => !a.Fed), "then it's empty");

        // Saved and loaded.
        var save = Services.Get<SaveService>();
        save.Save();
        yield return Wait(0.5f);
        save.Load();
        yield return Wait(1.5f);
        plans = FindFirstObjectByType<HomesteadUpgrades>();
        Check(plans.Has(HomesteadUpgradeKind.KitchenGarden) && plans.Has(HomesteadUpgradeKind.HayLoft), "the improvements are saved");
        Check(FindFirstObjectByType<StorageChest>().Contents.SlotCount == 100, "the chest stays large after loading");
        Check(garden.activeSelf, "the garden is still there");
        Finish();
    }

    void Finish()
    {
        Debug.Log($"[UpgradeTest] done, failures={failures}");
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
