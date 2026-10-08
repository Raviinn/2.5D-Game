using System;
using System.Collections;
using System.IO;
using System.Linq;
using Beast.Core;
using Beast.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Test-only: the rotating contracts board (Milestone 48). Runs with -boardharness.</summary>
public sealed class BoardHarness : MonoBehaviour
{
    string dir;
    int failures;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-boardharness") < 0) return;
        var go = new GameObject("BoardHarness");
        DontDestroyOnLoad(go);
        go.AddComponent<BoardHarness>();
    }

    void Check(bool ok, string what)
    {
        if (!ok) failures++;
        Debug.Log($"[BoardTest] {(ok ? "PASS" : "FAIL")} {what}");
    }

    IEnumerator Start()
    {
        var args = Environment.GetCommandLineArgs();
        dir = args[Array.IndexOf(args, "-shots") + 1];
        Directory.CreateDirectory(dir);
        while (SceneManager.GetActiveScene().name != "World_Test" || GameObject.FindWithTag("Player") == null) yield return null;
        yield return Wait(3f);

        var player = GameObject.FindWithTag("Player");
        var log = player.GetComponent<QuestLog>();
        var reputation = player.GetComponent<Reputation>();
        var inventory = player.GetComponent<Inventory>();
        var clock = Services.Get<WorldClock>();
        var board = FindFirstObjectByType<ContractBoard>();
        Check(board != null && board.Pool.Length >= 12, $"the board has a pool of contracts ({board?.Pool.Length})");
        if (board == null) { Finish(); yield break; }

        var today = board.Contracts;
        Check(today.Length == board.PostedPerDay, $"it posts {board.PostedPerDay} a day ({string.Join(", ", today.Select(c => c.Title))})");
        Check(today.SequenceEqual(board.Contracts), "the same draw all day");

        // Over three weeks: the postings change, nothing above Known shows at Stranger, at most one locked teaser.
        var seen = new System.Collections.Generic.HashSet<QuestData>();
        int distinctDays = 0;
        string previous = null;
        bool tierOk = true, teaserOk = true;
        for (int d = 0; d < 21; d++)
        {
            var posted = board.Contracts;
            string key = string.Join("|", posted.Select(c => c.Id));
            if (key != previous) distinctDays++;
            previous = key;
            foreach (var c in posted) seen.Add(c);
            if (posted.Any(c => c.RequiredTier > reputation.TierIndex + 1)) tierOk = false;
            if (posted.Count(c => c.RequiredTier > reputation.TierIndex) > 1) teaserOk = false;
            clock.AdvanceMinutes(WorldClock.MinutesPerDay);
            yield return null;
        }
        Check(distinctDays >= 15, $"the board changes from day to day ({distinctDays} different postings in 21 days)");
        Check(seen.Count >= 9, $"most of the pool turns up over three weeks ({seen.Count}/{board.Pool.Length})");
        Check(tierOk, "contracts two tiers above you never show");
        Check(teaserOk, "at most one locked contract (a teaser) a day");

        // A taken contract stays posted until handed in.
        var take = board.Contracts.FirstOrDefault(c => log.CanStart(c) && c.Objectives.All(o => o.Type == ObjectiveType.Collect));
        Check(take != null, "an open collect contract to take");
        if (take != null)
        {
            log.StartQuest(take);
            for (int d = 0; d < 3; d++)
            {
                clock.AdvanceMinutes(WorldClock.MinutesPerDay);
                yield return null;
            }
            Check(board.Contracts.Contains(take), $"\"{take.Title}\" stays posted while you hold it (3 days on)");
            Check(board.Contracts.Length == board.PostedPerDay + 1, "on top of the day's fresh postings");
            foreach (var o in take.Objectives) inventory.Add(o.Item, o.Count);
            yield return null;
            Check(log.TurnIn(take), "hand it in");
            Check(board.Contracts.Contains(take), "it shows as done today");
        }

        // Standing opens up the gated contracts.
        reputation.Change(160, StandingSource.Debug);
        yield return null;
        Check(reputation.TierIndex >= 2, $"now Trusted (tier {reputation.TierIndex})");
        bool shieldSeen = false;
        for (int d = 0; d < 30 && !shieldSeen; d++)
        {
            clock.AdvanceMinutes(WorldClock.MinutesPerDay);
            yield return null;
            shieldSeen = board.Contracts.Any(c => c.Title == "Shield-Breaker" && log.CanStart(c));
        }
        Check(shieldSeen, "Trusted: Shield-Breaker turns up, open to take");

        var interactor = player.GetComponent<PlayerInteractor>();
        board.Interact(interactor);
        yield return Wait(0.6f);
        yield return Shot("b1_board");
        Services.Get<GameStateService>().SetState(GameState.Playing);
        Finish();
    }

    void Finish()
    {
        Debug.Log($"[BoardTest] done, failures={failures}");
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
