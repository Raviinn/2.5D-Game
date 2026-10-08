using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Beast.Core;
using Beast.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Test-only: seasons, seasonal crops, festivals, snow and the seasonal look (Milestone 46). Runs with -seasonharness.</summary>
public sealed class SeasonHarness : MonoBehaviour
{
    string dir;
    int failures;
    WorldClock clock;
    PlayerMotor motor;
    PlayerInteractor interactor;
    FarmPlot plot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-seasonharness") < 0) return;
        var go = new GameObject("SeasonHarness");
        DontDestroyOnLoad(go);
        go.AddComponent<SeasonHarness>();
    }

    void Check(bool ok, string what)
    {
        if (!ok) failures++;
        Debug.Log($"[SeasonTest] {(ok ? "PASS" : "FAIL")} {what}");
    }

    IEnumerator Start()
    {
        var args = Environment.GetCommandLineArgs();
        dir = args[Array.IndexOf(args, "-shots") + 1];
        Directory.CreateDirectory(dir);
        while (SceneManager.GetActiveScene().name != "World_Test" || GameObject.FindWithTag("Player") == null) yield return null;
        yield return Wait(3f);

        var player = GameObject.FindWithTag("Player");
        motor = player.GetComponent<PlayerMotor>();
        interactor = player.GetComponent<PlayerInteractor>();
        var inventory = player.GetComponent<Inventory>();
        clock = Services.Get<WorldClock>();
        var database = Services.Get<GameDatabase>();
        plot = FindFirstObjectByType<FarmPlot>();
        var keeper = FindFirstObjectByType<SeasonKeeper>();
        player.GetComponent<Combatant>().Invulnerable = true;
        foreach (var e in EnemyController.Active.ToList()) e.enabled = false; // a quiet world for a year of skipping

        // ---- The calendar ----
        Check(Calendar.SeasonOf(1) == Season.Spring && Calendar.DayOfSeason(1) == 1, "day 1 is Spring 1");
        Check(Calendar.SeasonOf(15) == Season.Summer && Calendar.SeasonOf(29) == Season.Autumn && Calendar.SeasonOf(43) == Season.Winter, "seasons last 14 days");
        Check(Calendar.SeasonOf(57) == Season.Spring && Calendar.YearOf(57) == 2, "day 57 is Spring of year 2");
        Check(Calendar.FestivalOn(8) == Festival.PlantingFestival && Calendar.FestivalOn(42) == Festival.HarvestFair && Calendar.FestivalOn(9) == Festival.None,
            "Spring 8 is the Planting Festival, Autumn 14 the Harvest Fair");
        Check(keeper != null && keeper.Shown == Season.Spring, "the world starts dressed for spring");

        // Spring view.
        var viewpoint = new Vector3(-6f, 1.2f, -14f);
        motor.Teleport(viewpoint, Quaternion.LookRotation(new Vector3(1f, 0f, 1f)));
        yield return Wait(1f);
        yield return Shot("s1_spring");
        var ground = GameObject.Find("Ground")?.GetComponent<Renderer>();
        Color springGround = GroundColor(ground);

        // ---- Festivals and shop prices ----
        database.TryGet("item_turnipseeds", out ItemData turnipSeeds);
        database.TryGet("item_turnip", out ItemData turnip);
        var oswinShop = GameObject.Find("Merchant").GetComponent<Shopkeeper>();
        GoToDay(7);
        yield return Wait(0.3f);
        int seedPrice = oswinShop.BuyPrice(turnipSeeds);
        string message = null;
        Action<HudMessageEvent> onMessage = e => message = e.Text;
        EventBus<HudMessageEvent>.Subscribe(onMessage);
        GoToDay(8);
        yield return Wait(0.3f);
        Check(message != null && message.Contains("Planting Festival"), $"Spring 8 announces the Planting Festival (\"{message}\")");
        int festivalPrice = oswinShop.BuyPrice(turnipSeeds);
        Check(festivalPrice < seedPrice, $"seeds are cheaper at the festival ({seedPrice}g → {festivalPrice}g)");

        // Oswin mentions it.
        var oswin = GameObject.Find("Merchant");
        var runner = Services.Get<DialogueRunner>();
        motor.Teleport(oswin.transform.position + oswin.transform.forward * 2f + Vector3.up * 0.1f, Quaternion.LookRotation(-oswin.transform.forward));
        yield return Wait(0.5f);
        // The first meeting is his introduction; the festival greeting comes on the next visit.
        oswin.GetComponent<DialogueSpeaker>().Interact(interactor);
        yield return Wait(0.2f);
        Services.Get<GameStateService>().SetState(GameState.Playing);
        yield return Wait(0.3f);
        oswin.GetComponent<DialogueSpeaker>().Interact(interactor);
        yield return Wait(0.2f);
        float end = Time.realtimeSinceStartup + 4f;
        bool mentioned = false;
        while (runner.IsActive && Time.realtimeSinceStartup < end && runner.Choices.Count == 0)
        {
            mentioned |= runner.CurrentText.Contains("Planting Festival");
            runner.Advance();
            yield return null;
        }
        mentioned |= runner.CurrentText.Contains("Planting Festival");
        Check(mentioned, "Oswin greets you with the festival");
        Services.Get<GameStateService>().SetState(GameState.Playing);
        yield return Wait(0.3f);

        // ---- Summer ----
        message = null;
        GoToDay(15);
        yield return Wait(0.5f);
        Check(Calendar.Current == Season.Summer && keeper.Shown == Season.Summer, "day 15: summer");
        Check(message != null, $"the new season is announced (\"{message}\")");
        motor.Teleport(viewpoint, Quaternion.LookRotation(new Vector3(1f, 0f, 1f)));
        yield return Wait(1f);
        yield return Shot("s2_summer");

        // ---- Autumn: plant a turnip and a pumpkin on its last day; winter kills the turnip ----
        GoToDay(42);
        yield return Wait(0.5f);
        Check(Calendar.FestivalToday == Festival.HarvestFair, "Autumn 14 is the Harvest Fair");
        int fairPrice = oswinShop.SellPrice(turnip);
        motor.Teleport(viewpoint, Quaternion.LookRotation(new Vector3(1f, 0f, 1f)));
        yield return Wait(1f);
        yield return Shot("s3_autumn");
        Color autumnGround = GroundColor(ground);
        Check(ground == null || autumnGround != springGround, $"the grass turns in autumn ({springGround} → {autumnGround})");

        inventory.Add(turnipSeeds, 3);
        SelectSeed(player, turnipSeeds);
        yield return PlantAt(9);
        Check(TileHasCrop(9), "a turnip is planted in autumn");
        GoToDay(43);
        yield return Wait(0.5f);
        Check(oswinShop.SellPrice(turnip) < fairPrice, $"the fair's prices end with it ({fairPrice}g → {oswinShop.SellPrice(turnip)}g)");
        Check(Calendar.Current == Season.Winter && TileIsDead(9), "winter's first frost kills it");
        Check(message != null && (message.Contains("frost") || message.Contains("Winter")), $"and you're told (\"{message}\")");

        // ---- Winter: turnips can't be planted, frost kale can ----
        yield return ClearAt(9);
        SelectSeed(player, turnipSeeds);
        yield return Wait(0.3f);
        StandAt(10);
        yield return Wait(0.4f);
        bool tilledFirst = false;
        if (plot.TryGetPrompt(interactor, out var tillPrompt) && tillPrompt.Text.StartsWith("Till")) { plot.Interact(interactor); tilledFirst = true; }
        yield return Wait(0.2f);
        plot.TryGetPrompt(interactor, out var prompt);
        Check(!prompt.CanInteract && prompt.Text.Contains("won't grow"), $"turnip seeds in winter: \"{prompt.Text}\" (tilled first: {tilledFirst})");
        database.TryGet("item_frostkaleseeds", out ItemData kaleSeeds);
        inventory.Add(kaleSeeds, 2);
        SelectSeed(player, kaleSeeds);
        yield return Wait(0.2f);
        plot.TryGetPrompt(interactor, out prompt);
        Check(prompt.CanInteract && prompt.Text.Contains("Frost Kale"), $"frost kale can be planted: \"{prompt.Text}\"");

        // ---- Winter look and snow ----
        var weather = Services.Get<WeatherSystem>();
        Check(weather.IsSnow, "precipitation is snow in winter");
        weather.Set(Weather.Rain);
        motor.Teleport(viewpoint, Quaternion.LookRotation(new Vector3(1f, 0f, 1f)));
        yield return Wait(4f);
        yield return Shot("s4_winter_snow");
        var groundTexture = ground != null && ground.sharedMaterial != null && ground.sharedMaterial.HasProperty("_BaseMap") ? ground.sharedMaterial.GetTexture("_BaseMap") : null;
        Check(ground == null || (groundTexture != null && groundTexture.name.StartsWith("Snow")), $"snow lies on the ground ({groundTexture?.name})");
        var flowers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(r => r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("Env_Flowers")).ToList();
        Check(flowers.Count == 0 || flowers.All(r => !r.enabled), $"no flowers in winter ({flowers.Count} hidden)");
        var oaks = FindObjectsByType<Renderer>(FindObjectsSortMode.None).Count(r => r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("Env_Oak"));
        Check(oaks == 0, "the oaks are bare");
        weather.Set(Weather.Clear);

        // ---- Spring again ----
        GoToDay(57);
        yield return Wait(0.5f);
        Check(Calendar.Current == Season.Spring && keeper.Shown == Season.Spring && flowers.All(r => r == null || r.enabled), "spring returns, flowers and all");
        EventBus<HudMessageEvent>.Unsubscribe(onMessage);
        Finish();
    }

    static Color GroundColor(Renderer ground)
    {
        if (ground == null || ground.sharedMaterial == null) return Color.white;
        var m = ground.sharedMaterial;
        return m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
    }

    void GoToDay(int day)
    {
        double now = clock.TotalMinutes;
        double target = (day - 1) * (double)WorldClock.MinutesPerDay + 8 * 60;
        if (target > now) clock.AdvanceMinutes(target - now);
    }

    static void SelectSeed(GameObject player, ItemData seed)
    {
        var farmer = player.GetComponent<PlayerFarmer>();
        for (int i = 0; i < 6 && farmer.SelectedSeed != seed; i++) farmer.CycleSeed();
    }

    Vector3 TileCentre(int index)
    {
        var so = typeof(FarmPlot);
        int width = (int)so.GetField("width", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(plot);
        float size = (float)so.GetField("tileSize", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(plot);
        return plot.transform.TransformPoint(new Vector3((index % width + 0.5f) * size, 0f, (index / width + 0.5f) * size));
    }

    void StandAt(int index)
    {
        float reach = (float)typeof(FarmPlot).GetField("reach", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(plot);
        motor.Teleport(TileCentre(index) - Vector3.forward * reach + Vector3.up * 1.1f, Quaternion.identity);
    }

    IEnumerator PlantAt(int index)
    {
        StandAt(index);
        yield return Wait(0.4f);
        for (int i = 0; i < 2; i++) // till, then plant
        {
            plot.Interact(interactor);
            yield return Wait(0.2f);
        }
    }

    IEnumerator ClearAt(int index)
    {
        StandAt(index);
        yield return Wait(0.4f);
        plot.Interact(interactor);
        yield return Wait(0.2f);
    }

    object TileAtIndex(int index)
    {
        var tiles = (Array)typeof(FarmPlot).GetField("tiles", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(plot);
        return tiles.GetValue(index);
    }

    bool TileHasCrop(int index)
    {
        var tile = TileAtIndex(index);
        return tile.GetType().GetField("Crop").GetValue(tile) != null;
    }

    bool TileIsDead(int index)
    {
        var tile = TileAtIndex(index);
        return (bool)tile.GetType().GetField("Dead").GetValue(tile);
    }

    void Finish()
    {
        Debug.Log($"[SeasonTest] done, failures={failures}");
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
