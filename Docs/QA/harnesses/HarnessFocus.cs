using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Test-only: with any -…harness flag, the Input System keeps reading devices while the player window is in the
/// background. The harnesses type on a virtual keyboard; without this their keys are ignored as soon as someone
/// clicks another window on the same PC (the player keeps running thanks to runInBackground, but input stops).
/// </summary>
public static class HarnessFocus
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (Array.FindIndex(Environment.GetCommandLineArgs(), a => a.EndsWith("harness", StringComparison.OrdinalIgnoreCase)) < 0) return;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        Debug.Log("[HarnessFocus] input keeps working in the background");
    }
}
