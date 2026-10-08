using System;
using System.Collections;
using System.IO;
using Beast.Core;
using Beast.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Test-only: the workbench, recipes, buffs and in-place gear upgrades (Milestone 26). Runs only with -craftharness.</summary>
public sealed class CraftHarness : MonoBehaviour
{
    string dir;
    int failures;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-craftharness") < 0) return;
        var go = new GameObject("CraftHarness");
        DontDestroyOnLoad(go);
        go.AddComponent<CraftHarness>();
    }

    void Check(bool ok, string what)
    {
        if (!ok) failures++;
        Debug.Log($"[CraftTest] {(ok ? "PASS" : "FAIL")} {what}");
    }

    IEnumerator Start()
    {
        var args = Environment.GetCommandLineArgs();
        dir = args[Array.IndexOf(args, "-shots") + 1];
        Directory.CreateDirectory(dir);
        while (SceneManager.GetActiveScene().name != "World_Test" || GameObject.FindWithTag("Player") == null) yield return null;
        yield return new WaitForSecondsRealtime(3f);

        var player = GameObject.FindWithTag("Player");
        var inventory = player.GetComponent<Inventory>();
        var equipment = player.GetComponent<PlayerEquipment>();
        var buffs = player.GetComponent<PlayerBuffs>();
        var stats = player.GetComponent<PlayerStats>();
        var user = player.GetComponent<QuickItemUser>();
        var motor = player.GetComponent<PlayerMotor>();
        var interactor = player.GetComponent<PlayerInteractor>();
        var db = Services.Get<GameDatabase>();
        var state = Services.Get<GameStateService>();
        var bench = FindFirstObjectByType<Workbench>();
        var screen = FindFirstObjectByType<CraftingScreen>();

        Check(bench != null && bench.Recipes != null && bench.Recipes.Recipes.Length >= 8, $"workbench with {bench?.Recipes?.Recipes.Length} recipes");
        Check(buffs != null, "player has PlayerBuffs");
        Check(screen != null, "crafting screen exists");
        if (bench == null || buffs == null || screen == null) { Finish(); yield break; }

        db.TryGet("item_healroot", out ItemData healroot);
        db.TryGet("item_turnip", out ItemData turnip);
        db.TryGet("item_bread", out ItemData bread);
        db.TryGet("item_healingdraught", out ItemData draught);
        db.TryGet("item_turnipstew", out ConsumableData stew);
        db.TryGet("item_ironscrap", out ItemData scrap);
        db.TryGet("item_banditcloth", out ItemData cloth);
        db.TryGet("weapon_rustedswordshield", out WeaponData rusted);
        db.TryGet("weapon_ironswordshield", out WeaponData iron);
        Check(stew != null && stew.HasBuff && stew.Icon != null, "Turnip Stew exists, with a buff and an icon");
        RecipeData Find(string asset) => Array.Find(bench.Recipes.Recipes, r => r != null && r.name == asset);

        // ---- Walk up to the bench: the prompt shows ----
        Vector3 toBench = bench.transform.position - player.transform.position;
        toBench.y = 0f;
        motor.Teleport(bench.transform.position - toBench.normalized * 1.4f + Vector3.up * 1.1f, Quaternion.LookRotation(toBench));
        yield return Wait(0.6f);
        Check(bench.TryGetPrompt(interactor, out var prompt) && prompt.Text == "Use workbench", $"'Use workbench' prompt next to the bench ({prompt.Text})");
        yield return Shot("w0_bench");

        // ---- Alchemy: 2 Healroot -> Healing Draught ----
        Empty(inventory, healroot);
        inventory.Add(healroot, 4);
        int draughts = inventory.CountOf(draught);
        var draughtRecipe = Find("Recipe_HealingDraught");
        Check(Crafting.Craft(draughtRecipe, inventory, equipment) && Crafting.Craft(draughtRecipe, inventory, equipment),
            "brewed two Healing Draughts");
        Check(inventory.CountOf(healroot) == 0 && inventory.CountOf(draught) == draughts + 2,
            $"used 4 Healroot, gained 2 draughts (healroot {inventory.CountOf(healroot)}, draughts {draughts} -> {inventory.CountOf(draught)})");
        Check(!Crafting.Craft(draughtRecipe, inventory, equipment) && Crafting.Problem(draughtRecipe, inventory, equipment) == "Need 2× Healroot",
            $"can't brew without Healroot ({Crafting.Problem(draughtRecipe, inventory, equipment)})");

        // ---- Cooking + buff ----
        Empty(inventory, turnip);
        inventory.Add(turnip, 2);
        if (inventory.CountOf(bread) == 0) inventory.Add(bread, 1);
        Check(Crafting.Craft(Find("Recipe_TurnipStew"), inventory, equipment) && inventory.CountOf(stew) >= 1, "cooked a Turnip Stew");
        float defense = stats.Get(StatType.Defense);
        var combat = player.GetComponent<PlayerCombat>();
        yield return Wait(0.3f);
        Check(user.TryUse(stew), "ate the stew");
        yield return Wait(0.2f);
        Check(buffs.Buffs.Count == 1 && Mathf.Abs(stats.Get(StatType.Defense) - (defense + stew.Buff.Flat)) < 0.01f,
            $"stew buff: Defense {defense:0} -> {stats.Get(StatType.Defense):0}");
        yield return Shot("w1_buff_hud");
        buffs.Clear();
        yield return Wait(0.1f);
        Check(Mathf.Abs(stats.Get(StatType.Defense) - defense) < 0.01f, "buff gone: Defense back to normal");

        // ---- Smithing: upgrade the equipped rusted sword in place ----
        int slot = equipment.WeaponAt(0) == rusted ? 0 : equipment.WeaponAt(1) == rusted ? 1 : -1;
        Check(slot >= 0, $"rusted sword & shield is equipped (slot {slot})");
        Empty(inventory, scrap);
        Empty(inventory, cloth);
        inventory.Add(scrap, 6);
        inventory.Add(cloth, 2);
        var upgrade = Find("Recipe_IronSwordShield");
        Check(Crafting.Problem(upgrade, inventory, equipment) == null, $"upgrade possible with the sword equipped ({Crafting.Problem(upgrade, inventory, equipment)})");
        Check(Crafting.Craft(upgrade, inventory, equipment), "forged the Iron Sword & Shield");
        Check(slot >= 0 && equipment.WeaponAt(slot) == iron && inventory.CountOf(rusted) == 0 && inventory.CountOf(iron) == 0,
            $"the upgrade replaced the equipped sword in place (slot {slot}: {equipment.WeaponAt(Mathf.Max(slot, 0))?.DisplayName})");
        Check(inventory.CountOf(scrap) == 0 && inventory.CountOf(cloth) == 0, "scrap and cloth used up");

        // ---- The screen ----
        bench.Open();
        yield return Wait(0.3f);
        Check(state.Current == GameState.InGameMenu && screen.Bench == bench, "the workbench window opens (game paused)");
        inventory.Add(healroot, 3);
        inventory.Add(turnip, 1);
        screen.Select(CraftKind.Alchemy);
        yield return Wait(0.3f);
        yield return Shot("w2_alchemy");
        screen.Select(CraftKind.Cooking);
        yield return Wait(0.3f);
        yield return Shot("w3_cooking");
        inventory.Add(scrap, 3);
        screen.Select(CraftKind.Smithing);
        yield return Wait(0.3f);
        yield return Shot("w4_smithing");
        state.SetState(GameState.Playing);
        yield return Wait(0.3f);
        Check(screen.Bench == null, "the window closes when play resumes");

        yield return StorageTests(inventory, equipment, db, state, bench, healroot, draught, scrap, stew);
        Finish();
    }

    // ---------- Milestone 27: the storage chest ----------

    IEnumerator StorageTests(Inventory bag, PlayerEquipment equipment, GameDatabase db, GameStateService state, Workbench bench,
        ItemData healroot, ItemData draught, ItemData scrap, ConsumableData stew)
    {
        var chest = FindFirstObjectByType<StorageChest>();
        var screen = FindFirstObjectByType<StorageScreen>();
        Check(chest != null && chest.Contents.SlotCount == 60, $"storage chest with {chest?.Contents.SlotCount} slots");
        Check(screen != null, "storage screen exists");
        Check(bench.Storage != null && bench.Storage == chest?.Contents, "the workbench is linked to the chest");
        if (chest == null || screen == null) yield break;
        var box = chest.Contents;

        // Store goods: crops, seeds and materials go in; food and gear stay. No "+N" pickup notices.
        int notices = 0;
        void OnAdded(ItemsAddedEvent e) => notices++;
        EventBus<ItemsAddedEvent>.Subscribe(OnAdded);
        bag.Add(healroot, 3);
        bag.Add(scrap, 4);
        notices = 0;
        int food = bag.CountOf(stew);
        int moved = chest.StoreGoods(bag);
        Check(moved >= 7 && bag.CountOf(healroot) == 0 && bag.CountOf(scrap) == 0 && box.CountOf(healroot) >= 3 && box.CountOf(scrap) >= 4,
            $"'Store crops, seeds & materials' moved {moved} items into the chest");
        Check(bag.CountOf(stew) == food, "food stays in the bag");
        Check(notices == 0, $"moving items shows no pickup notices ({notices})");
        int took = StorageChest.Move(box, bag, scrap, 2);
        Check(took == 2 && bag.CountOf(scrap) == 2, "took 2 scrap back out");
        EventBus<ItemsAddedEvent>.Unsubscribe(OnAdded);

        // The workbench uses the chest's Healroot when the bag has none.
        int draughts = bag.CountOf(draught);
        int stored = box.CountOf(healroot);
        RecipeData recipe = Array.Find(bench.Recipes.Recipes, r => r != null && r.name == "Recipe_HealingDraught");
        Check(bag.CountOf(healroot) == 0 && Crafting.Craft(recipe, bag, equipment, bench.Storage) &&
              box.CountOf(healroot) == stored - 2 && bag.CountOf(draught) == draughts + 1,
            $"brewed a draught from Healroot in the chest (chest {stored} -> {box.CountOf(healroot)})");

        // Saved and loaded with the game.
        var save = Services.Get<SaveService>();
        int before = box.CountOf(scrap);
        Check(save.Save(1), "saved");
        box.Remove(scrap, before);
        save.Load(1);
        yield return Wait(1f);
        Check(chest.Contents.CountOf(scrap) == before, $"chest contents survive save/load (scrap {before} -> {chest.Contents.CountOf(scrap)})");

        chest.Open();
        yield return Wait(0.4f);
        Check(state.Current == GameState.InGameMenu && screen.Chest == chest, "the chest window opens (game paused)");
        yield return Shot("s1_storage");
        state.SetState(GameState.Playing);
        yield return Wait(0.2f);
        Check(screen.Chest == null, "the chest window closes when play resumes");
    }

    void Finish()
    {
        Debug.Log($"[CraftTest] done, failures={failures}");
        Application.Quit();
    }

    static void Empty(Inventory inventory, ItemData item)
    {
        if (item != null) inventory.Remove(item, inventory.CountOf(item));
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
