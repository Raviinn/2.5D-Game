using System;
using System.Collections;
using Beast.Core;
using Beast.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Test-only: the older milestone harnesses expect the game to boot straight into World_Test. Since Milestone 16 it
/// boots to the main menu, so for those flags this starts a new game in slot 1 from the menu.
/// </summary>
public sealed class HarnessAutoStart : MonoBehaviour
{
    static readonly string[] LegacyFlags = { "-uiharness", "-repharness", "-climbharness", "-aiharness", "-skyharness", "-nightharness", "-craftharness", "-townharness" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var args = Environment.GetCommandLineArgs();
        Debug.Log($"[AutoStart] boot, args: {string.Join(" ", args)}");
        if (Array.FindIndex(LegacyFlags, f => Array.IndexOf(args, f) >= 0) < 0) return;
        var go = new GameObject("HarnessAutoStart");
        DontDestroyOnLoad(go);
        go.AddComponent<HarnessAutoStart>();
        Debug.Log("[AutoStart] created");
    }

    IEnumerator Start()
    {
        Debug.Log("[AutoStart] start");
        for (int i = 0; i < SaveService.SlotCount; i++)
            foreach (var path in new[] { SaveService.GetSlotPath(i), SaveService.GetSlotPath(i) + ".bak" })
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        var state = Services.Get<GameStateService>();
        float end = Time.realtimeSinceStartup + 60f;
        int tries = 0;
        while (SceneManager.GetActiveScene().name != "World_Test" && Time.realtimeSinceStartup < end)
        {
            yield return new WaitForSecondsRealtime(0.5f);
            var menu = FindFirstObjectByType<MainMenu>();
            Debug.Log($"[AutoStart] try {++tries}: scene {SceneManager.GetActiveScene().name}, state {state.Current}, menu {(menu != null)}, timeScale {Time.timeScale}");
            if (menu != null && state.Current == GameState.MainMenu) menu.StartNewGame(0);
        }
        Debug.Log($"[AutoStart] done: scene {SceneManager.GetActiveScene().name}");
        Destroy(gameObject);
    }
}
