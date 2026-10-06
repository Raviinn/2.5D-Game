using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Beast.Core;
using Beast.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

/// <summary>Test-only: drives the player with a virtual keyboard through the climbing course. Runs only with -climbharness.</summary>
public sealed class ClimbHarness : MonoBehaviour
{
    static readonly Vector3 O = new(-20f, 0f, -13f); // course origin (ClimbingSetup)

    string dir;
    int failures;
    Keyboard keyboard;
    GameObject player;
    PlayerClimber climber;
    PlayerMotor motor;
    ThirdPersonCamera rig;
    readonly HashSet<ClimbMode> seen = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-climbharness") < 0) return;
        var go = new GameObject("ClimbHarness");
        DontDestroyOnLoad(go);
        go.AddComponent<ClimbHarness>();
    }

    void Check(bool ok, string what)
    {
        if (!ok) failures++;
        Debug.Log($"[ClimbTest] {(ok ? "PASS" : "FAIL")} {what}");
    }

    void Update()
    {
        if (climber != null) seen.Add(climber.Mode);
    }

    void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));

    IEnumerator Press(Key key, params Key[] held)
    {
        var down = new List<Key>(held) { key };
        Keys(down.ToArray());
        yield return null;
        yield return null;
        Keys(held);
        yield return null;
    }

    void Face(float yaw)
    {
        typeof(ThirdPersonCamera).GetField("yaw", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(rig, yaw);
        motor.Teleport(player.transform.position, Quaternion.Euler(0f, yaw, 0f));
    }

    void PlaceAt(Vector3 position, float yaw)
    {
        motor.Teleport(position, Quaternion.Euler(0f, yaw, 0f));
        typeof(ThirdPersonCamera).GetField("yaw", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(rig, yaw);
    }

    IEnumerator Start()
    {
        var args = Environment.GetCommandLineArgs();
        dir = args[Array.IndexOf(args, "-shots") + 1];
        Directory.CreateDirectory(dir);
        while (SceneManager.GetActiveScene().name != "World_Test" || GameObject.FindWithTag("Player") == null) yield return null;
        yield return new WaitForSecondsRealtime(3f);

        keyboard = InputSystem.AddDevice<Keyboard>("HarnessKeyboard");
        player = GameObject.FindWithTag("Player");
        climber = player.GetComponent<PlayerClimber>();
        motor = player.GetComponent<PlayerMotor>();
        rig = FindFirstObjectByType<ThirdPersonCamera>();
        var combat = player.GetComponent<PlayerCombat>();
        var stamina = player.GetComponent<Stamina>();
        var state = Services.Get<GameStateService>();

        Check(climber != null, "player has a PlayerClimber");
        Check(GameObject.Find("Climbing_Course") != null, "climbing course exists");
        var sheet = player.GetComponentInChildren<DirectionalSpriteRenderer>();
        var sheetField = sheet != null ? typeof(DirectionalSpriteRenderer).GetField("sheet", BindingFlags.Instance | BindingFlags.NonPublic) : null;
        var sheetAsset = sheetField != null ? sheetField.GetValue(sheet) as DirectionalSpriteSheet : null;
        Check(sheetAsset != null && sheetAsset.Find(CharacterAnim.Hang).Anim == CharacterAnim.Hang && sheetAsset.Find(CharacterAnim.Climb).Anim == CharacterAnim.Climb,
            "Knight sheet has Hang and Climb clips");
        Check(sheetAsset != null && sheetAsset.Texture.width >= PlaceholderColumns() * 48, $"Knight sheet texture is {sheetAsset?.Texture.width}px wide");

        // ---- A: vault the 2.3 m block (face at x = O.x + 6) ----
        PlaceAt(new Vector3(O.x + 9.5f, 1.1f, O.z - 0.5f), -90f);
        yield return Wait(0.5f);
        seen.Clear();
        Keys(Key.W);
        yield return Wait(0.35f);
        yield return Press(Key.Space, Key.W);
        yield return Until(() => seen.Contains(ClimbMode.PullingUp) && climber.Mode == ClimbMode.None, 2f);
        Keys();
        yield return Wait(0.4f);
        Check(seen.Contains(ClimbMode.PullingUp) && !seen.Contains(ClimbMode.Hanging), $"2.3 m block is vaulted (modes: {string.Join(",", seen)})");
        Check(player.transform.position.y > 3.0f && climber.Mode == ClimbMode.None, $"standing on the 2.3 m block (y {player.transform.position.y:0.00})");
        Check(!combat.IsTraversing, "combat is back to normal after the vault");

        // ---- B: hang from the 3.2 m wall (face at x = O.x + 2) ----
        PlaceAt(new Vector3(O.x + 4.6f, 1.1f, O.z + 2f), -90f);
        yield return Wait(0.5f);
        seen.Clear();
        Keys(Key.W);
        yield return Wait(0.2f);
        yield return Press(Key.Space, Key.W);
        yield return Wait(0.9f);
        Keys();
        yield return Wait(0.3f);
        Check(climber.Mode == ClimbMode.Hanging, $"hanging from the 3.2 m wall (mode {climber.Mode}, y {player.transform.position.y:0.00})");
        Check(combat.IsTraversing && !combat.CanUseItems, "combat and items are off while hanging");
        float hangY = player.transform.position.y;
        Check(Mathf.Abs(hangY - (3.2f - 2.2f + 1f)) < 0.1f, $"hands on the edge: body y {hangY:0.00} (expected 2.00)");
        float stamina0 = stamina.Current;
        yield return Wait(1f);
        Check(stamina.Current < stamina0 - 1.5f, $"hanging drains stamina ({stamina0:0.0} -> {stamina.Current:0.0})");
        yield return Shot("c1_hang");

        // Shimmy along +z (camera faces -x, so D = +z).
        float z0 = player.transform.position.z;
        Keys(Key.D);
        yield return Wait(1f);
        Check(player.transform.position.z > z0 + 1f && climber.Mode == ClimbMode.Hanging, $"shimmy moves along the wall (z {z0:0.00} -> {player.transform.position.z:0.00})");
        yield return Shot("c2_shimmy");
        yield return Wait(4f);
        float zEnd = player.transform.position.z;
        Check(climber.Mode == ClimbMode.Hanging && zEnd < O.z + 4.5f && zEnd > O.z + 3f, $"shimmy stops at the end of the wall (z {zEnd:0.00}, wall ends at {O.z + 4.5f:0.00})");
        Keys();
        yield return Wait(0.2f);

        // Pull up.
        stamina.Refill();
        yield return Press(Key.Space);
        yield return Wait(1f);
        Check(climber.Mode == ClimbMode.None && player.transform.position.y > 4.0f, $"pulled up onto the wall (y {player.transform.position.y:0.00})");

        // ---- C: climb the ivy cliff from the wall top (cliff face at x = O.x) ----
        PlaceAt(new Vector3(O.x + 0.9f, 3.2f + 1.1f, O.z + 1f), -90f);
        yield return Wait(0.3f);
        Check(Contains(climber.Hints, "Climb"), "'[Space] Climb' hint shows facing the ivy");
        yield return Shot("c3_ivy_hint");
        seen.Clear();
        Keys(Key.W);
        yield return Wait(0.15f);
        yield return Press(Key.Space, Key.W);
        yield return Wait(0.25f);
        yield return Shot("c4_climbing");
        yield return Wait(0.2f);
        Check(seen.Contains(ClimbMode.Climbing), $"grabbed the ivy and climbed (modes: {string.Join(",", seen)})");
        yield return Until(() => seen.Contains(ClimbMode.PullingUp) && climber.Mode == ClimbMode.None, 5f);
        Keys();
        yield return Wait(0.8f);
        Check(seen.Contains(ClimbMode.PullingUp) && climber.Mode == ClimbMode.None && player.transform.position.y > 6.8f,
            $"climbed to the top of the 6 m cliff (y {player.transform.position.y:0.00})");
        yield return Shot("c5_cliff_top");

        // ---- D: let go ----
        PlaceAt(new Vector3(O.x + 4.6f, 1.1f, O.z + 1.3f), -90f);
        stamina.Refill();
        yield return Wait(0.5f);
        Keys(Key.W);
        yield return Wait(0.2f);
        yield return Press(Key.Space, Key.W);
        yield return Wait(0.9f);
        Keys();
        yield return Wait(0.2f);
        bool hanging = climber.Mode == ClimbMode.Hanging;
        float xBefore = player.transform.position.x;
        yield return Press(Key.LeftShift);
        bool dodged = false;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime) { dodged |= combat.StateName == "Dodging"; yield return null; }
        Check(!dodged && Mathf.Abs(player.transform.position.x - xBefore) < 0.3f, $"letting go with Shift doesn't also dodge (x {xBefore:0.00} -> {player.transform.position.x:0.00})");
        Check(hanging && climber.Mode == ClimbMode.None && player.transform.position.y < 1.3f, $"Shift lets go and you drop (y {player.transform.position.y:0.00})");

        // ---- E: running out of stamina ----
        yield return Wait(0.6f);
        Keys(Key.W);
        yield return Wait(0.2f);
        yield return Press(Key.Space, Key.W);
        yield return Wait(0.9f);
        Keys();
        hanging = climber.Mode == ClimbMode.Hanging;
        Debug.Log($"[ClimbTest] stamina test: mode {climber.Mode}, pos {player.transform.position}");
        stamina.Spend(stamina.Current - 1f);
        yield return Wait(1f);
        Check(hanging && climber.Mode == ClimbMode.None && player.transform.position.y < 1.3f, "falls when stamina runs out");

        // ---- F: houses can't be climbed (House_A's west face at x = 7) ----
        stamina.Refill();
        PlaceAt(new Vector3(4.4f, 1.1f, 6f), 90f);
        yield return Wait(0.5f);
        seen.Clear();
        Keys(Key.W);
        yield return Wait(0.2f);
        yield return Press(Key.Space, Key.W);
        yield return Wait(1f);
        Keys();
        yield return Wait(0.5f);
        Check(!seen.Contains(ClimbMode.Hanging) && !seen.Contains(ClimbMode.PullingUp) && player.transform.position.y < 2f, "houses aren't grabbed");

        // ---- G: a plain jump in the open still jumps ----
        PlaceAt(new Vector3(O.x + 12f, 1.1f, O.z + 8f), 0f);
        yield return Wait(0.5f);
        float y0 = player.transform.position.y;
        yield return Press(Key.Space);
        yield return Wait(0.25f);
        Check(player.transform.position.y > y0 + 0.5f && climber.Mode == ClimbMode.None, "Space still jumps away from walls");
        yield return Wait(1f);

        // ---- H: loading a save mid-hang drops the hang ----
        var save = Services.Get<SaveService>();
        PlaceAt(new Vector3(O.x + 4.6f, 1.1f, O.z + 2f), -90f);
        yield return Wait(0.5f);
        Check(save.Save(1), "saved on the ground");
        Keys(Key.W);
        yield return Wait(0.2f);
        yield return Press(Key.Space, Key.W);
        yield return Wait(0.9f);
        Keys();
        hanging = climber.Mode == ClimbMode.Hanging;
        save.Load(1);
        yield return Wait(1f);
        Check(hanging && climber.Mode == ClimbMode.None && player.transform.position.y < 1.3f && !combat.IsTraversing,
            $"loading mid-hang returns to the saved spot (y {player.transform.position.y:0.00})");

        // ---- I: pause menu lists climbing ----
        state.SetState(GameState.Paused);
        SetField(FindFirstObjectByType<PauseMenu>(), "showControls", true);
        yield return Wait(0.4f);
        yield return Shot("c6_pause_controls");
        state.SetState(GameState.Playing);

        yield return ShadowTests();
        Debug.Log($"[ClimbTest] done, failures={failures}");
        Application.Quit();
    }

    static int PlaceholderColumns() => 27;

    // ---------- Sprite shadows (Milestone 12) ----------

    IEnumerator ShadowTests()
    {
        var visual = player.GetComponentInChildren<DirectionalSpriteRenderer>();
        var renderers = visual.GetComponentsInChildren<MeshRenderer>();
        MeshRenderer body = null, shadowQuad = null;
        foreach (var r in renderers)
            if (r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) shadowQuad = r; else body = r;
        Check(body != null && body.sharedMaterial.shader.name == "Beast/Sprite Lit", $"character sprite uses Beast/Sprite Lit ({body?.sharedMaterial.shader.name})");
        Check(body != null && body.receiveShadows, "character sprite receives shadows");

        Light sunLight = RenderSettings.sun;
        if (sunLight == null) foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) sunLight = l;
        Vector3 sunDir = sunLight.transform.forward;
        Vector3 sunFlat = new Vector3(sunDir.x, 0f, sunDir.z).normalized;
        Debug.Log($"[ClimbTest] sun forward {sunDir}");

        // Open ground in full sun: the sprite must not be darkened by its own shadow quad.
        Vector3 open = FindSpot(p => !Blocked(p, 0.3f, sunDir) && !Blocked(p, 1.8f, sunDir), new Vector3(O.x + 14f, 0f, O.z + 9f));
        PlaceAt(open + Vector3.up * 1.1f, 180f);
        yield return Wait(0.8f);
        float lit = 0f;
        yield return Measure(body, v => lit = v);
        shadowQuad.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        yield return Wait(0.2f);
        float litNoQuad = 0f;
        yield return Measure(body, v => litNoQuad = v);
        shadowQuad.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        Check(lit > 0.05f && Mathf.Abs(lit - litNoQuad) < litNoQuad * 0.08f, $"no self-shadow: sprite brightness {lit:0.000} with its shadow quad, {litNoQuad:0.000} without");
        yield return Shot("s1_sun");

        // Full shade behind the ivy cliff.
        var cliff = GameObject.Find("Ivy_Cliff").GetComponent<Collider>().bounds;
        Vector3 start = new Vector3(cliff.center.x, 0f, cliff.center.z);
        float exit = 0f;
        while (cliff.Contains(new Vector3(start.x, 1f, start.z) + sunFlat * exit) && exit < 20f) exit += 0.1f;
        Vector3 shade = FindSpot(p => Blocked(p, 0.3f, sunDir) && Blocked(p, 1.8f, sunDir), start + sunFlat * (exit + 1.2f));
        PlaceAt(shade + Vector3.up * 1.1f, 180f);
        yield return Wait(0.8f);
        float shaded = 0f;
        yield return Measure(body, v => shaded = v);
        Check(shaded < lit * 0.8f, $"in the cliff's shadow the sprite is darker ({shaded:0.000} vs {lit:0.000} in sun)");
        yield return Shot("s2_shade");

        // Shadow edge: legs in shade, head in sun.
        Vector3 edge = Vector3.zero;
        bool found = false;
        for (float d = exit; d < exit + 25f && !found; d += 0.2f)
        {
            var p = start + sunFlat * d;
            if (Blocked(p, 0.3f, sunDir) && !Blocked(p, 1.7f, sunDir) && Clear(p)) { edge = p; found = true; }
        }
        Check(found, "found a spot on the shadow's edge");
        if (found)
        {
            PlaceAt(edge + Vector3.up * 1.1f, 180f);
            yield return Wait(0.8f);
            yield return Shot("s3_shadow_edge");
        }
    }

    static bool Blocked(Vector3 ground, float height, Vector3 sunDir) =>
        Physics.Raycast(ground + Vector3.up * height, -sunDir, 200f, 1, QueryTriggerInteraction.Ignore);

    static bool Clear(Vector3 ground) => !Physics.CheckCapsule(ground + Vector3.up * 0.5f, ground + Vector3.up * 1.6f, 0.45f, 1, QueryTriggerInteraction.Ignore);

    static Vector3 FindSpot(Func<Vector3, bool> ok, Vector3 near)
    {
        for (float r = 0f; r < 20f; r += 0.5f)
            for (int a = 0; a < 16; a++)
            {
                var p = near + Quaternion.Euler(0f, a * 22.5f, 0f) * Vector3.forward * r;
                p.y = 0f;
                if (Clear(p) && ok(p)) return p;
            }
        return near;
    }

    /// <summary>Average brightness of the sprite's own pixels: frames with and without the body, compared.</summary>
    IEnumerator Measure(MeshRenderer body, Action<float> result)
    {
        yield return new WaitForEndOfFrame();
        var with = ScreenCapture.CaptureScreenshotAsTexture();
        body.enabled = false;
        yield return null;
        yield return new WaitForEndOfFrame();
        var without = ScreenCapture.CaptureScreenshotAsTexture();
        body.enabled = true;
        var a = with.GetPixels32();
        var b = without.GetPixels32();
        double sum = 0;
        int count = 0;
        for (int i = 0; i < a.Length; i++)
        {
            int diff = Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b);
            if (diff < 40) continue;
            sum += (0.2126 * a[i].r + 0.7152 * a[i].g + 0.0722 * a[i].b) / 255.0;
            count++;
        }
        Destroy(with);
        Destroy(without);
        Debug.Log($"[ClimbTest] measured {count} sprite pixels");
        result(count > 50 ? (float)(sum / count) : 0f);
    }

    static IEnumerator Until(Func<bool> done, float timeout)
    {
        float end = Time.realtimeSinceStartup + timeout;
        while (!done() && Time.realtimeSinceStartup < end) yield return null;
    }

    static bool Contains(IReadOnlyList<(string key, string label)> hints, string label)
    {
        foreach (var (_, l) in hints) if (l == label) return true;
        return false;
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
