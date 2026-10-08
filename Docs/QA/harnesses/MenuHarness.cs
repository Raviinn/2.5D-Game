using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Beast.Core;
using Beast.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Test-only: main menu and save slots. Runs only with -menuharness.</summary>
public sealed class MenuHarness : MonoBehaviour
{
    string dir;
    int failures;
    GameStateService state;
    SaveService save;
    SceneLoader loader;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-menuharness") < 0) return;
        var go = new GameObject("MenuHarness");
        DontDestroyOnLoad(go);
        go.AddComponent<MenuHarness>();
    }

    void Check(bool ok, string what)
    {
        if (!ok) failures++;
        Debug.Log($"[MenuTest] {(ok ? "PASS" : "FAIL")} {what}");
    }

    IEnumerator Start()
    {
        var args = Environment.GetCommandLineArgs();
        dir = args[Array.IndexOf(args, "-shots") + 1];
        Directory.CreateDirectory(dir);
        state = Services.Get<GameStateService>();
        save = Services.Get<SaveService>();
        loader = Services.Get<SceneLoader>();
        for (int i = 0; i < 3; i++)
            foreach (var path in new[] { SaveService.GetSlotPath(i), SaveService.GetSlotPath(i) + ".bak" })
                if (File.Exists(path)) File.Delete(path);

        // ---- Boot lands on the title screen ----
        yield return WaitFor(() => SceneManager.GetActiveScene().name == "MainMenu" && state.Current == GameState.MainMenu, 30f);
        yield return Wait(1.5f);
        var menu = FindFirstObjectByType<MainMenu>();
        menu.Refresh();
        Check(menu != null && state.Current == GameState.MainMenu, $"boot goes to the main menu ({SceneManager.GetActiveScene().name}, {state.Current})");
        Check(Cursor.visible, "cursor is visible on the menu");
        yield return new WaitForSecondsRealtime(2f);
        var audio = GameAudio.Instance;
        Check(audio != null && audio.Library.MusicMenu != null && audio.CurrentMusic == audio.Library.MusicMenu && audio.MusicVolumeNow > 0.05f,
            $"the title music plays ({(audio != null && audio.CurrentMusic != null ? audio.CurrentMusic.name : "none")}, volume {(audio != null ? audio.MusicVolumeNow : 0f):0.00})");
        Check(save.MostRecentSlot() == -1, "no saves: nothing to continue");
        yield return Shot("1_menu_empty");

        // ---- New game in slot 2 ----
        menu.StartNewGame(1);
        yield return WaitFor(() => InWorld() && save.HasSave(1), 40f);
        yield return Wait(1f);
        var clock = Services.Get<WorldClock>();
        var player = GameObject.FindWithTag("Player");
        var inventory = player.GetComponent<Inventory>();
        int startGold = inventory.Gold;
        Check(InWorld() && save.ActiveSlot == 1, $"new game starts the world in slot 2 (active slot {save.ActiveSlot + 1})");
        Check(save.HasSave(1) && !save.HasSave(0), "the new game claimed slot 2 straight away");
        Check(clock.Day == 1 && clock.Hour == 8, $"new game starts on day 1, 08:00 ({clock.Day} {clock.Hour:00}:{clock.Minute:00})");

        inventory.AddGold(77);
        int gold1 = inventory.Gold;
        clock.AdvanceMinutes(2 * WorldClock.MinutesPerDay);
        yield return Wait(0.5f);
        Check(save.Save(), "save to the active slot");
        var info = save.ReadSlot(1);
        Check(info.Readable && info.Summary.day == 3, $"slot 2 summary: day {info.Summary?.day}");
        Check(info.Summary.details.Contains($"{gold1} gold"), $"slot 2 summary details: {string.Join(" | ", info.Summary.details)}");
        Check(info.Summary.playSeconds > 1, $"slot 2 play time {info.Summary.playSeconds:0.0} s");

        state.SetState(GameState.Paused);
        yield return Wait(0.4f);
        yield return Shot("2_pause_menu");

        // ---- Quit to the main menu ----
        loader.LoadMainMenu();
        yield return WaitFor(() => SceneManager.GetActiveScene().name == "MainMenu" && state.Current == GameState.MainMenu, 30f);
        yield return Wait(1f);
        menu = FindFirstObjectByType<MainMenu>();
        menu.Refresh();
        Check(state.Current == GameState.MainMenu && Mathf.Approximately(Time.timeScale, 1f), $"quit to main menu ({state.Current}, timeScale {Time.timeScale})");
        Check(!Services.TryGet(out Reputation _), "the world's services are gone");
        Check(save.MostRecentSlot() == 1, "Continue offers slot 2");
        yield return Shot("3_menu_continue");

        SetPage(menu, 2); // Load Game
        yield return Wait(0.3f);
        yield return Shot("4_load_slots");
        SetPage(menu, 1); // New Game, asking before overwriting slot 2
        SetField(menu, "confirm", Enum.ToObject(Field(menu, "confirm").FieldType, 1));
        SetField(menu, "confirmSlot", 1);
        yield return Wait(0.3f);
        yield return Shot("5_overwrite_confirm");
        SetField(menu, "confirm", Enum.ToObject(Field(menu, "confirm").FieldType, 0));
        SetPage(menu, 0);

        // ---- A second new game in slot 1 starts fresh and leaves slot 2 alone ----
        menu.StartNewGame(0);
        yield return WaitFor(() => InWorld() && save.HasSave(0), 40f);
        yield return Wait(1f);
        inventory = GameObject.FindWithTag("Player").GetComponent<Inventory>();
        Check(clock.Day == 1 && clock.Hour == 8, $"second new game resets the clock to day 1, 08:00 ({clock.Day} {clock.Hour:00}:{clock.Minute:00})");
        Check(inventory.Gold == startGold, $"second new game has starting gold ({inventory.Gold})");
        Check(save.ActiveSlot == 0 && save.ReadSlot(1).Summary.day == 3, "slot 2 is untouched");
        Check(save.PlaySeconds < 10, $"play time restarts ({save.PlaySeconds:0.0} s)");

        // ---- Load slot 2 from the menu ----
        loader.LoadMainMenu();
        yield return WaitFor(() => SceneManager.GetActiveScene().name == "MainMenu" && state.Current == GameState.MainMenu, 30f);
        yield return Wait(0.5f);
        menu = FindFirstObjectByType<MainMenu>();
        menu.Refresh();
        menu.LoadSlot(1);
        yield return WaitFor(() => InWorld() && save.ActiveSlot == 1, 40f);
        yield return Wait(1f);
        player = GameObject.FindWithTag("Player");
        inventory = player.GetComponent<Inventory>();
        Check(inventory.Gold == gold1 && clock.Day == 3, $"loading slot 2 restores it (gold {inventory.Gold}/{gold1}, day {clock.Day})");
        Check(save.PlaySeconds > 1, $"play time carries on ({save.PlaySeconds:0.0} s)");

        // Sleeping autosaves to the active slot only.
        var slot0Time = save.SavedAt(0);
        FindFirstObjectByType<SleepSpot>().Interact(player.GetComponent<PlayerInteractor>());
        yield return Wait(0.5f);
        Check(save.ReadSlot(1).Summary.day == 4, $"sleeping saved slot 2 (day {save.ReadSlot(1).Summary.day})");
        Check(save.SavedAt(0) == slot0Time && save.ReadSlot(0).Summary.day == 1, "sleeping left slot 1 alone");
        yield return Wait(2.5f);

        // ---- Delete, old-format and damaged saves ----
        loader.LoadMainMenu();
        yield return WaitFor(() => SceneManager.GetActiveScene().name == "MainMenu" && state.Current == GameState.MainMenu, 30f);
        yield return Wait(0.5f);
        menu = FindFirstObjectByType<MainMenu>();
        menu.DeleteSlot(0);
        Check(!save.HasSave(0) && !File.Exists(SaveService.GetSlotPath(0) + ".bak"), "delete removes slot 1 and its backup");

        File.WriteAllText(SaveService.GetSlotPath(2), "{\"version\":1,\"sceneName\":\"World_Test\",\"savedAtUtc\":\"2026-01-01T00:00:00Z\",\"entries\":[]}");
        var legacy = save.ReadSlot(2);
        Check(legacy.Exists && legacy.Readable && legacy.Summary.day == 0, "a version 1 save (no summary) still reads");
        menu.Refresh();
        menu.LoadSlot(2);
        yield return WaitFor(() => InWorld() && save.ActiveSlot == 2, 40f);
        Check(InWorld() && save.ActiveSlot == 2, "a version 1 save loads");
        yield return Wait(1f);

        loader.LoadMainMenu();
        yield return WaitFor(() => SceneManager.GetActiveScene().name == "MainMenu" && state.Current == GameState.MainMenu, 30f);
        yield return Wait(0.5f);
        menu = FindFirstObjectByType<MainMenu>();
        File.WriteAllText(SaveService.GetSlotPath(2), "{ this is not a save");
        var damaged = save.ReadSlot(2);
        Check(damaged.Exists && !damaged.Readable, "a damaged save is reported as damaged");
        menu.Refresh();
        menu.LoadSlot(2);
        yield return Wait(1.5f);
        Check(SceneManager.GetActiveScene().name == "MainMenu" && state.Current == GameState.MainMenu, "loading a damaged save stays on the menu");
        Check(save.MostRecentSlot() == 1, "Continue skips the damaged save");
        SetPage(menu, 2);
        yield return Wait(0.3f);
        yield return Shot("6_load_with_damaged");

        Debug.Log($"[MenuTest] done, {failures} failure(s)");
        Application.Quit();
    }

    static bool InWorld() =>
        SceneManager.GetActiveScene().name == "World_Test" && GameObject.FindWithTag("Player") != null &&
        Services.Get<GameStateService>().Current == GameState.Playing;

    static void SetPage(MainMenu menu, int page)
    {
        var method = typeof(MainMenu).GetMethod("Open", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Invoke(menu, new[] { Enum.ToObject(Field(menu, "page").FieldType, page) });
    }

    static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    static void SetField(object target, string name, object value) => Field(target, name).SetValue(target, value);

    static IEnumerator Wait(float seconds) => new WaitForSecondsRealtime(seconds);

    static IEnumerator WaitFor(Func<bool> condition, float timeout)
    {
        float end = Time.realtimeSinceStartup + timeout;
        while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        if (!condition()) Debug.Log("[MenuTest] FAIL timed out waiting");
    }

    IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();
        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
        Destroy(tex);
        Debug.Log($"[MenuTest] shot {name}");
    }
}
