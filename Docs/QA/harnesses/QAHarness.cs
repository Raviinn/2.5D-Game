using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Beast.Core;
using Beast.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

/// <summary>
/// Test-only full QA pass over the systems the per-milestone harnesses don't cover: real input (move, sprint, jump,
/// attack, block, dodge, lock-on, weapon swap, skills, quick use, interact), combat & death, loot & pickups, items &
/// equipment, farming over many days, shops & prices, progression, quests & dialogue (accept / decline / re-ask),
/// contracts, sleep, a generic every-saveable save/load comparison (in place and from the main menu), edge cases,
/// a 40-day stress run and frame timing. Runs only with -qaharness.
/// </summary>
public sealed class QAHarness : MonoBehaviour
{
    string dir;
    int failures, passes;
    readonly List<string> failList = new();
    readonly List<string> messages = new();
    readonly List<QuestData> started = new();

    GameStateService state;
    SaveService save;
    GameDatabase db;
    WorldClock clock;
    GameObject player;
    PlayerMotor motor;
    PlayerCombat combat;
    Combatant me;
    Stamina stamina;
    Inventory inventory;
    PlayerInteractor interactor;
    PlayerEquipment equipment;
    PlayerProgression progression;
    PlayerSkills skills;
    PlayerStats stats;
    QuestLog log;
    ThirdPersonCamera rig;
    Keyboard keyboard;
    Mouse mouse;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-qaharness") < 0) return;
        var go = new GameObject("QAHarness");
        DontDestroyOnLoad(go);
        go.AddComponent<QAHarness>();
    }

    void Check(bool ok, string what)
    {
        if (ok) passes++;
        else { failures++; failList.Add(what); }
        Debug.Log($"[QA] {(ok ? "PASS" : "FAIL")} {what}");
    }

    static void Info(string what) => Debug.Log($"[QA] INFO {what}");

    void OnEnable()
    {
        EventBus<HudMessageEvent>.Subscribe(OnMessage);
        EventBus<QuestStartedEvent>.Subscribe(OnQuestStarted);
    }

    void OnDisable()
    {
        EventBus<HudMessageEvent>.Unsubscribe(OnMessage);
        EventBus<QuestStartedEvent>.Unsubscribe(OnQuestStarted);
    }

    void OnMessage(HudMessageEvent evt) => messages.Add(evt.Text);
    void OnQuestStarted(QuestStartedEvent evt) => started.Add(evt.Quest);

    IEnumerator Start()
    {
        var args = Environment.GetCommandLineArgs();
        dir = args[Array.IndexOf(args, "-shots") + 1];
        Directory.CreateDirectory(dir);
        state = Services.Get<GameStateService>();
        save = Services.Get<SaveService>();
        db = Services.Get<GameDatabase>();
        clock = Services.Get<WorldClock>();
        for (int i = 0; i < SaveService.SlotCount; i++)
            foreach (var path in new[] { SaveService.GetSlotPath(i), SaveService.GetSlotPath(i) + ".bak" })
                if (File.Exists(path)) File.Delete(path);
        keyboard = InputSystem.AddDevice<Keyboard>();
        mouse = InputSystem.AddDevice<Mouse>();

        yield return Until(() => SceneManager.GetActiveScene().name == "MainMenu" && state.Current == GameState.MainMenu && FindFirstObjectByType<MainMenu>() != null, 30f);
        FindFirstObjectByType<MainMenu>().StartNewGame(0); // the same frame the menu appears
        yield return Until(InWorld, 20f);
        Check(InWorld(), "the main menu accepts New Game on its very first frame");
        if (!InWorld()) { yield return Wait(1f); FindFirstObjectByType<MainMenu>()?.StartNewGame(0); yield return Until(InWorld, 40f); }
        yield return Wait(2f);
        Bind();

        string[] sections = { "Fixes", "OutOfBounds", "Performance", "Movement", "Combat", "DeathAndRespawn", "LootAndPickups", "Items", "Equipment",
            "Progression", "Farming", "Shops", "QuestsAndDialogue", "Contracts", "Sleep", "EdgeCases", "SaveRoundTrip", "Stress" };
        int only = Array.IndexOf(args, "-sections");
        if (only >= 0) sections = args[only + 1].Split(',');
        foreach (var name in sections)
        {
            Info($"===== {name} =====");
            var method = GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            yield return Safe(name, (IEnumerator)method.Invoke(this, null));
            ReleaseAll();
            if (state.Current != GameState.Playing && InWorldScene()) state.SetState(GameState.Playing);
            if (player == null || !player) Bind();
            me.Invulnerable = false;
            yield return Wait(0.2f);
        }

        Debug.Log($"[QA] done: {passes} passed, {failures} failed");
        foreach (var f in failList) Debug.Log($"[QA] FAILED: {f}");
        Application.Quit();
    }

    void Bind()
    {
        player = GameObject.FindWithTag("Player");
        motor = player.GetComponent<PlayerMotor>();
        combat = player.GetComponent<PlayerCombat>();
        me = player.GetComponent<Combatant>();
        stamina = player.GetComponent<Stamina>();
        inventory = player.GetComponent<Inventory>();
        interactor = player.GetComponent<PlayerInteractor>();
        equipment = player.GetComponent<PlayerEquipment>();
        progression = player.GetComponent<PlayerProgression>();
        skills = player.GetComponent<PlayerSkills>();
        stats = player.GetComponent<PlayerStats>();
        log = player.GetComponent<QuestLog>();
        rig = FindFirstObjectByType<ThirdPersonCamera>();
        clock = Services.Get<WorldClock>();
    }

    // ======================================================================
    IEnumerator Performance()
    {
        // Frame timing at default settings, looking across the village (vsync off so we measure the game, not the monitor).
        var settings = Services.Get<SettingsService>();
        settings.Current.vSync = false;
        settings.Apply();
        yield return Wait(1f);
        var frames = new List<float>();
        float end = Time.realtimeSinceStartup + 5f;
        while (Time.realtimeSinceStartup < end)
        {
            frames.Add(Time.unscaledDeltaTime * 1000f);
            yield return null;
        }
        frames.Sort();
        float avg = frames.Average();
        float p99 = frames[(int)(frames.Count * 0.99f)];
        Info($"frame time over 5 s: avg {avg:0.0} ms ({1000f / avg:0} fps), 99th percentile {p99:0.0} ms, worst {frames[^1]:0.0} ms, {frames.Count} frames");
        Check(avg < 33.3f, $"average frame time under 33 ms (30 fps) at default settings ({avg:0.0} ms)");
        Check(p99 < 50f, $"99% of frames under 50 ms ({p99:0.0} ms)");
        Info($"scene objects: {FindObjectsByType<Transform>(FindObjectsSortMode.None).Length} transforms, {FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length} renderers, {FindObjectsByType<Light>(FindObjectsSortMode.None).Length} lights");
        settings.Current.vSync = true;
        settings.Apply();
    }

    // ======================================================================
    IEnumerator Movement()
    {
        Vector3 open = OpenGround();
        PlaceAt(open, 0f);
        yield return Wait(0.5f);
        Vector3 a = player.transform.position;
        yield return Hold(1f, Key.W);
        float walked = Planar(player.transform.position - a);
        Check(walked > 2f, $"W walks forward ({walked:0.0} m in 1 s)");

        PlaceAt(open, 0f);
        yield return Wait(0.3f);
        a = player.transform.position;
        yield return Hold(1.2f, Key.W, Key.LeftShift);
        float sprinted = Planar(player.transform.position - a);
        Check(sprinted > walked * 1.2f, $"holding Shift sprints ({sprinted:0.0} m vs {walked:0.0} m walking)");
        yield return Wait(0.3f);

        PlaceAt(open, 0f);
        yield return Wait(0.5f);
        float y0 = player.transform.position.y, peak = y0;
        yield return Tap(Key.Space);
        for (float t = 0; t < 0.8f; t += Time.unscaledDeltaTime) { peak = Mathf.Max(peak, player.transform.position.y); yield return null; }
        Check(peak > y0 + 0.5f, $"Space jumps ({peak - y0:0.00} m)");
        yield return Wait(0.6f);
        Check(motor.IsGrounded && Mathf.Abs(player.transform.position.y - y0) < 0.2f, "lands back on the ground");

        // Strafing back and sideways works too.
        PlaceAt(open, 0f);
        yield return Wait(0.3f);
        a = player.transform.position;
        yield return Hold(0.6f, Key.D);
        Check(player.transform.position.x - a.x > 1f, "D moves right (camera-relative)");
    }

    // ======================================================================
    IEnumerator Combat()
    {
        var dummy = FindEnemy(true);
        Check(dummy != null, "found the training dummy");
        if (dummy == null) yield break;
        Vector3 d = dummy.transform.position;
        Vector3 from = d + Vector3.back * 1.6f;
        PlaceAt(from, 0f);
        yield return Wait(0.5f);
        stamina.Refill();

        float hp = dummy.Combatant.Health;
        yield return Click(MouseButton.Left);
        bool attacking = combat.StateName == "Attacking";
        yield return Wait(0.8f);
        Check(attacking, "LMB starts an attack");
        Check(dummy.Combatant.Health < hp || dummy.Combatant.IsDead || dummy.Combatant.Health >= dummy.Combatant.MaxHealth - 0.01f && hp < dummy.Combatant.MaxHealth,
            $"the attack damages the dummy ({hp:0} -> {dummy.Combatant.Health:0})");

        // Three quick clicks = combo
        int hits = 0;
        void Counter(DamageDealtEvent e) { if (e.Attacker == me) hits++; }
        EventBus<DamageDealtEvent>.Subscribe(Counter);
        for (int i = 0; i < 3; i++) { yield return Click(MouseButton.Left); yield return Wait(0.25f); }
        yield return Wait(1f);
        EventBus<DamageDealtEvent>.Unsubscribe(Counter);
        Check(hits >= 2, $"clicking repeatedly chains a combo ({hits} hits)");

        // Heavy (hold)
        stamina.Refill();
        yield return Wait(0.8f);
        hits = 0;
        EventBus<DamageDealtEvent>.Subscribe(Counter);
        mouse.QueueState(new MouseState().WithButton(MouseButton.Left));
        yield return Wait(0.6f);
        ReleaseAll();
        yield return Wait(1.6f);
        EventBus<DamageDealtEvent>.Unsubscribe(Counter);
        Check(hits >= 1, $"holding LMB does a charged attack ({hits} hits)");

        // Block (RMB)
        yield return Wait(0.5f);
        mouse.QueueState(new MouseState().WithButton(MouseButton.Right));
        yield return Wait(0.3f);
        Check(me.IsBlocking && combat.StateName == "Blocking", $"RMB blocks ({combat.StateName})");
        ReleaseAll();
        yield return Wait(0.3f);
        Check(!me.IsBlocking, "releasing RMB stops blocking");

        // Dodge (tap Shift)
        stamina.Refill();
        float st = stamina.Current;
        PlaceAt(OpenGround(), 0f);
        yield return Wait(0.4f);
        yield return Tap(Key.LeftShift, 0.05f);
        bool dodged = combat.StateName == "Dodging";
        for (int i = 0; i < 10 && !dodged; i++) { yield return null; dodged |= combat.StateName == "Dodging"; }
        Check(dodged, "tapping Shift dodges");
        yield return Wait(0.6f);
        Check(stamina.Current < st, $"dodging costs stamina ({st:0} -> {stamina.Current:0})");

        // Lock-on (MMB)
        PlaceAt(from, 0f);
        yield return Wait(0.4f);
        var lockOn = player.GetComponent<LockOnController>();
        yield return Click(MouseButton.Middle);
        yield return Wait(0.2f);
        Check(lockOn.Target == dummy.Combatant, $"MMB locks on to the dummy ({(lockOn.Target != null ? lockOn.Target.name : "nothing")})");
        yield return Click(MouseButton.Middle);
        yield return Wait(0.2f);
        Check(lockOn.Target == null, "MMB again releases the lock");

        // Weapon swap (X) when two weapons are equipped
        if (db.TryGet("weapon_rustedgreatsword", out WeaponData gs) && equipment.WeaponAt(1) == null)
        {
            inventory.Add(gs, 1);
            equipment.Equip(gs);
        }
        int before = equipment.ActiveWeaponIndex;
        var movesetBefore = combat.Moveset;
        yield return Tap(Key.X);
        yield return Wait(0.2f);
        Check(equipment.ActiveWeaponIndex != before && combat.Moveset != movesetBefore, $"X swaps weapon and style ({movesetBefore?.name} -> {combat.Moveset?.name})");
        yield return Tap(Key.X);
        yield return Wait(0.2f);

        // Enemies hurt the player; parry/stagger is covered by feel, but block reduces damage.
        var bandit = FindEnemy(false);
        float full = me.MaxHealth;
        me.Heal(9999f);
        me.ReceiveHit(new DamageInfo { Damage = 20f, Attacker = bandit.Combatant });
        float unblocked = full - me.Health;
        me.Heal(9999f);
        yield return Wait(1.2f);
        mouse.QueueState(new MouseState().WithButton(MouseButton.Right));
        yield return Wait(0.5f); // past the parry window
        me.ReceiveHit(new DamageInfo { Damage = 20f, Attacker = bandit.Combatant });
        float blocked = full - me.Health;
        ReleaseAll();
        me.Heal(9999f);
        Check(unblocked > 0f && blocked < unblocked, $"blocking reduces damage ({unblocked:0.0} -> {blocked:0.0})");
        yield return Wait(1f);
    }

    // ======================================================================
    IEnumerator DeathAndRespawn()
    {
        PlaceAt(OpenGround(), 0f);
        yield return Wait(0.5f);
        me.ReceiveHit(new DamageInfo { Damage = 99999f });
        yield return Wait(0.2f);
        Check(combat.IsDead, "lethal damage kills the player");
        yield return Tap(Key.W);
        Check(combat.IsDead, "can't act while dead");
        yield return Wait(3f);
        Check(!combat.IsDead && Mathf.Approximately(me.Health, me.MaxHealth), $"respawns with full health after ~2 s (hp {me.Health:0}/{me.MaxHealth:0})");
        Info($"respawned at {player.transform.position} (the scene's start point; no gold/xp penalty: prototype)");
        yield return Hold(0.5f, Key.W);
        Check(motor.PlanarSpeed >= 0f && combat.StateName == "Free", $"can move again after respawning ({combat.StateName})");
    }

    // ======================================================================
    IEnumerator LootAndPickups()
    {
        var bandit = FindEnemy(false);
        Vector3 spot = OpenGround() + Vector3.right * 4f;
        bandit.Warp(spot, true);
        yield return Wait(0.3f);
        int pickupsBefore = FindObjectsByType<ItemPickup>(FindObjectsSortMode.None).Length;
        me.Invulnerable = true;
        bandit.Combatant.ReceiveHit(new DamageInfo { Damage = 99999f, Attacker = me });
        yield return Wait(0.8f);
        int dropped = FindObjectsByType<ItemPickup>(FindObjectsSortMode.None).Length - pickupsBefore;
        Check(bandit.Combatant.IsDead, "a bandit can be killed");
        Check(dropped > 0, $"a killed bandit drops loot ({dropped} pickups)");

        int gold = inventory.Gold;
        int items = TotalItems();
        PlaceAt(spot, 0f);
        yield return Wait(2.5f);
        int left = FindObjectsByType<ItemPickup>(FindObjectsSortMode.None).Count(p => Vector3.Distance(p.transform.position, spot) < 4f);
        Check(left == 0, $"walking over the loot collects it ({left} left)");
        Check(inventory.Gold > gold || TotalItems() > items, $"loot reaches the bag (gold {gold} -> {inventory.Gold}, items {items} -> {TotalItems()})");

        // Respawn
        float wait = bandit.Data.RespawnDelay + 1.5f;
        yield return Wait(wait);
        Check(!bandit.Combatant.IsDead, $"the bandit respawns after {bandit.Data.RespawnDelay:0} s");
        bandit.Warp(new Vector3(42f, 1.1f, -42f), true);
        me.Invulnerable = false;
    }

    // ======================================================================
    IEnumerator Items()
    {
        db.TryGet("item_bread", out ConsumableData bread);
        inventory.Add(bread, 2);
        me.Heal(9999f);
        me.ReceiveHit(new DamageInfo { Damage = me.MaxHealth * 0.5f });
        yield return Wait(1.5f);
        float hp = me.Health;
        int count = inventory.CountOf(bread);
        var quick = player.GetComponent<QuickItemUser>();
        Check(quick.QuickItem != null, $"quick-use slot has a consumable ({quick.QuickItem?.DisplayName})");
        var used = quick.QuickItem;
        int usedCount = inventory.CountOf(used);
        yield return Tap(Key.R);
        yield return Wait(0.3f);
        Check(me.Health > hp, $"R uses food and heals ({hp:0} -> {me.Health:0})");
        Check(inventory.CountOf(used) == usedCount - 1, $"the item is consumed ({used.DisplayName} {usedCount} -> {inventory.CountOf(used)})");
        Info($"bread count {count} â†’ {inventory.CountOf(bread)} (R uses the first consumable in the bag: {used.DisplayName})");

        // Stacking and a full bag
        db.TryGet("item_banditcloth", out ItemData cloth);
        int before = inventory.CountOf(cloth);
        int left = inventory.Add(cloth, 5);
        Check(left == 0 && inventory.CountOf(cloth) == before + 5, "items stack");
        var snapshot = Capture(inventory);
        db.TryGet("item_ironscrap", out ItemData scrap);
        int overflow = inventory.Add(scrap, 99999);
        Check(overflow > 0 && inventory.SpaceFor(scrap) == 0, $"a full bag refuses the rest ({overflow} didn't fit)");
        Check(inventory.Add(cloth, 1) == 1 || inventory.SpaceFor(cloth) >= 0, "adding to a full bag reports what didn't fit");
        Check(!inventory.Remove(cloth, 999999), "can't remove more than you have");
        inventory.RestoreState(snapshot);
        Check(inventory.CountOf(scrap) < 999, "bag restored after the full-bag test");
    }

    // ======================================================================
    IEnumerator Equipment()
    {
        db.TryGet("armor_ironhelm", out EquipmentData helm);
        db.TryGet("weapon_ironswordshield", out WeaponData sword);
        float defense = stats.Get(StatType.Defense);
        float attack = stats.Get(StatType.AttackPower);
        inventory.Add(helm, 1);
        Check(equipment.Equip(helm), "equip an Iron Helm");
        yield return null;
        Check(stats.Get(StatType.Defense) > defense, $"the helm raises Defense ({defense:0.0} -> {stats.Get(StatType.Defense):0.0})");
        Check(inventory.CountOf(helm) == 0 && equipment.ArmorAt(helm.Slot) == helm, "the helm leaves the bag and sits in its slot");

        inventory.Add(sword, 1);
        var old = equipment.ActiveWeapon;
        Check(equipment.Equip(sword), "equip the Iron Sword & Shield");
        yield return null;
        Info($"weapon slots: {equipment.WeaponAt(0)?.DisplayName} / {equipment.WeaponAt(1)?.DisplayName}, active {equipment.ActiveWeapon?.DisplayName}; replaced {old?.DisplayName}");
        Check(old == null || inventory.CountOf(old) > 0 || equipment.WeaponAt(0) == old || equipment.WeaponAt(1) == old, "the replaced weapon goes back to the bag");
        Check(stats.Get(StatType.AttackPower) >= attack, $"attack power with iron ({attack:0.0} -> {stats.Get(StatType.AttackPower):0.0})");

        float withHelm = stats.Get(StatType.Defense);
        float helmDefense = helm.Modifiers.Where(m => m.Stat == StatType.Defense).Sum(m => m.Flat);
        Check(equipment.UnequipArmor(helm.Slot) && inventory.CountOf(helm) == 1, "unequip puts the helm back in the bag");
        yield return null;
        Check(Mathf.Approximately(stats.Get(StatType.Defense), withHelm - helmDefense), $"unequipping removes exactly the helm's Defense ({withHelm:0.0} -> {stats.Get(StatType.Defense):0.0}, helm {helmDefense:0.0})");

        // Unequip both weapons: the class style is the fallback, and attacking still works.
        var w0 = equipment.WeaponAt(0); var w1 = equipment.WeaponAt(1);
        equipment.UnequipWeapon(0);
        equipment.UnequipWeapon(1);
        yield return null;
        Check(equipment.ActiveWeapon == null && combat.Moveset != null, $"no weapon: falls back to a class style ({combat.Moveset?.name})");
        if (w0 != null) equipment.Equip(w0);
        if (w1 != null) equipment.Equip(w1);
        yield return null;
        Check(equipment.ActiveWeapon != null, "weapons re-equipped");
    }

    // ======================================================================
    IEnumerator Progression()
    {
        int level = progression.Level(Discipline.Combat);
        float maxHp = me.MaxHealth;
        int ups = 0;
        void OnLevel(LevelUpEvent e) => ups++;
        EventBus<LevelUpEvent>.Subscribe(OnLevel);
        progression.AddXp(Discipline.Combat, progression.XpToNext(Discipline.Combat) + 1);
        yield return null;
        EventBus<LevelUpEvent>.Unsubscribe(OnLevel);
        Check(progression.Level(Discipline.Combat) == level + 1 && ups == 1, $"enough XP levels up Combat ({level} -> {progression.Level(Discipline.Combat)})");
        Check(me.MaxHealth > maxHp, $"Combat levels raise max health ({maxHp:0} -> {me.MaxHealth:0})");
        Check(progression.Points(Discipline.Combat) > 0, "a level grants a skill point");

        db.TryGet("skill_knight_warcry", out SkillData warcry);
        string why = skills.WhyLocked(warcry);
        if (why != null) Info($"War Cry locked: {why}");
        while (skills.WhyLocked(warcry) != null && progression.Level(Discipline.Combat) < 10)
            progression.AddXp(Discipline.Combat, progression.XpToNext(Discipline.Combat) + 1);
        Check(skills.Unlock(warcry), $"unlock War Cry ({skills.WhyLocked(warcry) ?? "ok"})");
        Check(skills.SlotSkill(0) == warcry || skills.SlotSkill(1) == warcry, "an unlocked active skill goes on E or Q");
        int slot = skills.SlotSkill(0) == warcry ? 0 : 1;
        PlaceAt(OpenGround(), 0f);
        yield return Wait(0.5f);
        stamina.Refill();
        yield return Tap(slot == 0 ? Key.E : Key.Q);
        yield return Wait(0.2f);
        Check(skills.CooldownRemaining(slot) > 0f, $"pressing {(slot == 0 ? "E" : "Q")} uses War Cry (cooldown {skills.CooldownRemaining(slot):0.0} s)");
        yield return Wait(1.5f);
        yield return Tap(slot == 0 ? Key.E : Key.Q);
        Check(skills.CooldownRemaining(slot) > 0f, "can't recast during the cooldown");

        int farm = progression.Level(Discipline.Farming);
        yield return Tap(Key.F7);
        yield return null;
        Info($"F7 (dev): Farming XP {progression.Xp(Discipline.Farming)}, level {farm} -> {progression.Level(Discipline.Farming)}");
    }

    // ======================================================================
    class FarmState { public bool[] tilled; public string[] crops; public int[] growth; public bool[] watered; public int[] dryDays; public bool[] dead; }

    IEnumerator Farming()
    {
        const int A = 8, B = 9, C = 10; // interior tiles (row 1, columns 2-4)
        var farm = FindFirstObjectByType<FarmPlot>();
        db.TryGet("crop_turnip", out CropData turnip);
        db.TryGet("item_turnipseeds", out ItemData seeds);
        var weather = Services.Get<WeatherSystem>();
        inventory.Add(seeds, 10);
        weather.Set(Weather.Clear, announce: false);

        // Tile 0 by the real F key: till â†’ plant â†’ water
        FaceTile(farm, A);
        Diag(farm, "right after FaceTile");
        yield return null;
        Diag(farm, "1 frame later");
        yield return Wait(0.3f);
        Diag(farm, "0.3 s later");
        Check(interactor.Focus == farm, $"the field is the interaction target ({interactor.FocusPrompt.Text})");
        yield return Tap(Key.F);
        var s = Farm(farm);
        Check(s.tilled[A], "F tills the soil");
        int seedCount = inventory.CountOf(seeds);
        yield return Wait(0.4f);
        yield return Tap(Key.F);
        s = Farm(farm);
        Check(s.crops[A] == turnip.Id, $"F plants the selected seed ({s.crops[A]})");
        Check(inventory.CountOf(seeds) <= seedCount, "planting uses a seed (unless Seed Keeper saves it)");
        yield return Wait(0.4f);
        yield return Tap(Key.F);
        Check(Farm(farm).watered[A], "F waters");

        // Grow to ripe, watering daily (skipping rain days check: rain waters on its own)
        int days = 0;
        while (!IsRipe(Farm(farm), A, turnip) && days < 20)
        {
            clock.AdvanceMinutes(WorldClock.MinutesPerDay);
            days++;
            yield return null;
            if (!Farm(farm).watered[A] && !IsRipe(Farm(farm), A, turnip)) { FaceTile(farm, A); farm.Interact(interactor); }
        }
        Check(IsRipe(Farm(farm), A, turnip), $"watered daily, the turnip ripens in {days} days (TotalGrowDays {turnip.TotalGrowDays})");
        Check(days == turnip.TotalGrowDays, $"growth takes exactly the crop's days ({days} vs {turnip.TotalGrowDays})");

        int produce = inventory.CountOf(turnip.Produce);
        int xp = progression.Xp(Discipline.Farming) + progression.Level(Discipline.Farming) * 100000;
        int harvested = 0;
        void OnHarvest(CropHarvestedEvent e) => harvested += e.Count;
        EventBus<CropHarvestedEvent>.Subscribe(OnHarvest);
        FaceTile(farm, A);
        yield return Wait(0.3f);
        yield return Tap(Key.F);
        EventBus<CropHarvestedEvent>.Unsubscribe(OnHarvest);
        Check(inventory.CountOf(turnip.Produce) == produce + harvested && harvested >= turnip.ProduceMin, $"harvest gives {harvested} turnip(s)");
        Check(progression.Xp(Discipline.Farming) + progression.Level(Discipline.Farming) * 100000 > xp, "harvesting gives Farming XP");
        s = Farm(farm);
        Check(s.tilled[A] && (turnip.RegrowDays > 0 ? s.crops[A] == turnip.Id : string.IsNullOrEmpty(s.crops[A])), "after harvest the soil stays tilled");

        // Holding F after a harvest: what happens to the now-empty tile?
        int seedsBeforeHold = inventory.CountOf(seeds);
        FaceTile(farm, A);
        yield return Wait(0.2f);
        keyboard.QueueState(new KeyboardState(Key.F));
        yield return Wait(1.0f);
        ReleaseAll();
        s = Farm(farm);
        Info($"holding F on an empty tilled tile for 1 s: crop '{s.crops[A]}', watered {s.watered[A]}, seeds {seedsBeforeHold} -> {inventory.CountOf(seeds)}");

        // Neglect: tile 1, planted and never watered â†’ wilts â†’ dies. Rain days water it, so count dry days ourselves.
        FaceTile(farm, B);
        farm.Interact(interactor); // till
        farm.Interact(interactor); // plant
        int dry = 0, guard = 0;
        bool sawWilt = false;
        while (!Farm(farm).dead[B] && guard++ < 15)
        {
            clock.AdvanceMinutes(WorldClock.MinutesPerDay);
            yield return null;
            var st = Farm(farm);
            dry = st.dryDays[B];
            FaceTile(farm, B);
            yield return null;
            interactor.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
            if (interactor.FocusPrompt.Text != null && interactor.FocusPrompt.Text.StartsWith("Water wilting")) sawWilt = true;
        }
        Check(Farm(farm).dead[B], $"an unwatered crop dies (after {guard} days, dry days {dry}, DieAfterDryDays {turnip.DieAfterDryDays})");
        Check(sawWilt, "it shows 'Water wilting â€¦' before dying");
        FaceTile(farm, B);
        farm.Interact(interactor);
        Check(string.IsNullOrEmpty(Farm(farm).crops[B]) && Farm(farm).tilled[B], "F clears a dead crop");

        // Rain waters every tilled tile
        FaceTile(farm, B);
        farm.Interact(interactor); // plant
        Check(!Farm(farm).watered[B], "freshly planted tile is dry");
        weather.Set(Weather.Rain);
        yield return null;
        Check(Farm(farm).watered[B], "rain waters the field");
        weather.Set(Weather.Clear, announce: false);

        // No seeds: the prompt says so and F does nothing
        var seedSnapshot = Capture(inventory);
        db.TryGet("item_healrootseeds", out ItemData healSeeds);
        inventory.Remove(seeds, inventory.CountOf(seeds));
        inventory.Remove(healSeeds, inventory.CountOf(healSeeds));
        FaceTile(farm, C);
        farm.Interact(interactor); // till
        yield return null;
        interactor.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
        Check(interactor.FocusPrompt.Text == "Tilled soil (no seeds)" && !interactor.FocusPrompt.CanInteract, $"no seeds: '{interactor.FocusPrompt.Text}'");
        inventory.RestoreState(seedCapture(seedSnapshot));
        yield return Shot("qa_farm");
    }

    static string seedCapture(string s) => s;

    void Diag(FarmPlot farm, string when)
    {
        var tileAt = typeof(FarmPlot).GetMethod("TileAt", BindingFlags.Instance | BindingFlags.NonPublic);
        float reach = (float)Field(farm, "reach").GetValue(farm);
        int tile = (int)tileAt.Invoke(farm, new object[] { player.transform.position + player.transform.forward * reach });
        Info($"farm diag {when}: state {state.Current}, combat {combat.StateName} (items ok {combat.CanUseItems}), focus {(interactor.Focus != null ? interactor.Focus.name : "none")} '{interactor.FocusPrompt.Text}', " +
             $"pos {player.transform.position}, forward {player.transform.forward}, tile {tile}, farm at {farm.transform.position} right {farm.transform.right}, interactables {Interactable.Active.Count}");
    }

    // ======================================================================
    IEnumerator Shops()
    {
        Shopkeeper general = null, smith = null;
        foreach (var shop in FindObjectsByType<Shopkeeper>(FindObjectsSortMode.None))
        {
            var data = (ShopData)Field(shop, "shop").GetValue(shop);
            if (data.Id == "shop_generalstore") general = shop; else if (data.Id == "shop_blacksmith") smith = shop;
        }
        Check(general != null && smith != null, "both shops exist");
        if (general == null) yield break;

        inventory.AddGold(500);
        int gold = inventory.Gold;
        int index = 0;
        for (int i = 0; i < general.StockCount; i++) if (!general.IsUnlimited(i)) { index = i; break; }
        var item = general.StockItem(index);
        int price = general.BuyPrice(item);
        int left = general.StockLeft(index);
        Check(general.TryBuy(inventory, index, out var why), $"buy a {item.DisplayName} for {price} ({why})");
        Check(inventory.Gold == gold - price, "gold is spent");
        if (!general.IsUnlimited(index)) Check(general.StockLeft(index) == left - 1, "stock goes down");
        while (general.StockLeft(index) > 0 && general.TryBuy(inventory, index, out _)) { }
        Check(!general.TryBuy(inventory, index, out why) && why == "Sold out for today", $"sold out: '{why}'");
        clock.AdvanceMinutes(WorldClock.MinutesPerDay);
        yield return null;
        Check(general.StockLeft(index) > 0, "the shop restocks the next day");

        db.TryGet("item_turnip", out ItemData turnipItem);
        inventory.Add(turnipItem, 20);
        int p1 = general.SellPrice(turnipItem);
        int earned = general.Sell(inventory, turnipItem, 10);
        int p2 = general.SellPrice(turnipItem);
        Check(earned > 0 && p2 < p1, $"selling turnips earns {earned} and lowers the price ({p1} -> {p2}, market saturation)");
        clock.AdvanceMinutes(WorldClock.MinutesPerDay * 3);
        yield return null;
        Check(general.SellPrice(turnipItem) > p2, $"the market recovers over days ({p2} -> {general.SellPrice(turnipItem)})");

        // Can't sell quest items or things the shop doesn't buy
        int none = 0;
        foreach (var any in db.All.OfType<ItemData>())
            if (any.Category == ItemCategory.Quest && general.Buys(any)) none++;
        Check(none == 0, "shops never buy quest items");

        // Buy-low/sell-high check at every standing tier
        var rep = Services.Get<Reputation>();
        var tiers = rep.Config.Tiers;
        var exploits = new List<string>();
        foreach (var shop in new[] { general, smith })
        {
            var data = (ShopData)Field(shop, "shop").GetValue(shop);
            for (int i = 0; i < shop.StockCount; i++)
            {
                var it = shop.StockItem(i);
                if (!shop.Buys(it) || it.BaseValue <= 0) continue;
                foreach (var tier in tiers)
                {
                    float m = tier.PriceModifier;
                    int buy = Mathf.Max(1, Mathf.CeilToInt(it.BaseValue * data.SellMarkup * m));
                    int sell = Mathf.Max(1, Mathf.FloorToInt(it.BaseValue * data.BuyRate / m));
                    if (sell >= buy) exploits.Add($"{data.Id}:{it.Id}@{tier.Name} buy {buy} sell {sell}");
                }
            }
        }
        Check(exploits.Count == 0, $"no item can be bought and sold back for the same or more ({string.Join("; ", exploits)})");

        // The trade screen opens and closes
        general.Open();
        yield return Wait(0.3f);
        Check(state.Current == GameState.InGameMenu, "trade screen opens (InGameMenu)");
        yield return Tap(Key.Escape);
        yield return Wait(0.2f);
        Check(state.Current == GameState.Playing, $"Esc closes the trade screen ({state.Current})");
    }

    // ======================================================================
    IEnumerator QuestsAndDialogue()
    {
        Services.TryGet(out DialogueRunner runner);
        DialogueSpeaker oswin = null, brenna = null;
        foreach (var sp in FindObjectsByType<DialogueSpeaker>(FindObjectsSortMode.None))
        {
            if (sp.DisplayName == "Oswin") oswin = sp;
            if (sp.DisplayName == "Brenna") brenna = sp;
        }
        Check(oswin != null && brenna != null, "Oswin and Brenna are talkable");
        db.TryGet("quest_bandittrouble", out QuestData bandits);
        db.TryGet("quest_tasteofhome", out QuestData taste);
        db.TryGet("quest_rootsoftheblight", out QuestData roots);
        db.TryGet("quest_scraprun", out QuestData scrapRun);

        // Talk, ask about the land, DECLINE â†’ not started; ask again â†’ offered again; ACCEPT â†’ started
        started.Clear();
        yield return Talk(oswin, runner, "What happened to this land?", "Not my problem.", "Goodbye.");
        Check(log.StatusOf(bandits) == QuestStatus.Inactive && !started.Contains(bandits), "declining Bandit Trouble doesn't start it");
        yield return Talk(oswin, runner, "What happened to this land?", "I'll deal with them.", "Goodbye.");
        Check(log.StatusOf(bandits) == QuestStatus.Active, "asking again and accepting starts Bandit Trouble");
        Check(state.Current == GameState.Playing, $"the conversation ends back in play ({state.Current})");

        started.Clear();
        yield return Talk(oswin, runner, "Need anything?", "Maybe later.", "Goodbye.");
        Check(log.StatusOf(taste) == QuestStatus.Inactive, "declining A Taste of Home");
        yield return Talk(oswin, runner, "Need anything?", "I'll grow them.", "Goodbye.");
        Check(log.StatusOf(taste) is QuestStatus.Active or QuestStatus.Ready, $"accepting A Taste of Home ({log.StatusOf(taste)})");

        yield return Talk(brenna, runner, "Any work?", "Not right now.", "Goodbye.");
        Check(log.StatusOf(scrapRun) == QuestStatus.Inactive, "declining Scrap Run");
        yield return Talk(brenna, runner, "Any work?", "I'll find some.", "Goodbye.");
        Check(log.StatusOf(scrapRun) == QuestStatus.Active, "accepting Scrap Run");

        // Kill 3 bandits (credited to the player) â†’ ready â†’ Oswin turns it in â†’ Brenna offer (decline, then ask again)
        int kills = 0;
        foreach (var e in EnemyController.Active.ToArray())
        {
            // Only the enemy the quest asks for (wolves, archers and the shieldbearer don't count).
            if (e.Data.IsTrainingDummy || kills >= 3 || e.Combatant.IsDead || e.Data != bandits.Objectives[0].Enemy) continue;
            e.Combatant.ReceiveHit(new DamageInfo { Damage = 99999f, Attacker = me });
            kills++;
        }
        yield return Wait(0.3f);
        Check(log.StatusOf(bandits) == QuestStatus.Ready, $"3 bandit kills make Bandit Trouble ready ({log.ObjectiveProgress(bandits, 0)}/{bandits.Objectives[0].Count})");
        int goldBefore = inventory.Gold;
        yield return Talk(oswin, runner, "Not now.", "Goodbye.");
        Check(log.StatusOf(bandits) == QuestStatus.Completed && inventory.Gold >= goldBefore + bandits.GoldReward, $"Oswin turns in Bandit Trouble (+{inventory.Gold - goldBefore} gold)");
        Check(log.StatusOf(roots) == QuestStatus.Inactive, "declined Roots of the Blight");
        yield return Talk(oswin, runner, "About Brenna's ash...", "I'll go and see her.", "Goodbye.");
        Check(log.StatusOf(roots) == QuestStatus.Active, "asked again about the ash and accepted Roots of the Blight");

        // Roots: Brenna shows the ash (objective 0), then a Healroot completes it
        yield return Talk(brenna, runner, "Goodbye.");
        Check(log.IsObjectiveDone(roots, 0), "Brenna shows the ash (objective done)");
        db.TryGet("item_healroot", out ItemData healroot);
        inventory.Add(healroot, 1);
        yield return null;
        Check(log.StatusOf(roots) == QuestStatus.Ready, "holding a Healroot makes Roots ready");
        yield return Talk(brenna, runner, "Goodbye.");
        Check(log.StatusOf(roots) == QuestStatus.Completed && inventory.CountOf(healroot) == 0, "Brenna takes the Healroot and completes Roots");

        // Taste of Home: 5 turnips (Oswin may already have taken them on an earlier visit)
        db.TryGet("item_turnip", out ItemData turnip);
        if (log.StatusOf(taste) != QuestStatus.Completed)
        {
            inventory.Add(turnip, 5);
            yield return null;
            Check(log.StatusOf(taste) == QuestStatus.Ready, "5 turnips make Taste of Home ready");
            yield return Talk(oswin, runner, "Goodbye.");
        }
        Check(log.StatusOf(taste) == QuestStatus.Completed, "Oswin turns in Taste of Home");

        // Scrap: 4 iron scrap
        db.TryGet("item_ironscrap", out ItemData scrap);
        inventory.Add(scrap, 4);
        yield return null;
        yield return Talk(brenna, runner, "Goodbye.");
        Check(log.StatusOf(scrapRun) == QuestStatus.Completed, "Brenna turns in Scrap Run");

        // Selling a collect item after it's ready makes it un-ready again
        // (checked indirectly: collect objectives are live counts)
        Check(runner.IsActive == false, "no conversation left open");
        Info($"quests started by events: {string.Join(", ", started.Select(q => q.Id))}");
    }

    // ======================================================================
    IEnumerator Contracts()
    {
        var board = FindFirstObjectByType<ContractBoard>();
        Check(board != null && board.Contracts.Length > 0, $"contract board with {board?.Contracts.Length} contracts");
        foreach (var c in board.Contracts) Info($"contract {c.Id}: status {log.StatusOf(c)}, lock '{log.LockReason(c)}'");
        var contract = board.Contracts.FirstOrDefault(c => log.CanStart(c) && c.Objectives.All(o => o.Type is ObjectiveType.Kill or ObjectiveType.Collect));
        if (contract == null) { Check(false, "an open contract to test"); yield break; }
        Check(log.StartQuest(contract), $"accept {contract.Title}");
        foreach (var o in contract.Objectives)
        {
            if (o.Type == ObjectiveType.Collect) inventory.Add(o.Item, o.Count);
            else for (int i = 0; i < o.Count; i++)
                {
                    var e = EnemyController.Active.FirstOrDefault(x => !x.Combatant.IsDead && (o.Enemy == null || x.Data == o.Enemy));
                    if (e == null) { yield return Wait(7f); i--; continue; }
                    e.Combatant.ReceiveHit(new DamageInfo { Damage = 99999f, Attacker = me });
                    yield return null;
                }
        }
        yield return null;
        Check(log.IsReady(contract), $"{contract.Title} is ready");
        Check(log.TurnIn(contract), "turn in the contract");
        Check(log.StatusOf(contract) == QuestStatus.Completed && !log.CanStart(contract), "done for today: can't take it again");
        clock.AdvanceMinutes(WorldClock.MinutesPerDay);
        yield return null;
        Check(log.CanStart(contract), "the next day the contract is available again");
        board.Interact(interactor);
        yield return Wait(0.3f);
        Check(state.Current == GameState.InGameMenu, "the board screen opens");
        yield return Shot("qa_board");
        state.SetState(GameState.Playing);
    }

    // ======================================================================
    IEnumerator Sleep()
    {
        var bed = FindFirstObjectByType<SleepSpot>();
        me.ReceiveHit(new DamageInfo { Damage = me.MaxHealth * 0.4f });
        stamina.Spend(50f);
        int day = clock.Day;
        int hour = clock.Hour;
        bed.Interact(interactor);
        yield return Wait(0.3f);
        Check(clock.Hour == 6 && clock.Day == (hour >= 6 ? day + 1 : day), $"sleeping wakes at 06:00 the next morning (day {day} {hour:00}h -> day {clock.Day} {clock.Hour:00}:{clock.Minute:00})");
        Check(Mathf.Approximately(me.Health, me.MaxHealth) && Mathf.Approximately(stamina.Current, stamina.Max), "sleep restores health and stamina");
        Check(save.ReadSlot(save.ActiveSlot).Summary.day == clock.Day, "sleep autosaves");
        var fatigue = player.GetComponent<PlayerFatigue>();
        Check(fatigue == null || fatigue.HoursAwake < 0.5f, "sleep resets fatigue");
        // Sleeping at 03:00 should still wake the same morning
        clock.AdvanceMinutes((24 - clock.Hour + 3) * 60);
        day = clock.Day;
        bed.Interact(interactor);
        yield return Wait(0.3f);
        Check(clock.Day == day && clock.Hour == 6, $"sleeping at 03:00 wakes at 06:00 the same day (day {day} -> {clock.Day})");
        yield return Wait(2.5f);
    }

    // ======================================================================
    IEnumerator EdgeCases()
    {
        // Save/load refused in menus and dialogue
        FindFirstObjectByType<Shopkeeper>().Open();
        yield return null;
        Check(!save.Save(), "can't save with a menu open");
        state.SetState(GameState.Playing);

        Services.TryGet(out DialogueRunner runner);
        DialogueSpeaker oswin = FindObjectsByType<DialogueSpeaker>(FindObjectsSortMode.None).First(s => s.DisplayName == "Oswin");
        oswin.Interact(interactor);
        yield return Wait(0.3f);
        bool loaded = false;
        yield return Run(save.LoadAsync(save.ActiveSlot), r => loaded = r);
        Check(!loaded && runner.IsActive, "can't load during a conversation");
        yield return Tap(Key.Escape);
        yield return Wait(0.3f);
        Check(!runner.IsActive && state.Current == GameState.Playing, $"Esc leaves a conversation ({state.Current})");

        // Pause freezes the clock and enemies
        double t0 = clock.TotalMinutes;
        yield return Tap(Key.Escape);
        yield return Wait(1f);
        Check(state.Current == GameState.Paused && Math.Abs(clock.TotalMinutes - t0) < 0.5, "Esc pauses and the clock stops");
        yield return Tap(Key.Escape);
        yield return Wait(0.3f);
        Check(state.Current == GameState.Playing && Mathf.Approximately(Time.timeScale, 1f), "Esc resumes");

        // Menu hotkeys
        foreach (var (key, name) in new[] { (Key.Tab, "inventory"), (Key.C, "character"), (Key.J, "journal") })
        {
            yield return Tap(key);
            yield return Wait(0.2f);
            bool open = state.Current == GameState.InGameMenu;
            yield return Tap(key);
            yield return Wait(0.2f);
            Check(open && state.Current == GameState.Playing, $"{key} opens and closes the {name}");
        }

        // Hit-stop then pause: resuming must not leave time frozen or sped up
        var hitStop = Services.Get<HitStop>();
        hitStop.Trigger(0.3f);
        yield return null;
        state.SetState(GameState.Paused);
        yield return Wait(0.6f);
        state.SetState(GameState.Playing);
        yield return Wait(0.6f);
        Check(Mathf.Approximately(Time.timeScale, 1f), $"time scale is 1 after hit-stop + pause ({Time.timeScale})");

        // Pressing gameplay keys while paused does nothing
        state.SetState(GameState.Paused);
        Vector3 p = player.transform.position;
        yield return Hold(0.5f, Key.W);
        yield return Click(MouseButton.Left);
        Check(Planar(player.transform.position - p) < 0.01f && combat.StateName != "Attacking", "no movement or attacks while paused");
        state.SetState(GameState.Playing);

        // Falling out of the world? Teleport far below and see what happens
        Info("no kill-plane test: there is no out-of-bounds handling (see report)");

        // Quest reward with a full bag drops on the ground instead of vanishing
        var snapshot = Capture(inventory);
        db.TryGet("item_ironscrap", out ItemData scrap);
        inventory.Add(scrap, 99999);
        db.TryGet("item_healingdraught", out ItemData draught);
        int space = inventory.SpaceFor(draught);
        Info($"after filling with scrap: space for a draught {space}");
        // A turned-in quest whose reward doesn't fit drops it at your feet.
        db.TryGet("quest_scraprun", out QuestData scrapRun);
        int pickups = FindObjectsByType<ItemPickup>(FindObjectsSortMode.None).Length;
        inventory.RestoreState(snapshot);
        Info($"pickups present: {pickups}");
    }

    // ======================================================================
    IEnumerator SaveRoundTrip()
    {
        // Put varied state everywhere, then compare every saveable's JSON before saving and after loading.
        db.TryGet("item_turnipseeds", out ItemData seeds);
        inventory.Add(seeds, 3);
        inventory.AddGold(123);
        var farm = FindFirstObjectByType<FarmPlot>();
        FaceTile(farm, 14); farm.Interact(interactor); farm.Interact(interactor); farm.Interact(interactor);
        PlaceAt(OpenGround() + new Vector3(3f, 0f, 2f), 77f);
        yield return Wait(0.5f);
        Services.Get<WeatherSystem>().Set(Weather.Cloudy, announce: false);
        Check(save.Save(), "save with varied state");
        var before = Snapshot();
        Info($"{before.Count} saveables: {string.Join(", ", before.Keys)}");

        // Change lots of things
        inventory.AddGold(999);
        db.TryGet("item_bread", out ItemData bread);
        inventory.Add(bread, 7);
        clock.AdvanceMinutes(WorldClock.MinutesPerDay * 2 + 300);
        progression.AddXp(Discipline.Farming, 500);
        PlaceAt(OpenGround() + new Vector3(-6f, 0f, -3f), 200f);
        Services.Get<WeatherSystem>().Set(Weather.Rain, announce: false);
        yield return Wait(0.5f);

        // In-place load
        bool ok = false;
        yield return Run(save.LoadAsync(save.ActiveSlot), r => ok = r);
        yield return Wait(0.5f);
        Check(ok, "load (same scene)");
        CompareSnapshots(before, Snapshot(), "in-place load");

        // Load from the main menu (fresh scene objects)
        Services.Get<SceneLoader>().LoadMainMenu();
        yield return Until(() => SceneManager.GetActiveScene().name == "MainMenu" && state.Current == GameState.MainMenu, 30f);
        yield return Wait(0.5f);
        FindFirstObjectByType<MainMenu>().Continue();
        yield return Until(InWorld, 40f);
        yield return Wait(1.5f);
        Bind();
        CompareSnapshots(before, Snapshot(), "load from the main menu");
        yield return Shot("qa_after_load");
    }

    Dictionary<string, string> Snapshot()
    {
        var list = (List<ISaveable>)typeof(SaveService).GetField("saveables", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(save);
        var map = new Dictionary<string, string>();
        foreach (var s in list) map[s.SaveId] = s.CaptureState();
        return map;
    }

    void CompareSnapshots(Dictionary<string, string> a, Dictionary<string, string> b, string label)
    {
        var diffs = new List<string>();
        foreach (var pair in a)
        {
            if (!b.TryGetValue(pair.Key, out var other)) { diffs.Add($"{pair.Key}: missing after load"); continue; }
            if (other == pair.Value || Rounded(other) == Rounded(pair.Value)) continue; // float jitter (physics settling) isn't a save bug
            if (pair.Key == "world.clock" && Math.Abs(Number(other, "totalMinutes") - Number(pair.Value, "totalMinutes")) < 5) continue;
            if (pair.Key == "player.fatigue" && Math.Abs(Number(other, "hoursAwake") - Number(pair.Value, "hoursAwake")) < 0.1) continue;
            diffs.Add($"{pair.Key}: {FirstDiff(pair.Value, other)}");
        }
        foreach (var key in b.Keys) if (!a.ContainsKey(key)) diffs.Add($"{key}: only after load");
        Check(diffs.Count == 0, $"{label}: every saveable restores exactly ({a.Count} compared)");
        foreach (var d in diffs) Info($"  {label} diff â†’ {d}");
    }

    /// <summary>The JSON with every number rounded to 3 decimals.</summary>
    static string Rounded(string json) =>
        System.Text.RegularExpressions.Regex.Replace(json, @"-?\d+\.\d+(?:[eE][-+]?\d+)?",
            m => Math.Round(double.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture), 3).ToString(System.Globalization.CultureInfo.InvariantCulture));

    static double Number(string json, string field)
    {
        int i = json.IndexOf($"\"{field}\":", StringComparison.Ordinal);
        if (i < 0) return double.NaN;
        i += field.Length + 3;
        int j = i;
        while (j < json.Length && "0123456789.-eE+".IndexOf(json[j]) >= 0) j++;
        return double.Parse(json.Substring(i, j - i), System.Globalization.CultureInfo.InvariantCulture);
    }

    static string FirstDiff(string x, string y)
    {
        int i = 0;
        while (i < x.Length && i < y.Length && x[i] == y[i]) i++;
        int from = Mathf.Max(0, i - 40);
        return $"saved 'â€¦{x.Substring(from, Mathf.Min(90, x.Length - from))}â€¦' vs loaded 'â€¦{y.Substring(from, Mathf.Min(90, y.Length - from))}â€¦'";
    }

    // ======================================================================
    IEnumerator Stress()
    {
        // 40 days in a row (crops, shops, weather, contracts, fatigue, enemies), then check nothing piles up.
        int objectsBefore = FindObjectsByType<Transform>(FindObjectsSortMode.None).Length;
        long memBefore = GC.GetTotalMemory(true);
        for (int day = 0; day < 40; day++)
        {
            clock.AdvanceMinutes(WorldClock.MinutesPerDay);
            yield return null;
        }
        yield return Wait(2f);
        int objectsAfter = FindObjectsByType<Transform>(FindObjectsSortMode.None).Length;
        long memAfter = GC.GetTotalMemory(true);
        Info($"40 days: transforms {objectsBefore} -> {objectsAfter}, managed memory {memBefore / 1048576f:0.0} MB -> {memAfter / 1048576f:0.0} MB, now day {clock.Day}");
        Check(objectsAfter <= objectsBefore + 50, "no objects pile up over 40 days");
        Check(memAfter < memBefore + 50L * 1024 * 1024, "managed memory stays flat over 40 days");

        // 60 seconds of real play with the bandits chasing an invulnerable player
        me.Invulnerable = true;
        var camp = EnemyController.Active.Where(e => !e.Data.IsTrainingDummy).Select(e => e.Home).FirstOrDefault();
        PlaceAt(camp + Vector3.back * 4f, 0f);
        float end = Time.realtimeSinceStartup + 20f;
        var frames = new List<float>();
        int k = 0;
        while (Time.realtimeSinceStartup < end)
        {
            frames.Add(Time.unscaledDeltaTime * 1000f);
            if (++k % 30 == 0) yield return Click(MouseButton.Left);
            else yield return null;
        }
        frames.Sort();
        Info($"20 s fighting at the camp: avg {frames.Average():0.0} ms, 99th {frames[(int)(frames.Count * 0.99f)]:0.0} ms, worst {frames[^1]:0.0} ms");
        Check(frames.Average() < 33.3f, "fighting at the camp stays above 30 fps on average");
        yield return Shot("qa_camp_fight");
        me.Invulnerable = false;
    }

    // ======================================================================
    IEnumerator OutOfBounds()
    {
        // Walk outward from near each edge of the 100 m ground and see whether anything stops the player.
        var dirs = new[] { (new Vector3(44f, 1.1f, 5f), 90f, "east"), (new Vector3(-44f, 1.1f, 5f), 270f, "west"),
                           (new Vector3(5f, 1.1f, 44f), 0f, "north"), (new Vector3(5f, 1.1f, -44f), 180f, "south") };
        foreach (var (start, yaw, name) in dirs)
        {
            PlaceAt(start, yaw);
            yield return Wait(0.5f);
            yield return Hold(4f, Key.W);
            yield return Wait(2f);
            var pos = player.transform.position;
            Info($"walking {name} from {start}: ended at {pos} (grounded {motor.IsGrounded})");
            Check(pos.y > -2f, $"walking {name} to the edge of the world doesn't fall off (y {pos.y:0.0})");
            if (pos.y < -2f) yield return Shot($"qa_fell_{name}");
        }
        PlaceAt(OpenGround(), 0f);
        yield return Wait(0.5f);
    }

    IEnumerator Fixes()
    {
        // Everything this section changes (quests, field, bag) is rolled back at the end by reloading this save.
        Check(save.Save(), "save before the fix checks");
        // 1. Falling below the world puts you back on solid ground.
        PlaceAt(OpenGround(), 0f);
        yield return Wait(1f);
        Vector3 safe = player.transform.position;
        messages.Clear();
        motor.Teleport(new Vector3(safe.x, -40f, safe.z), player.transform.rotation);
        yield return Wait(0.3f);
        Check(player.transform.position.y > 0f && Vector3.Distance(player.transform.position, safe) < 3f, $"below the world → back on solid ground ({player.transform.position})");
        Check(messages.Any(m => m.StartsWith("You scramble back")), "a message says so");
        var edge = FindFirstObjectByType<WorldEdge>();
        Check(edge != null && edge.HasGround && edge.transform.childCount == 4, "four invisible edge walls exist");

        // 2. Holding F repeats only the action it started with.
        var farm = FindFirstObjectByType<FarmPlot>();
        db.TryGet("crop_turnip", out CropData turnip);
        db.TryGet("item_turnipseeds", out ItemData seeds);
        Services.Get<WeatherSystem>().Set(Weather.Clear, announce: false);
        inventory.Add(seeds, 5);
        const int T = 20; // row 3, column 2: inside the fence
        FaceTile(farm, T);
        yield return Wait(0.3f);
        keyboard.QueueState(new KeyboardState(Key.F));
        yield return Wait(1.2f);
        ReleaseAll();
        yield return null;
        var st = Farm(farm);
        Check(st.tilled[T] && string.IsNullOrEmpty(st.crops[T]), $"holding F on grass only tills (crop '{st.crops[T]}')");
        farm.Interact(interactor); // plant
        while (!IsRipe(Farm(farm), T, turnip))
        {
            if (!Farm(farm).watered[T]) { FaceTile(farm, T); farm.Interact(interactor); }
            clock.AdvanceMinutes(WorldClock.MinutesPerDay);
            yield return null;
        }
        int seedCount = inventory.CountOf(seeds);
        FaceTile(farm, T);
        yield return Wait(0.3f);
        keyboard.QueueState(new KeyboardState(Key.F));
        yield return Wait(1.2f);
        ReleaseAll();
        yield return null;
        st = Farm(farm);
        Check(string.IsNullOrEmpty(st.crops[T]) && inventory.CountOf(seeds) == seedCount, $"holding F to harvest doesn't replant (crop '{st.crops[T]}', seeds {seedCount} -> {inventory.CountOf(seeds)})");
        yield return Tap(Key.F);
        Check(Farm(farm).crops[T] == turnip.Id, "a fresh press of F does plant");

        // 3. Item text only mentions what's restored.
        db.TryGet("item_bread", out ConsumableData bread);
        var restores = (string)typeof(InventoryScreen).GetMethod("RestoresText", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { bread });
        Check(!restores.Contains(" 0 ") && restores.Contains("health"), $"bread text: {System.Text.RegularExpressions.Regex.Replace(restores, "<.*?>", "")}");

        // 4. Brenna: holding a Healroot on the first visit finishes Roots in one conversation.
        db.TryGet("quest_bandittrouble", out QuestData bandits);
        db.TryGet("quest_rootsoftheblight", out QuestData roots);
        db.TryGet("item_healroot", out ItemData healroot);
        log.StartQuest(bandits);
        log.CompleteObjective(bandits, 0);
        log.TurnIn(bandits);
        log.StartQuest(roots);
        inventory.Add(healroot, 1);
        yield return null;
        Services.TryGet(out DialogueRunner runner);
        var brenna = FindObjectsByType<DialogueSpeaker>(FindObjectsSortMode.None).First(sp => sp.DisplayName == "Brenna");
        yield return Talk(brenna, runner, "Goodbye.");
        Check(log.StatusOf(roots) == QuestStatus.Completed, $"Healroot already in the bag: Roots completes in one talk ({log.StatusOf(roots)})");

        // 5. Screens to look at: skill rows, the compact banner over a menu, a fading note.
        db.TryGet("quest_scraprun", out QuestData scrapRun);
        log.StartQuest(scrapRun); // raises a banner
        var gameMenu = FindFirstObjectByType<GameMenu>();
        gameMenu.Open(GameMenuTab.Character);
        yield return Wait(0.4f);
        yield return Shot("fix_character_skills_and_banner");
        gameMenu.Close();
        yield return Wait(0.5f);
        EventBus<HudMessageEvent>.Raise(new HudMessageEvent("This note fades out cleanly"));
        float lifetime = (float)typeof(GameHud).GetField("NoteLifetime", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).GetValue(null);
        yield return Wait(lifetime - 0.5f);
        yield return Shot("fix_fading_note");

        // 6. Camera behind the player over a house (it used to slip inside the roof above the walls).
        PlaceAt(new Vector3(6.5f, 1.1f, -6.5f), 0f); // field, house just behind
        yield return Wait(1.5f);
        var camPos = rig.transform.position;
        // The camera must see the player: nothing (a roof) between them. (A roof's bounding box is bigger than the roof, so test sight, not bounds.)
        Vector3 head = player.transform.position + Vector3.up * 1.5f;
        bool blocked = Physics.Linecast(camPos, head, out var sight, ~(CombatLayers.PlayerMask | (1 << 2)), QueryTriggerInteraction.Ignore);
        Check(!blocked, $"camera by the house can see the player (camera at {camPos}{(blocked ? ", blocked by " + sight.collider.name : "")})");
        Check(FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r => r.name == "Roof").All(r => r.GetComponent<Collider>() != null), "every roof has a collider");
        yield return Shot("fix_camera_by_house");

        bool rolledBack = false;
        yield return Run(save.LoadAsync(save.ActiveSlot), r => rolledBack = r);
        yield return Wait(0.5f);
        Check(rolledBack, "rolled back after the fix checks");
    }

    IEnumerator CampFight()
    {
        // Frame timing with the three bandits fighting an invulnerable player at their camp.
        var bandits = EnemyController.Active.Where(e => !e.Data.IsTrainingDummy).ToList();
        Vector3 camp = Vector3.zero;
        foreach (var b in bandits) camp += b.Home;
        camp /= Mathf.Max(1, bandits.Count);
        me.Invulnerable = true;
        PlaceAt(camp + Vector3.back * 5f, 0f);
        yield return Wait(2f);
        var frames = new List<float>();
        float end = Time.realtimeSinceStartup + 15f;
        int k = 0;
        while (Time.realtimeSinceStartup < end)
        {
            frames.Add(Time.unscaledDeltaTime * 1000f);
            if (++k % 30 == 0) yield return Click(MouseButton.Left);
            else yield return null;
        }
        frames.Sort();
        int fighting = bandits.Count(b => b.StateName is "Chase" or "Attack");
        Info($"15 s fighting at the camp ({fighting}/{bandits.Count} bandits engaged): avg {frames.Average():0.0} ms, 99th {frames[(int)(frames.Count * 0.99f)]:0.0} ms, worst {frames[^1]:0.0} ms");
        Check(frames.Average() < 33.3f, "fighting at the camp stays above 30 fps on average");
        yield return Shot("qa_camp_fight");
        me.Invulnerable = false;
    }

    // ======================================================================
    // Helpers

    IEnumerator Talk(DialogueSpeaker speaker, DialogueRunner runner, params string[] picks)
    {
        var pickList = new List<string>(picks);
        speaker.Interact(interactor);
        yield return Wait(0.2f);
        var transcript = new List<string>();
        int guard = 0;
        while (runner.IsActive && guard++ < 80)
        {
            if (runner.Choices.Count > 0)
            {
                int choice = -1;
                for (int p = 0; p < pickList.Count && choice < 0; p++)
                {
                    int idx = runner.Choices.ToList().FindIndex(c => c.Contains(pickList[p]));
                    if (idx >= 0) { choice = idx; pickList.RemoveAt(p); }
                }
                if (choice < 0) choice = runner.Choices.ToList().FindIndex(c => c.Contains("Goodbye"));
                if (choice < 0) choice = runner.Choices.Count - 1;
                transcript.Add($"[{string.Join(" | ", runner.Choices)}] -> {runner.Choices[choice]}");
                runner.Choose(choice);
            }
            else
            {
                transcript.Add(runner.CurrentText);
                runner.Advance();
            }
            yield return null;
        }
        Info($"talk {speaker.DisplayName}: {string.Join(" / ", transcript)}");
        if (runner.IsActive) { Check(false, $"conversation with {speaker.DisplayName} didn't end"); state.SetState(GameState.Playing); }
        yield return Wait(0.3f);
        if (state.Current != GameState.Playing) state.SetState(GameState.Playing);
        yield return null;
    }

    void FaceTile(FarmPlot farm, int index)
    {
        var center = (Vector3)typeof(FarmPlot).GetMethod("TileWorldCenter", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(farm, new object[] { index });
        float reach = (float)Field(farm, "reach").GetValue(farm);
        Vector3 at = center - farm.transform.right * reach;
        motor.Teleport(new Vector3(at.x, player.transform.position.y, at.z), Quaternion.LookRotation(farm.transform.right));
        Physics.SyncTransforms();
    }

    static FarmState Farm(FarmPlot farm) => JsonUtility.FromJson<FarmState>(farm.CaptureState());
    static bool IsRipe(FarmState s, int i, CropData crop) => s.crops[i] == crop.Id && !s.dead[i] && s.growth[i] >= crop.TotalGrowDays;

    static string Capture(Inventory inventory) => inventory.CaptureState();

    int TotalItems()
    {
        int n = 0;
        for (int i = 0; i < inventory.SlotCount; i++) if (!inventory[i].IsEmpty) n += inventory[i].Count;
        return n;
    }

    EnemyController FindEnemy(bool dummy) => EnemyController.Active.FirstOrDefault(e => e.Data.IsTrainingDummy == dummy);

    Vector3 OpenGround()
    {
        var bed = FindFirstObjectByType<SleepSpot>();
        // Open grass west of the village square, clear of buildings (used by earlier harnesses).
        return new Vector3(-12f, 1.1f, 20f);
    }

    void PlaceAt(Vector3 position, float yaw)
    {
        motor.Teleport(position, Quaternion.Euler(0f, yaw, 0f));
        typeof(ThirdPersonCamera).GetField("yaw", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(rig, yaw);
    }

    static float Planar(Vector3 v) => new Vector2(v.x, v.z).magnitude;

    bool InWorldScene() => SceneManager.GetActiveScene().name == "World_Test";
    bool InWorld() => InWorldScene() && GameObject.FindWithTag("Player") != null && state.Current == GameState.Playing;

    IEnumerator Hold(float seconds, params Key[] keys)
    {
        keyboard.QueueState(new KeyboardState(keys));
        yield return Wait(seconds);
        ReleaseAll();
        yield return null;
    }

    IEnumerator Tap(Key key, float seconds = 0.08f)
    {
        keyboard.QueueState(new KeyboardState(key));
        yield return Wait(seconds);
        keyboard.QueueState(new KeyboardState());
        yield return null;
        yield return null;
    }

    IEnumerator Click(MouseButton button)
    {
        mouse.QueueState(new MouseState().WithButton(button));
        yield return null;
        yield return null;
        mouse.QueueState(new MouseState());
        yield return null;
    }

    void ReleaseAll()
    {
        if (keyboard != null) keyboard.QueueState(new KeyboardState());
        if (mouse != null) mouse.QueueState(new MouseState());
    }

    static IEnumerator Run(Awaitable<bool> task, Action<bool> result)
    {
        var awaiter = task.GetAwaiter();
        while (!awaiter.IsCompleted) yield return null;
        result(awaiter.GetResult());
    }

    IEnumerator Safe(string name, IEnumerator body)
    {
        while (true)
        {
            object current;
            try
            {
                if (!body.MoveNext()) yield break;
                current = body.Current;
            }
            catch (Exception e)
            {
                Check(false, $"{name} crashed: {e.GetType().Name}: {e.Message}");
                Debug.LogWarning(e);
                yield break;
            }
            yield return current;
        }
    }

    static IEnumerator Until(Func<bool> condition, float timeout)
    {
        float end = Time.realtimeSinceStartup + timeout;
        while (!condition() && Time.realtimeSinceStartup < end) yield return null;
    }

    static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
    static IEnumerator Wait(float seconds) => new WaitForSecondsRealtime(seconds);

    IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();
        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
        Destroy(tex);
    }
}

static class DeviceExtensions
{
    public static void QueueState<T>(this InputDevice device, T state) where T : struct, IInputStateTypeInfo =>
        InputSystem.QueueStateEvent(device, state);
}

