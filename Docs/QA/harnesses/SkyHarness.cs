using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Beast.Core;
using Beast.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Test-only: checks the day/night cycle, weather, rain watering and lanterns. Runs only with -skyharness.</summary>
public sealed class SkyHarness : MonoBehaviour
{
    string dir;
    int failures;
    GameObject player;
    PlayerMotor motor;
    ThirdPersonCamera rig;
    WorldClock clock;
    WeatherSystem weather;
    DayNightCycle cycle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-skyharness") < 0) return;
        var go = new GameObject("SkyHarness");
        DontDestroyOnLoad(go);
        go.AddComponent<SkyHarness>();
    }

    void Check(bool ok, string what)
    {
        if (!ok) failures++;
        Debug.Log($"[SkyTest] {(ok ? "PASS" : "FAIL")} {what}");
    }

    void PlaceAt(Vector3 position, float yaw)
    {
        motor.Teleport(position, Quaternion.Euler(0f, yaw, 0f));
        typeof(ThirdPersonCamera).GetField("yaw", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(rig, yaw);
    }

    /// <summary>Jumps the clock to the given hour (today or tomorrow) and fixes the weather.</summary>
    IEnumerator At(int hour, Weather sky)
    {
        clock.AdvanceToHour(hour);
        weather.Set(sky);
        typeof(WeatherSystem).GetMethod("SnapVisuals", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(weather, null);
        yield return Wait(1.2f);
    }

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
        Check(Services.TryGet(out weather), "weather system running");
        Check(Services.TryGet(out cycle), "day/night cycle running");
        Check(weather.Current == Weather.Clear, "day 1 is clear");
        var sun = RenderSettings.sun;
        var lanterns = FindObjectsByType<LanternLight>(FindObjectsSortMode.None);
        Check(lanterns.Length >= 3, $"{lanterns.Length} lanterns / fires in the world");

        // Village view: houses, square and lanterns in frame.
        PlaceAt(new Vector3(1f, 1.1f, -1f), 40f);

        yield return At(12, Weather.Clear);
        float noonSun = sun.intensity;
        float noonLight = 0f;
        yield return Brightness(v => noonLight = v);
        int litAtNoon = Lit(lanterns);
        Check(noonSun > 0.9f && cycle.Darkness < 0.05f, $"noon: sun at {noonSun:0.00}, darkness {cycle.Darkness:0.00}");
        Check(litAtNoon == 0, "lanterns are off at noon");
        yield return Shot("1_noon_clear");

        yield return At(19, Weather.Clear);
        yield return Wait(0.5f);
        Check(sun.color.b < sun.color.r * 0.8f, $"dusk: the sun turns warm ({sun.color})");
        yield return Shot("2_dusk");

        yield return At(23, Weather.Clear);
        yield return Wait(2.5f); // lanterns fade in
        float nightLight = 0f;
        yield return Brightness(v => nightLight = v);
        Check(sun.color.b > sun.color.r && sun.intensity < 0.4f, $"night: moonlight is dim and blue ({sun.intensity:0.00}, {sun.color})");
        Check(nightLight < noonLight * 0.5f && nightLight > 0.02f, $"night is dark but visible: screen {nightLight:0.000} vs {noonLight:0.000} at noon");
        Check(Lit(lanterns) == lanterns.Length, $"lanterns are lit at night ({Lit(lanterns)}/{lanterns.Length})");
        Check(cycle.IsNight, "IsNight at 23:00");
        yield return Shot("3_night_lanterns");

        // Rain by day.
        yield return At(14, Weather.Rain);
        yield return Wait(1.5f);
        var rain = weather.GetComponentInChildren<ParticleSystem>();
        Check(rain != null && rain.isPlaying && rain.particleCount > 500, $"rain is falling ({(rain != null ? rain.particleCount : 0)} drops)");
        Check(RenderSettings.fogEndDistance < 80f, $"rain closes in the fog (ends at {RenderSettings.fogEndDistance:0} m)");
        Check(sun.intensity < noonSun * 0.5f, $"rain dims the sun ({sun.intensity:0.00} vs {noonSun:0.00})");
        float rainLight = 0f;
        yield return Brightness(v => rainLight = v);
        Check(rainLight < noonLight, $"rainy day is darker than a clear one ({rainLight:0.000} vs {noonLight:0.000})");
        yield return Shot("4_rain_day");

        // Rain waters the field.
        var plot = FindFirstObjectByType<FarmPlot>();
        var tilesField = typeof(FarmPlot).GetField("tiles", BindingFlags.Instance | BindingFlags.NonPublic);
        var tiles = (Array)tilesField.GetValue(plot);
        var tileType = tiles.GetType().GetElementType();
        object tile = tiles.GetValue(0);
        tileType.GetField("Tilled").SetValue(tile, true);
        tileType.GetField("Watered").SetValue(tile, false);
        tiles.SetValue(tile, 0);
        weather.Set(Weather.Clear);
        weather.Set(Weather.Rain);
        Check((bool)tileType.GetField("Watered").GetValue(tiles.GetValue(0)), "rain starting waters tilled soil");

        // ...and a rainy new day waters it after the overnight dry-out.
        int rainyDay = -1;
        for (int d = clock.Day + 1; d < clock.Day + 60 && rainyDay < 0; d++) if (weather.Roll(d) == Weather.Rain) rainyDay = d;
        Check(rainyDay > 0, $"found a rainy day ahead (day {rainyDay})");
        if (rainyDay > 0)
        {
            while (clock.Day < rainyDay - 1) clock.AdvanceToHour(12);
            clock.AdvanceToHour(23);
            weather.Set(Weather.Clear);
            tile = tiles.GetValue(0);
            tileType.GetField("Watered").SetValue(tile, false);
            tiles.SetValue(tile, 0);
            clock.AdvanceToHour(6); // crosses midnight into the rainy day
            Check(weather.Current == Weather.Rain && clock.Day == rainyDay, $"day {clock.Day} is rainy as rolled");
            Check((bool)tileType.GetField("Watered").GetValue(tiles.GetValue(0)), "a rainy day waters the field after the overnight dry-out");
        }

        // Rain at night + HUD icon.
        yield return At(22, Weather.Rain);
        yield return Wait(2f);
        yield return Shot("5_rain_night");

        // Save / load keeps the weather.
        var save = Services.Get<SaveService>();
        yield return At(10, Weather.Rain);
        Check(save.Save(1), "saved while raining");
        weather.Set(Weather.Clear);
        save.Load(1);
        yield return Wait(1f);
        Check(weather.Current == Weather.Rain, $"load restores the weather ({weather.Current})");

        // Same seed, same weather.
        bool deterministic = true;
        for (int d = 1; d < 40; d++) deterministic &= weather.Roll(d) == weather.Roll(d);
        int rainy = 0, cloudy = 0;
        for (int d = 2; d < 202; d++) { var w = weather.Roll(d); if (w == Weather.Rain) rainy++; else if (w == Weather.Cloudy) cloudy++; }
        Check(deterministic && rainy > 20 && rainy < 60 && cloudy > 25 && cloudy < 75, $"weather mix over 200 days: {rainy} rainy, {cloudy} cloudy");

        Debug.Log($"[SkyTest] done, failures={failures}");
        Application.Quit();
    }

    static int Lit(LanternLight[] lanterns)
    {
        int lit = 0;
        foreach (var l in lanterns) if (l.GetComponent<Light>().enabled && l.GetComponent<Light>().intensity > 0.3f) lit++;
        return lit;
    }

    IEnumerator Brightness(Action<float> result)
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        var pixels = tex.GetPixels32();
        double sum = 0;
        for (int i = 0; i < pixels.Length; i += 7) sum += (0.2126 * pixels[i].r + 0.7152 * pixels[i].g + 0.0722 * pixels[i].b) / 255.0;
        Destroy(tex);
        result((float)(sum / (pixels.Length / 7)));
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
