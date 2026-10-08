using System;
using System.Collections;
using System.IO;
using System.Linq;
using Beast.Core;
using Beast.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Test-only: durability and repair (Milestone 44). Runs with -gearharness.</summary>
public sealed class GearHarness : MonoBehaviour
{
    string dir;
    int failures;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-gearharness") < 0) return;
        var go = new GameObject("GearHarness");
        DontDestroyOnLoad(go);
        go.AddComponent<GearHarness>();
    }

    void Check(bool ok, string what)
    {
        if (!ok) failures++;
        Debug.Log($"[GearTest] {(ok ? "PASS" : "FAIL")} {what}");
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
        var gear = player.GetComponent<GearCondition>();
        var equipment = player.GetComponent<PlayerEquipment>();
        var inventory = player.GetComponent<Inventory>();
        var stats = player.GetComponent<PlayerStats>();
        var database = Services.Get<GameDatabase>();
        Check(gear != null, "the player has Gear Condition");
        if (gear == null) { Finish(); yield break; }
        var weapon = equipment.ActiveWeapon;
        var body = equipment.ArmorAt(EquipSlot.Body);
        Check(weapon != null && weapon.Durability > 0 && body != null && body.Durability > 0,
            $"weapon and armour wear ({weapon?.DisplayName} {weapon?.Durability}, {body?.DisplayName} {body?.Durability})");
        var token = equipment.ArmorAt(EquipSlot.Accessory);
        database.TryGet("armor_soldierstoken", out EquipmentData soldiersToken);
        Check(soldiersToken == null || soldiersToken.Durability == 0, "accessories never wear");

        string lastMessage = null;
        Action<HudMessageEvent> onMessage = e => lastMessage = e.Text;
        EventBus<HudMessageEvent>.Subscribe(onMessage);

        // ---- Wear from fighting ----
        var bandit = EnemyController.Active.First(e => !e.Data.IsTrainingDummy && !e.Data.Ranged);
        var dummy = EnemyController.Active.First(e => e.Data.IsTrainingDummy);
        me.Invulnerable = true;
        bandit.Combatant.ReceiveHit(new DamageInfo { Damage = 1f, Attacker = me });
        float afterBlow = gear.ConditionOf(weapon);
        Check(Mathf.Abs(afterBlow - (1f - 1f / weapon.Durability)) < 0.001f, $"each blow you land wears your weapon a little ({afterBlow * 100f:0.0}%)");
        dummy.Combatant.ReceiveHit(new DamageInfo { Damage = 1f, Attacker = me });
        Check(Mathf.Abs(gear.ConditionOf(weapon) - afterBlow) < 0.0001f, "hitting the training dummy doesn't");
        me.Invulnerable = false;
        me.ReceiveHit(new DamageInfo { Damage = 1f, Attacker = bandit.Combatant });
        Check(gear.ConditionOf(body) < 1f, $"each hit you take wears your armour ({gear.ConditionOf(body) * 100f:0.0}%)");
        me.Heal(999f);
        me.Invulnerable = true;

        // ---- Worn, then broken ----
        float attackBefore = stats.Get(StatType.AttackPower);
        float defenseBefore = stats.Get(StatType.Defense);
        gear.Wear(weapon, weapon.Durability * (gear.ConditionOf(weapon) - 0.2f));
        Check(lastMessage != null && lastMessage.Contains("badly worn"), $"a warning when it's badly worn (\"{lastMessage}\")");
        Check(gear.ConditionText(weapon).Contains("Condition"), $"the bag shows its condition ({gear.ConditionText(weapon)})");
        gear.Wear(weapon, weapon.Durability);
        yield return null;
        Check(gear.IsBroken(weapon) && lastMessage.Contains("broke"), $"it breaks (\"{lastMessage}\")");
        // The Rusted Sword gives no bonus to lose; broken armour shows it: the Gambeson's Defense goes.
        gear.Wear(body, body.Durability);
        yield return null;
        float defenseBroken = stats.Get(StatType.Defense);
        Check(defenseBroken < defenseBefore, $"broken gear gives no bonus (Defense {defenseBefore:0} → {defenseBroken:0})");
        gear.Repair(body);

        // ---- Mend at the workbench with scrap ----
        database.TryGet("item_ironscrap", out ItemData scrap);
        int needed = gear.RepairScrap(weapon);
        Check(needed == gear.ScrapForFullRepair, $"a broken item takes {needed} Iron Scrap to mend");
        inventory.Add(scrap, needed);
        var bench = FindFirstObjectByType<Workbench>();
        motor.Teleport(bench.transform.position + bench.transform.forward * 1.4f + Vector3.up * 1.1f, Quaternion.LookRotation(-bench.transform.forward));
        yield return Wait(0.5f);
        bench.Open();
        yield return Wait(0.3f);
        var screen = FindFirstObjectByType<CraftingScreen>();
        screen.Select(CraftKind.Smithing);
        yield return Wait(0.5f);
        yield return Shot("g1_mend_rows");
        Check(gear.RepairWithScrap(weapon, scrap, bench.Storage) && gear.ConditionOf(weapon) >= 1f && inventory.CountOf(scrap) == 0,
            "mending uses the scrap and makes it as good as new");
        yield return null;
        Check(stats.Get(StatType.AttackPower) >= attackBefore - 0.01f, "and the bonus is back");
        Services.Get<GameStateService>().SetState(GameState.Playing);
        yield return Wait(0.3f);

        // ---- Brenna mends for gold ----
        gear.Wear(body, body.Durability * 0.5f);
        int price = gear.TotalRepairPrice();
        Check(price > 0, $"Brenna's price for the worn armour: {price} gold");
        inventory.AddGold(price + 50);
        int goldBefore = inventory.Gold;
        var brenna = GameObject.Find("Blacksmith");
        motor.Teleport(brenna.transform.position + brenna.transform.forward * 2f + Vector3.up * 0.1f, Quaternion.LookRotation(-brenna.transform.forward));
        yield return Wait(0.5f);
        var runner = Services.Get<DialogueRunner>();
        brenna.GetComponent<DialogueSpeaker>().Interact(player.GetComponent<PlayerInteractor>());
        float end = Time.realtimeSinceStartup + 5f;
        while (runner.IsActive && runner.Choices.Count == 0 && Time.realtimeSinceStartup < end) { runner.Advance(); yield return null; }
        int mend = runner.Choices.ToList().FindIndex(c => c.Contains("mend"));
        Check(mend >= 0 && runner.Choices[mend].Contains($"{price} gold"), $"Brenna offers to mend it ({(mend >= 0 ? runner.Choices[mend] : string.Join(" | ", runner.Choices))})");
        yield return Wait(1.5f);
        yield return Shot("g2_brenna_mend");
        if (mend >= 0)
        {
            runner.Choose(mend);
            yield return null;
            Check(gear.ConditionOf(body) >= 1f && inventory.Gold == goldBefore - price, $"she mends it for {goldBefore - inventory.Gold} gold");
            end = Time.realtimeSinceStartup + 5f;
            while (runner.IsActive && runner.Choices.Count == 0 && Time.realtimeSinceStartup < end) { runner.Advance(); yield return null; }
            Check(!runner.Choices.Any(c => c.Contains("mend")), "with nothing worn she doesn't offer again");
        }
        Services.Get<GameStateService>().SetState(GameState.Playing);
        yield return Wait(0.3f);

        // ---- Saved with the game ----
        gear.Wear(weapon, weapon.Durability * 0.3f);
        float saved = gear.ConditionOf(weapon);
        var save = Services.Get<SaveService>();
        save.Save();
        yield return Wait(0.5f);
        gear.Repair(weapon);
        save.Load();
        yield return Wait(1.5f);
        Check(Mathf.Abs(gear.ConditionOf(equipment.ActiveWeapon) - saved) < 0.01f, $"condition is saved ({saved * 100f:0}% → {gear.ConditionOf(equipment.ActiveWeapon) * 100f:0}%)");

        // ---- Forgotten once you own none ----
        weapon = equipment.ActiveWeapon;
        int index = equipment.ActiveWeaponIndex;
        equipment.UnequipWeapon(index);
        inventory.Remove(weapon, inventory.CountOf(weapon));
        yield return null;
        yield return null;
        inventory.Add(weapon, 1);
        Check(gear.ConditionOf(weapon) >= 1f, "a new one bought after selling the old one is pristine");

        EventBus<HudMessageEvent>.Unsubscribe(onMessage);
        Finish();
    }

    void Finish()
    {
        Debug.Log($"[GearTest] done, failures={failures}");
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
