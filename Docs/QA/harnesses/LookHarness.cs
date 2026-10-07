using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Beast.Core;
using Beast.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Test-only: Milestones 18-20. Item icons and dropped-item cards; the character creator, the look in the world,
/// the sword &amp; shield / greatsword sheets and saving the look; sounds, ambience and the volume settings.
/// Runs only with -lookharness.
/// </summary>
public sealed class LookHarness : MonoBehaviour
{
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    string dir;
    int failures;
    GameStateService state;
    SaveService save;
    SettingsService settings;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-lookharness") < 0) return;
        var go = new GameObject("LookHarness");
        DontDestroyOnLoad(go);
        go.AddComponent<LookHarness>();
    }

    void Check(bool ok, string what)
    {
        if (!ok) failures++;
        Debug.Log($"[LookTest] {(ok ? "PASS" : "FAIL")} {what}");
    }

    IEnumerator Start()
    {
        var args = Environment.GetCommandLineArgs();
        dir = args[Array.IndexOf(args, "-shots") + 1];
        Directory.CreateDirectory(dir);
        state = Services.Get<GameStateService>();
        save = Services.Get<SaveService>();
        settings = Services.Get<SettingsService>();
        for (int i = 0; i < 3; i++)
            foreach (var path in new[] { SaveService.GetSlotPath(i), SaveService.GetSlotPath(i) + ".bak" })
                if (File.Exists(path)) File.Delete(path);
        settings.ResetToDefaults();

        yield return Until(() => SceneManager.GetActiveScene().name == "MainMenu" && state.Current == GameState.MainMenu, 30f);
        yield return Wait(3f);
        var menu = FindFirstObjectByType<MainMenu>();

        // ================= Sound: the library and the title screen =================
        var audio = GameAudio.Instance;
        Check(audio != null && audio.Library != null, "GameAudio exists and found Resources/SoundLibrary");
        var lib = audio.Library;
        var missing = typeof(SoundLibrary).GetFields().Where(f =>
            f.FieldType == typeof(AudioClip[]) ? ((AudioClip[])f.GetValue(lib))?.Any(c => c != null) != true : f.GetValue(lib) == null).Select(f => f.Name).ToArray();
        Check(missing.Length == 0, $"every sound library entry has a clip (missing: {string.Join(", ", missing)})");
        Check(lib.AmbienceDay != null && lib.AmbienceDay.length > 10f, $"day ambience is a long loop ({lib.AmbienceDay?.length:0.0} s)");
        var menuLoop = (AudioSource)Field(audio, "menu");
        Check(menuLoop.isPlaying && menuLoop.volume > 0.05f, $"title screen plays its wind ambience (volume {menuLoop.volume:0.00})");
        Check(FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(l => l.isActiveAndEnabled) == 1, "exactly one audio listener on the title screen");

        int played = audio.SoundsPlayed;
        typeof(UITheme).GetMethod("Click", Any).Invoke(null, new object[] { true });
        Check(audio.SoundsPlayed == played + 1 && lib.UiClick.Contains(audio.LastClip), "a button click plays the click sound");

        // Settings → Audio tab
        var panel = (SettingsPanel)Field(menu, "settingsPanel");
        panel.Open();
        SetField(panel, "tab", Enum.ToObject(typeof(SettingsPanel).GetField("tab", Any).FieldType, 2));
        yield return Wait(0.5f);
        yield return Shot("01_settings_audio");
        panel.Close();

        // ================= Character creator =================
        menu.OpenCreator(0);
        yield return Wait(0.5f);
        var look = menu.CreatorLook;
        look.hairStyle = (int)HairStyle.Long;
        look.hairColor = 5;  // blonde
        look.skinTone = 4;   // brown
        look.outfit = 2;     // wine coat
        yield return Wait(0.6f);
        yield return Shot("02_creator_long_blonde");
        Check(Field(menu, "previewSheet") is Texture2D, "the creator draws a live preview");
        SetField(menu, "previewDirection", 2);
        SetField(menu, "previewWeapon", WeaponLook.Greatsword);
        yield return Wait(0.5f);
        yield return Shot("03_creator_side_greatsword");
        SetField(menu, "previewDirection", 0);
        SetField(menu, "previewWeapon", WeaponLook.SwordAndShield);

        // The builder really draws the choices.
        var a = CharacterSpriteBuilder.Draw(look.ToPalette(WeaponLook.SwordAndShield));
        var other = look.Clone();
        other.hairStyle = (int)HairStyle.Shaved;
        var b = CharacterSpriteBuilder.Draw(other.ToPalette(WeaponLook.SwordAndShield));
        Check(Has(a, CharacterAppearance.SkinTones[4]) && Has(a, CharacterAppearance.HairColors[5]) && Has(a, CharacterAppearance.Outfits[2].Body),
            "the sheet uses the chosen skin, hair colour and outfit");
        Check(!Has(b, CharacterAppearance.HairColors[5]), "shaved: no hair colour on the sheet");
        var greatsword = CharacterSpriteBuilder.Draw(look.ToPalette(WeaponLook.Greatsword));
        Check(Has(a, CharacterAppearance.Outfits[2].ShieldFace) && !Has(greatsword, CharacterAppearance.Outfits[2].ShieldFace),
            "sword & shield sheet has a shield; greatsword sheet has none");
        Check(Count(greatsword, new Color32(215, 220, 230, 255)) > Count(a, new Color32(215, 220, 230, 255)) * 1.3f,
            $"greatsword sheet has a much bigger blade ({Count(greatsword, new Color32(215, 220, 230, 255))} vs {Count(a, new Color32(215, 220, 230, 255))} steel pixels)");
        foreach (HairStyle style in Enum.GetValues(typeof(HairStyle)))
        {
            var each = look.Clone();
            each.hairStyle = (int)style;
            var tex = CharacterSpriteBuilder.Build(each.ToPalette(WeaponLook.SwordAndShield));
            File.WriteAllBytes(Path.Combine(dir, $"sheet_hair_{style}.png"), tex.EncodeToPNG());
            Destroy(tex);
        }

        var chosen = look.Clone();
        menu.StartNewGame(0, look);
        yield return Until(InWorld, 40f);
        Check(InWorld(), "Begin your journey starts the world");
        yield return Wait(3f);

        // ================= The look in the world =================
        var player = GameObject.FindWithTag("Player");
        var appearance = player.GetComponent<PlayerAppearance>();
        Check(appearance != null, "the player has PlayerAppearance (Milestone 19 setup)");
        Check(appearance.Appearance.SameAs(chosen), "the player wears the look picked in the creator");
        var sprite = player.GetComponentInChildren<DirectionalSpriteRenderer>();
        var shown = (Texture2D)Field(sprite, "textureOverride");
        Check(shown != null && shown == appearance.CurrentSheet, "the sprite draws the generated sheet");
        var pixels = appearance.CurrentSheet.GetPixels32();
        Check(Has(pixels, CharacterAppearance.SkinTones[4]) && Has(pixels, CharacterAppearance.HairColors[5]), "the in-game sheet has the chosen colours");

        var equipment = player.GetComponent<PlayerEquipment>();
        var combat = player.GetComponent<PlayerCombat>();
        Debug.Log($"[LookTest] INFO weapon {equipment.ActiveWeapon?.DisplayName}, style {combat.Moveset?.DisplayName}, look {appearance.ShownLook}");
        Check(appearance.ShownLook == combat.Moveset.ResolvedLook, $"shown look matches the style in use ({combat.Moveset.DisplayName} → {appearance.ShownLook})");

        var cam = Camera.main.transform;
        yield return Shot("04_world_start");
        File.WriteAllBytes(Path.Combine(dir, $"sheet_world_{appearance.ShownLook}.png"), appearance.CurrentSheet.EncodeToPNG());

        // Equip a greatsword, then a sword & shield.
        var allWeapons = Resources.FindObjectsOfTypeAll<WeaponData>();
        var gs = allWeapons.FirstOrDefault(w => w.Moveset != null && w.Moveset.ResolvedLook == WeaponLook.Greatsword);
        var ss = allWeapons.FirstOrDefault(w => w.Moveset != null && w.Moveset.ResolvedLook == WeaponLook.SwordAndShield);
        Check(gs != null && ss != null, $"found a greatsword ({gs?.DisplayName}) and a sword & shield ({ss?.DisplayName})");
        Check(gs.Moveset.Look == WeaponLook.Greatsword, "Milestone 19 setup marked the greatsword style");
        yield return EquipActive(equipment, gs);
        Check(appearance.ShownLook == WeaponLook.Greatsword, $"greatsword equipped → greatsword look ({appearance.ShownLook})");
        Check(!Has(appearance.CurrentSheet.GetPixels32(), CharacterAppearance.Outfits[2].ShieldFace), "greatsword look: no shield");
        File.WriteAllBytes(Path.Combine(dir, "sheet_world_Greatsword.png"), appearance.CurrentSheet.EncodeToPNG());
        yield return Shot("05_world_greatsword");
        yield return EquipActive(equipment, ss);
        Check(appearance.ShownLook == WeaponLook.SwordAndShield, $"sword & shield equipped → shield look ({appearance.ShownLook})");
        Check(Has(appearance.CurrentSheet.GetPixels32(), CharacterAppearance.Outfits[2].ShieldFace), "sword & shield look: shield drawn");

        // Saved with the game.
        Check(save.Save(), "save");
        var changed = chosen.Clone();
        changed.hairStyle = (int)HairStyle.Bun;
        changed.outfit = 0;
        appearance.SetAppearance(changed);
        Check(appearance.Appearance.SameAs(changed), "the look can change at runtime");
        bool loaded = false;
        LoadSlot(0, ok => loaded = ok);
        yield return Until(() => loaded && InWorld(), 40f);
        yield return Wait(2f);
        player = GameObject.FindWithTag("Player");
        appearance = player.GetComponent<PlayerAppearance>();
        Check(appearance.Appearance.SameAs(chosen), "loading the save brings the creator's look back");

        // ================= Item icons and dropped items =================
        var items = Resources.FindObjectsOfTypeAll<ItemData>();
        var noIcon = items.Where(i => i.Icon == null).Select(i => i.name).ToArray();
        Check(items.Length >= 15 && noIcon.Length == 0, $"all {items.Length} items have an icon (missing: {string.Join(", ", noIcon)})");
        var bread = items.First(i => i.name.Contains("Bread"));
        var drawn = items.Where(i => i.Icon != null).Select(i => i.Icon.texture).Distinct().Count();
        Check(drawn == items.Count(i => i.Icon != null), $"every item has its own icon picture ({drawn} textures)");

        Vector3 ahead = player.transform.position + Flat(cam.forward) * 4.5f;
        ItemPickup.SpawnItem(ahead, bread, 1, 0f);
        ItemPickup.SpawnItem(ahead + Flat(cam.right) * 1.2f, items.First(i => i.name.Contains("Greatsword")), 1, 0f);
        ItemPickup.SpawnGold(ahead - Flat(cam.right) * 1.2f, 5, 0f);
        yield return Wait(1f);
        var cards = FindObjectsByType<ItemPickup>(FindObjectsSortMode.None);
        Check(cards.Length >= 3, $"{cards.Length} pickups on the ground");
        Check(cards.All(p => p.GetComponentInChildren<MeshRenderer>().sharedMaterial.name.StartsWith("Pickup_Icon")), "dropped items show their icon card (not a cube)");
        var card = cards[0].GetComponentInChildren<MeshRenderer>().transform;
        Check(Vector3.Angle(Flat(card.forward), Flat(cam.forward)) < 5f, "icon cards turn to face the camera");
        yield return Shot("06_pickups");

        var inventory = player.GetComponent<Inventory>();
        inventory.Add(items.First(i => i.name.Contains("Turnip") && !i.name.Contains("Seeds")), 3);
        inventory.Add(items.First(i => i.name.Contains("IronHelm")), 1);
        inventory.Add(items.First(i => i.name.Contains("Draught")), 2);
        yield return Wait(0.3f);
        var gameMenu = FindFirstObjectByType<GameMenu>();
        gameMenu.Open(GameMenuTab.Bag);
        yield return Wait(0.4f);
        yield return Shot("07_inventory_icons");
        gameMenu.Close();
        yield return Wait(0.3f);

        // ================= Sounds in the world =================
        played = audio.SoundsPlayed;
        var combatant = player.GetComponent<Combatant>();
        EventBus<DamageDealtEvent>.Raise(new DamageDealtEvent(combatant, null, 5f, HitResult.Hit));
        Check(lib.Hit.Contains(audio.LastClip), "a hit plays a hit sound");
        EventBus<DamageDealtEvent>.Raise(new DamageDealtEvent(combatant, null, 5f, HitResult.Blocked));
        Check(lib.Block.Contains(audio.LastClip), "a block clangs");
        EventBus<DamageDealtEvent>.Raise(new DamageDealtEvent(combatant, null, 0f, HitResult.Parried));
        Check(lib.Parry.Contains(audio.LastClip), "a parry rings");
        EventBus<FarmActionEvent>.Raise(new FarmActionEvent(FarmAction.Water, player.transform.position));
        Check(lib.Water.Contains(audio.LastClip), "watering splashes");
        EventBus<ItemUsedEvent>.Raise(new ItemUsedEvent(bread));
        Check(lib.Eat.Contains(audio.LastClip), "eating bread crunches");
        EventBus<GoldChangedEvent>.Raise(new GoldChangedEvent(5, 100));
        Check(lib.Coins.Contains(audio.LastClip), "gold jingles");
        Check(audio.SoundsPlayed >= played + 6, $"{audio.SoundsPlayed - played} sounds played");

        int frames = 0;
        Action<DirectionalSpriteRenderer, CharacterAnim, int> counter = (r, anim, f) => frames++;
        DirectionalSpriteRenderer.FrameShown += counter;
        yield return Wait(1.5f);
        DirectionalSpriteRenderer.FrameShown -= counter;
        Check(frames > 5, $"characters report their animation frames ({frames} in 1.5 s)");
        typeof(GameAudio).GetMethod("OnFrameShown", Any).Invoke(audio, new object[] { sprite = player.GetComponentInChildren<DirectionalSpriteRenderer>(), CharacterAnim.Run, 0 });
        Check(lib.Footstep.Contains(audio.LastClip), "a run contact frame plays a footstep");
        yield return EquipActive(equipment, gs);
        typeof(GameAudio).GetMethod("OnFrameShown", Any).Invoke(audio, new object[] { sprite, CharacterAnim.Attack, sprite.Sheet.Find(CharacterAnim.Attack).StartupFrames });
        Check(lib.HeavySwing.Contains(audio.LastClip), "the greatsword's strike frame plays a heavy swing");

        // Ambience follows the clock.
        var day = (AudioSource)Field(audio, "day");
        var night = (AudioSource)Field(audio, "night");
        yield return Wait(3f);
        Check(day.isPlaying && day.volume > 0.2f && night.volume < 0.05f && !menuLoop.isPlaying, $"morning: day ambience (day {day.volume:0.00}, night {night.volume:0.00})");
        Services.Get<WorldClock>().AdvanceToHour(23);
        yield return Wait(4f);
        Check(night.volume > 0.2f && day.volume < 0.05f, $"23:00: night ambience (day {day.volume:0.00}, night {night.volume:0.00})");
        yield return Shot("08_night");

        // Volumes.
        settings.Current.ambienceVolume = 0f;
        settings.Current.effectsVolume = 0f;
        settings.Apply();
        yield return Wait(4f);
        Check(night.volume < 0.01f, $"Ambience slider at 0 silences the ambience ({night.volume:0.000})");
        EventBus<DamageDealtEvent>.Raise(new DamageDealtEvent(combatant, null, 5f, HitResult.Hit));
        yield return null;
        var pool = (AudioSource[])Field(audio, "pool");
        var hitSource = pool.First(s => s.clip == audio.LastClip && s.isPlaying);
        Check(hitSource.volume < 0.001f, $"Sound effects slider at 0 silences effects ({hitSource.volume:0.000})");
        settings.Current.masterVolume = 0.5f;
        settings.Apply();
        Check(Mathf.Approximately(AudioListener.volume, 0.5f), "master volume drives the listener");
        settings.ResetToDefaults();

        Debug.Log($"[LookTest] done: {failures} failure(s)");
        yield return Wait(1f);
        Application.Quit(failures == 0 ? 0 : 1);
    }

    IEnumerator EquipActive(PlayerEquipment equipment, WeaponData weapon)
    {
        if (equipment.ActiveWeapon != weapon)
        {
            equipment.Equip(weapon);
            if (equipment.ActiveWeapon != weapon) equipment.SwapWeapons();
        }
        yield return Wait(0.5f);
        Debug.Log($"[LookTest] INFO active weapon now {equipment.ActiveWeapon?.DisplayName}");
    }

    async void LoadSlot(int slot, Action<bool> done) => done(await save.LoadAsync(slot));

    static bool Has(Color32[] pixels, Color32 color) => Count(pixels, color) > 0;

    static int Count(Color32[] pixels, Color32 color)
    {
        int n = 0;
        foreach (var p in pixels)
            if (p.a == 255 && p.r == color.r && p.g == color.g && p.b == color.b) n++;
        return n;
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.normalized;
    }

    static bool InWorld() =>
        SceneManager.GetActiveScene().name == "World_Test" && GameObject.FindWithTag("Player") != null &&
        Services.Get<GameStateService>().Current == GameState.Playing;

    static object Field(object target, string name) => target.GetType().GetField(name, Any).GetValue(target);

    static void SetField(object target, string name, object value) => target.GetType().GetField(name, Any).SetValue(target, value);

    static IEnumerator Wait(float seconds) => new WaitForSecondsRealtime(seconds);

    static IEnumerator Until(Func<bool> condition, float timeout)
    {
        float end = Time.realtimeSinceStartup + timeout;
        while (!condition() && Time.realtimeSinceStartup < end) yield return null;
    }

    IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();
        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
        Destroy(tex);
        Debug.Log($"[LookTest] shot {name}");
    }
}
