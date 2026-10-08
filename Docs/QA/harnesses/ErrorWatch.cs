using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Test-only: counts every error, exception and assert logged during a harness run (any -…harness flag) and
/// reports them when the player quits: "[ErrorWatch] errors=N unique=M", then the first few distinct messages.
/// run_all_harnesses prints the count, so errors that don't fail a check (a particle system complaining every frame)
/// still show up.
/// </summary>
public static class ErrorWatch
{
    static int count;
    static readonly List<string> unique = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        bool harness = Array.Exists(Environment.GetCommandLineArgs(), a => a.StartsWith("-") && a.EndsWith("harness"));
        if (!harness) return;
        Application.logMessageReceived += OnLog;
        Application.quitting += Report;
    }

    static void OnLog(string message, string stackTrace, LogType type)
    {
        if (type is not (LogType.Error or LogType.Exception or LogType.Assert)) return;
        if (message.StartsWith("[ErrorWatch]")) return;
        if (message.Contains("is unreadable or from a newer version")) return; // the menu test writes a broken save on purpose
        count++;
        string first = message.Split('\n')[0];
        if (unique.Count < 50 && !unique.Contains(first)) unique.Add(first);
    }

    static void Report()
    {
        Application.logMessageReceived -= OnLog;
        Debug.Log($"[ErrorWatch] errors={count} unique={unique.Count}");
        for (int i = 0; i < unique.Count && i < 5; i++) Debug.Log($"[ErrorWatch] #{i + 1}: {unique[i]}");
    }
}
