using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Beast.Core;
using Beast.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Test-only: exercises light reputation end to end and screenshots its UI. Runs only with -repharness.</summary>
public sealed class RepHarness : MonoBehaviour
{
    string dir;
    GameStateService state;
    int failures;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-repharness") < 0) return;
        var go = new GameObject("RepHarness");
        DontDestroyOnLoad(go);
        go.AddComponent<RepHarness>();
    }

    void Check(bool ok, string what)
    {
        if (!ok) failures++;
        Debug.Log($"[RepTest] {(ok ? "PASS" : "FAIL")} {what}");
    }

    IEnumerator Start()
    {
        var args = Environment.GetCommandLineArgs();
        dir = args[Array.IndexOf(args, "-shots") + 1];
        Directory.CreateDirectory(dir);
        while (SceneManager.GetActiveScene().name != "World_Test" || GameObject.FindWithTag("Player") == null) yield return null;
        yield return new WaitForSecondsRealtime(3f);

        state = Services.Get<GameStateService>();
        var player = GameObject.FindWithTag("Player");
        var inventory = player.GetComponent<Inventory>();
        var log = player.GetComponent<QuestLog>();
        var me = player.GetComponent<Combatant>();
        var interactor = player.GetComponent<PlayerInteractor>();
        var db = Services.Get<GameDatabase>();
        var clock = Services.Get<WorldClock>();
        var save = Services.Get<SaveService>();
        var rep = player.GetComponent<Reputation>();

        Check(rep != null && rep.IsConfigured, "player has a configured Reputation");
        Check(Services.TryGet(out Reputation registered) && registered == rep, "Reputation registered as a service");
        Check(rep.Standing == 0 && rep.TierName == "Stranger", $"starts at 0 Stranger (got {rep.Standing} {rep.TierName})");

        Shopkeeper merchant = null;
        foreach (var shop in FindObjectsByType<Shopkeeper>(FindObjectsSortMode.None))
            if (shop.name == "Merchant") merchant = shop;
        var stockItem = merchant.StockItem(0);
        db.TryGet("item_turnip", out ItemData turnip);
        int buy0 = merchant.BuyPrice(stockItem), sell0 = merchant.SellPrice(turnip);
        Check(buy0 == Mathf.Max(1, Mathf.CeilToInt(stockItem.BaseValue * merchant.Shop.SellMarkup)), $"neutral buy price {stockItem.DisplayName} = {buy0}");

        db.TryGet("contract_townpatrol", out QuestData patrol);
        Check(patrol != null && patrol.RequiredTier == 2, "Town Patrol exists and needs Trusted");
        Check(!log.CanStart(patrol) && log.LockReason(patrol) == "Requires Trusted standing", $"patrol locked: '{log.LockReason(patrol)}'");
        var board = FindFirstObjectByType<ContractBoard>();
        Check(board.Posts(patrol), "patrol posted on the board");

        board.Interact(interactor);
        yield return Wait(0.4f);
        yield return Shot("r1_board_locked");
        state.SetState(GameState.Playing);

        // Quest turn-in: +25 (A Taste of Home).
        db.TryGet("quest_tasteofhome", out QuestData taste);
        log.StartQuest(taste);
        inventory.Add(turnip, 5);
        Check(log.TurnIn(taste), "turned in A Taste of Home");
        Check(rep.Standing == 25, $"quest gives +25 (standing {rep.Standing})");

        // Kills: +2 each, capped at 20 per day.
        EnemyController bandit = null;
        foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            if (enemy.Data != null && enemy.Data.StandingReward > 0) bandit = enemy;
        var banditCombatant = bandit.GetComponent<Combatant>();
        for (int i = 0; i < 15; i++)
            EventBus<DamageDealtEvent>.Raise(new DamageDealtEvent(banditCombatant, me, 10f, HitResult.Killed));
        Check(rep.Standing == 45, $"kills capped at +20/day (standing {rep.Standing})");
        EventBus<DamageDealtEvent>.Raise(new DamageDealtEvent(me, banditCombatant, 10f, HitResult.Killed));
        Check(rep.Standing == 45, "the player dying gives nothing");

        // Trade: 1 per 25 gold, capped at 15 per day.
        EventBus<ItemTradedEvent>.Raise(new ItemTradedEvent(merchant.Shop, turnip, 1, 24, false));
        Check(rep.Standing == 45, "24 gold of trade banks without a point");
        EventBus<ItemTradedEvent>.Raise(new ItemTradedEvent(merchant.Shop, turnip, 1, 1, false));
        Check(rep.Standing == 46, $"25th gold makes a point (standing {rep.Standing})");
        EventBus<ItemTradedEvent>.Raise(new ItemTradedEvent(merchant.Shop, turnip, 1, 5000, false));
        Check(rep.Standing == 60, $"trade capped at +15/day (standing {rep.Standing})");

        // A new day resets the caps.
        clock.AdvanceMinutes(WorldClock.MinutesPerDay);
        EventBus<DamageDealtEvent>.Raise(new DamageDealtEvent(banditCombatant, me, 10f, HitResult.Killed));
        Check(rep.Standing == 62 && rep.TierName == "Known", $"caps reset next day; Known at 62 (standing {rep.Standing} {rep.TierName})");
        int expectedBuy = Mathf.Max(1, Mathf.CeilToInt(stockItem.BaseValue * merchant.Shop.SellMarkup * 0.95f));
        Check(merchant.BuyPrice(stockItem) == expectedBuy, $"Known buy price {merchant.BuyPrice(stockItem)} (was {buy0}, expected {expectedBuy})");
        Check(merchant.SellPrice(turnip) >= sell0, $"Known sell price {merchant.SellPrice(turnip)} (was {sell0})");
        yield return Wait(0.3f);
        yield return Shot("r2_tier_banner");

        // Abandoning town work costs standing.
        db.TryGet("contract_supplyrun", out QuestData supply);
        log.StartQuest(supply);
        Check(rep.AbandonPenaltyFor(supply) == 5, "abandon penalty is 5");
        var journal = FindFirstObjectByType<QuestJournal>();
        state.SetState(GameState.InGameMenu);
        SetField(journal, "isOpen", true);
        SetField(journal, "selected", supply);
        SetField(journal, "confirmAbandon", true);
        yield return Wait(0.4f);
        yield return Shot("r3_journal_abandon_confirm");
        state.SetState(GameState.Playing);
        log.Abandon(supply);
        Check(rep.Standing == 57, $"abandon -5 (standing {rep.Standing})");

        // Reach Trusted: the patrol unlocks and prices get 10% better.
        rep.Change(100, StandingSource.Debug);
        Check(rep.TierName == "Trusted" && log.CanStart(patrol), $"Trusted at {rep.Standing}; patrol unlocked");
        yield return Wait(4f); // let the queued banners play
        board.Interact(interactor);
        yield return Wait(0.4f);
        yield return Shot("r4_board_unlocked");
        state.SetState(GameState.Playing);

        merchant.Open();
        yield return Wait(0.4f);
        yield return Shot("r5_shop_standing");
        state.SetState(GameState.Playing);

        state.SetState(GameState.InGameMenu);
        SetField(journal, "isOpen", true);
        SetField(journal, "confirmAbandon", false);
        yield return Wait(0.4f);
        yield return Shot("r6_journal_standing");
        state.SetState(GameState.Playing);

        var character = FindFirstObjectByType<CharacterScreen>();
        state.SetState(GameState.InGameMenu);
        SetField(character, "isOpen", true);
        yield return Wait(0.4f);
        yield return Shot("r7_character");
        state.SetState(GameState.Playing);

        // Dialogue: Oswin's Trusted thanks (gifts), then the standing talk.
        db.TryGet("item_bread", out ItemData bread);
        int bread0 = inventory.CountOf(bread);
        DialogueSpeaker oswin = null;
        foreach (var speaker in FindObjectsByType<DialogueSpeaker>(FindObjectsSortMode.None))
            if (speaker.DisplayName == "Oswin") oswin = speaker;
        oswin.Interact(interactor);
        Services.TryGet(out DialogueRunner runner);
        for (int i = 0; i < 20 && runner.IsActive && runner.Choices.Count == 0; i++) { runner.Advance(); yield return Wait(0.05f); }
        Check(inventory.CountOf(bread) == bread0 + 2, $"Oswin's Trusted gift: bread {bread0} -> {inventory.CountOf(bread)}");
        int talk = -1;
        for (int i = 0; i < runner.Choices.Count; i++) if (runner.Choices[i] == "How does the town see me?") talk = i;
        Check(talk >= 0, "standing choice offered");
        if (talk >= 0) runner.Choose(talk);
        yield return Wait(1.5f);
        yield return Shot("r8_dialogue_standing");
        Check(runner.CurrentText.Contains("trusted"), $"Oswin's Trusted line: '{runner.CurrentText}'");
        state.SetState(GameState.Playing);
        yield return Wait(0.3f);

        // Save / load round trip.
        int saved = rep.Standing;
        Check(save.Save(1), "saved slot 1");
        rep.Change(200, StandingSource.Debug);
        save.Load(1);
        yield return Wait(1f);
        Check(rep.Standing == saved, $"load restores standing {saved} (got {rep.Standing})");

        // Dropping below a threshold drops the tier.
        rep.Change(-500, StandingSource.Debug);
        Check(rep.Standing == 0 && rep.TierName == "Stranger" && !log.CanStart(patrol), "standing floors at 0 and the patrol locks again");
        yield return Wait(0.5f);
        yield return Shot("r9_standing_lowered");

        Debug.Log($"[RepTest] done, failures={failures}");
        Application.Quit();
    }

    static void SetField(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    static IEnumerator Wait(float seconds) => new WaitForSecondsRealtime(seconds);

    IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();
        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
        Destroy(tex);
    }
}
