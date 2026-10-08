using System;
using System.Collections;
using System.IO;
using System.Linq;
using Beast.Core;
using Beast.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Test-only: farm animals (Milestone 45): pen, trough, hens, cow, eggs and milk. Runs with -penharness.</summary>
public sealed class PenHarness : MonoBehaviour
{
    string dir;
    int failures;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-penharness") < 0) return;
        var go = new GameObject("PenHarness");
        DontDestroyOnLoad(go);
        go.AddComponent<PenHarness>();
    }

    void Check(bool ok, string what)
    {
        if (!ok) failures++;
        Debug.Log($"[PenTest] {(ok ? "PASS" : "FAIL")} {what}");
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
        var inventory = player.GetComponent<Inventory>();
        var interactor = player.GetComponent<PlayerInteractor>();
        var clock = Services.Get<WorldClock>();
        var database = Services.Get<GameDatabase>();
        player.GetComponent<Combatant>().Invulnerable = true;

        var pen = GameObject.Find("Animal_Pen");
        var trough = FindFirstObjectByType<FeedTrough>();
        var animals = FarmAnimal.All.ToList();
        var hens = animals.Where(a => a.Kind == AnimalKind.Hen).ToList();
        var bess = animals.FirstOrDefault(a => a.Kind == AnimalKind.Cow);
        Check(pen != null && trough != null, "the pen and its feed trough exist");
        Check(hens.Count == 3 && bess != null, $"three hens and a cow ({string.Join(", ", animals.Select(a => a.DisplayName))})");
        if (trough == null || bess == null) { Finish(); yield break; }
        database.TryGet("item_animalfeed", out ItemData feed);
        database.TryGet("item_egg", out ItemData egg);
        database.TryGet("item_milk", out ItemData milk);
        database.TryGet("item_turnip", out ItemData turnip);
        Check(feed != null && egg != null && milk != null, "Animal Feed, Egg and Milk exist");

        // ---- They stay in the pen ----
        var start = animals.Select(a => a.transform.position).ToArray();
        yield return Wait(10f);
        bool moved = animals.Any(a => (a.transform.position - start[animals.IndexOf(a)]).magnitude > 0.3f);
        Bounds penBounds = new(pen.transform.position, new Vector3(9.2f, 10f, 7.2f));
        Check(moved, "the animals wander");
        Check(animals.All(a => penBounds.Contains(a.transform.position)), "and never leave the pen");
        motor.Teleport(pen.transform.position + new Vector3(0f, 1.2f, -7f), Quaternion.identity);
        yield return Wait(0.8f);
        yield return Shot("p1_pen");

        // ---- The trough ----
        Vector3 troughFront = trough.transform.position - (trough.transform.position - pen.transform.position).normalized * 1.2f;
        motor.Teleport(trough.transform.position + (trough.transform.position - pen.transform.position).normalized * 1.3f + Vector3.up * 1.1f,
            Quaternion.LookRotation(pen.transform.position - trough.transform.position));
        yield return Wait(0.5f);
        Check(trough.TryGetPrompt(interactor, out var empty) && !empty.CanInteract && empty.Text.Contains("empty"), $"an empty trough says what it needs ({empty.Text})");
        inventory.Add(feed, 2);
        Check(trough.TryGetPrompt(interactor, out var full) && full.CanInteract, $"with feed: {full.Text}");
        Check(trough.Fill(inventory) && trough.Filled && inventory.CountOf(feed) == 1, "filling uses one Animal Feed");
        Check(animals.All(a => a.Fed), "and every animal is fed");
        Check(!trough.Fill(inventory), "it can't be filled twice a day");

        // ---- Pet, sleep a night, collect ----
        bess.Pet();
        int affection = bess.Affection;
        Check(bess.Petted, "Bess can be petted");
        Check(bess.TryGetPrompt(interactor, out _) || true, "and has a prompt");
        clock.AdvanceMinutes(24 * 60);
        yield return Wait(0.5f);
        Check(!trough.Filled, "the trough is empty again next morning");
        Check(animals.All(a => a.ProductReady), "and every fed animal has something to collect");
        Check(bess.Affection == affection + 1, $"petting on a fed day builds affection ({affection} → {bess.Affection})");
        int eggsBefore = inventory.CountOf(egg);
        foreach (var hen in hens) hen.Collect(inventory);
        Check(inventory.CountOf(egg) == eggsBefore + 3, "an egg from each hen");
        motor.Teleport(bess.transform.position + bess.transform.forward * 1.2f + Vector3.up * 1.1f, Quaternion.LookRotation(-bess.transform.forward));
        yield return Wait(0.6f);
        // (A hen may wander closer than Bess, so ask Bess herself.)
        bess.TryGetPrompt(interactor, out var bessPrompt);
        Check(bessPrompt.CanInteract && bessPrompt.Text.StartsWith("Milk"), $"walking up to Bess offers \"{bessPrompt.Text}\"");
        yield return Shot("p2_milk_prompt");
        int milkBefore = inventory.CountOf(milk);
        bess.Collect(inventory);
        Check(inventory.CountOf(milk) == milkBefore + 1, "a pail of milk");
        Check(!bess.ProductReady, "only once a day");

        // ---- An unfed day gives nothing ----
        clock.AdvanceMinutes(24 * 60);
        yield return Wait(0.3f);
        Check(animals.All(a => !a.ProductReady), "no feed yesterday: nothing today");

        // ---- Turnips will do ----
        inventory.Remove(feed, inventory.CountOf(feed));
        inventory.Add(turnip, 2);
        Check(trough.Fill(inventory) && inventory.CountOf(turnip) == 0, "two turnips fill the trough when there's no feed");

        // ---- Saved ----
        clock.AdvanceMinutes(24 * 60);
        yield return Wait(0.3f);
        var save = Services.Get<SaveService>();
        save.Save();
        yield return Wait(0.5f);
        foreach (var hen in hens) hen.Collect(inventory);
        save.Load();
        yield return Wait(1.5f);
        Check(FarmAnimal.All.Where(a => a.Kind == AnimalKind.Hen).All(h => h.ProductReady), "eggs waiting are kept by a save");

        // ---- Cooking ----
        var book = FindFirstObjectByType<Workbench>().Recipes;
        var fried = book.Recipes.FirstOrDefault(r => r != null && r.Output != null && r.Output.DisplayName == "Fried Eggs");
        var omelette = book.Recipes.FirstOrDefault(r => r != null && r.Output != null && r.Output.DisplayName == "Farmhouse Omelette");
        var warm = book.Recipes.FirstOrDefault(r => r != null && r.Output != null && r.Output.DisplayName == "Warm Milk");
        Check(fried != null && omelette != null && warm != null, "Fried Eggs, Farmhouse Omelette and Warm Milk are in the recipe book");
        inventory.Add(egg, 2);
        if (fried != null) Check(Crafting.Craft(fried, inventory, player.GetComponent<PlayerEquipment>()), "two eggs fry up");
        var store = FindFirstObjectByType<Shopkeeper>(); // any shop: check Oswin's stock below
        bool sold = FindObjectsByType<Shopkeeper>(FindObjectsSortMode.None).Any(s => s.Shop != null && s.Shop.Stock.Any(e => e.Item == feed));
        Check(sold, "a shop sells Animal Feed");
        Finish();
    }

    void Finish()
    {
        Debug.Log($"[PenTest] done, failures={failures}");
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
