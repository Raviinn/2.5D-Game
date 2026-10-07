# UI Restyle (Ghost of Tsushima reference) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restyle Beast's IMGUI UI to the parchment-and-ink language in `Docs/superpowers/specs/2026-10-07-tsushima-ui-design.md`, in four tested steps.

**Architecture:**
- `UITheme` gains a second, light "ink and parchment" widget set *next to* the current dark one: tokens, generated brush textures, fonts and widgets. Screens move over one step at a time, so nothing is unreadable halfway.
- A new `GameMenu` shell (step 2) hosts the tabs.
- The existing screens keep their logic and gain `DrawTab(Rect)` entry points (step 3).

**Tech Stack:** Unity 6000.0.34f1, IMGUI, C# 9. Verification uses the batch-build harness workflow: a scratch copy subst-mapped to `Q:`, `BuildHarness`, `run_all_harnesses.ps1`.

**Detail policy:** Step 1 is specified to the function level here. Steps 2–4 each get their own detailed task list, appended to this file when the previous step has landed, because they build on step 1's widget API. Their scope and acceptance criteria are fixed below.

---

## File map

| File | Step | Responsibility |
|---|---|---|
| `Assets/_Project/Resources/UI/Fonts/AlegreyaSans-{Regular,Medium,Bold}.ttf` + `OFL.txt` | 1 | Body font (Google Fonts repo, OFL) |
| `Assets/_Project/Resources/UI/Fonts/CormorantGaramond-SemiBold.ttf` + `OFL.txt` | 1 | Display font (CatharsisFonts/Cormorant, OFL) |
| `Gameplay/UI/InkTheme.cs` (new, `partial class UITheme`) | 1 | Ink and parchment tokens, brush textures, fonts and widgets. Kept out of UITheme.cs, which is already 660 lines. |
| `Gameplay/UI/UITheme.cs` | 1 | Becomes `partial`. `Build()` calls `BuildInk()`. Body styles switch to the new fonts. |
| `Gameplay/UI/MainMenu.cs` | 1 | Storm backdrop, left list with swash, parchment slot picker and creator |
| `Docs/QA/harnesses/StyleHarness.cs` (new, `-styleharness`) | 1→4 | Checks fonts and textures, screenshots each restyled screen; grows each step |
| `Gameplay/UI/GameMenu.cs` (new) | 2 | Tab shell: open keys, Q/E, Esc, top bar, footer, key hints |
| `Gameplay/UI/PauseMenu.cs`, `SettingsPanel.cs` | 2 | Become the Options tab and its inline categories |
| `Gameplay/UI/InventoryScreen.cs`, `CharacterScreen.cs`, `QuestJournal.cs`, `Minimap.cs` | 3 | Tab content via `DrawTab(Rect)`; stop opening themselves |
| `Gameplay/UI/ShopScreen.cs`, `ContractBoardScreen.cs`, `DialogueBox.cs`, `GameHud.cs`, `QuestTracker.cs` | 4 | Pop-ups and HUD restyle |
| `Docs/Test_Checklist.md`, `Docs/Game_Design_Document.md` | each | Checklist 13q; GDD §20 and change log |

---

## Step 1 — Theme, fonts and title screen

### Task 1.1: Fonts

- [ ] Download into `Assets/_Project/Resources/UI/Fonts/`:
  - `AlegreyaSans-Regular.ttf`, `AlegreyaSans-Medium.ttf`, `AlegreyaSans-Bold.ttf`, and its `OFL.txt` saved as `OFL-AlegreyaSans.txt`, all from `raw.githubusercontent.com/google/fonts/main/ofl/alegreyasans/`.
  - `CormorantGaramond-SemiBold.ttf` from `raw.githubusercontent.com/CatharsisFonts/Cormorant/master/fonts/ttf/`.
  - `OFL.txt` from `google/fonts/main/ofl/cormorantgaramond/`, saved as `OFL-CormorantGaramond.txt`.
- [ ] Verify: each `.ttf` starts with the TrueType magic `00 01 00 00` and is over 200 KB.

### Task 1.2: `InkTheme.cs` — tokens and fonts

- [ ] `UITheme.cs`: `public static class UITheme` → `public static partial class UITheme`; at the end of `Build()` call `BuildInk();`.
- [ ] New `InkTheme.cs` (`public static partial class UITheme`), with tokens:
  - Colours: `Parchment #E9E6DF`, `Ink #141414`, `Paper #F4F2EE`, `Vermilion #D9534F`, `InkGold #C9A86A`, `InkGoldDark #8A6A2C`, `OffWhite #F2EFE8`, `MutedOnPaper #55514A`, `MutedOnInk #BDB7AA`.
  - Hex strings for rich text.
- [ ] Fonts:
  - `static Font bodyFont, bodyBoldFont, displayFont` loaded with `Resources.Load<Font>("UI/Fonts/…")`.
  - `public static bool FontsLoaded`.
  - Fallback: the current `GUI.skin.label.font`.
- [ ] Styles (all rich text, no padding or margin):
  - `DisplayTitle` (display 64, off-white, centred).
  - `TabLabel` (display 20, centred).
  - `MenuItem` (display 28, middle-left).
  - `InkHeader` (display 24, ink).
  - `PaperBody` (body 17, ink, wrap).
  - `PaperMuted` (body 15, muted on paper).
  - `InkBody` (body 17, off-white, wrap).
  - `InkSmall` (body 15, off-white, middle-left).
  - `BrushButtonStyle` (display 18, off-white, centred).
- [ ] `public static string Spaced(string s)`: upper-cases and puts a thin space (U+2009) between letters, and three between words. This is the letter-spacing for display capitals.
- [ ] Existing body styles (`Body`, `Small`, `BodyMiddle`, …) use `bodyFont`; bold ones use `bodyBoldFont` with `FontStyle.Normal`. Titles and headers use `displayFont`. This gives the whole game the new lettering in step 1, HUD included.

### Task 1.3: Brush textures

All generated once in `BuildInk()`, with value noise from a fixed seed (`new System.Random(7)`), so they look the same every time.

- [ ] Textures:
  - `parchmentTex` (256²): parchment fill. It darkens toward the edges, a radial ink wash from 0 at the centre to 0.18 at the corners. Fine fibre noise is ±2%.
  - `inkStripTex` (256×48, sliced 28 px left/right, 0 top/bottom): ink body. The ends are ragged: per-row end offset from 1D noise, about 0–22 px, with dry-brush gaps where the noise is above 0.8.
  - `vermilionStripTex`: the same shape in vermilion (hovered brush buttons).
  - `redBlockTex` (128², sliced 12): a vermilion fill whose edges wobble ±3 px.
  - `swashTex` (512×64, sliced 40 left/90 right): an off-white brush swash. Full height on the left, tapering and breaking up toward the right end.
  - `paperCardTex` (64², sliced 8): a paper fill with a 1 px `#C9C4BA` hairline.
  - `inkPanelTex` (64², sliced 8): ink at 96% opacity.

### Task 1.4: Widgets

- [ ] `void ParchmentBackground(Rect r)`: draws `parchmentTex` stretched.
- [ ] `void PaperCard(Rect r, bool selected)`: `redBlockTex` when selected, else `paperCardTex`.
- [ ] `void InkPanel(Rect r)` and `void InkStrip(Rect r)`.
- [ ] `bool BrushButton(Rect r, string text, bool enabled = true)`:
  - Ink strip, or the vermilion strip on hover; label `Spaced(text)` in `BrushButtonStyle`.
  - Disabled: 45% alpha and no click.
  - Raises `Clicked`, so the click sound plays.
- [ ] `void Swash(Rect r)`: draws `swashTex`.
- [ ] `float KeyHints(float rightX, float y, params (string key, string label)[] hints)`:
  - Right-aligned. The key in `[ ]`, ink text on parchment.
  - Returns the left x.
- [ ] `void FooterTip(Rect r, string richText)`: an ink strip with `InkSmall` text, 18 px padding.
- [ ] `Rect ParchmentWindow(float w, float h, string title, string subtitle = null)`:
  - A centred card: parchment fill and a 1 px ink border.
  - Title in `InkHeader` spaced capitals, subtitle right-aligned in `PaperMuted`.
  - An ink rule under the title.
  - Returns the content rect: 24 px padding, starting at y+64.

### Task 1.5: Title screen (`MainMenu.cs`)

- [ ] Backdrop replaces the dusk sky:
  - A vertical gradient `#1C2025` (top) → `#3A4047` (horizon) → `#1A1C1F` (bottom).
  - Three cloud bands: wide soft ellipses in `#4A5058` at 25–40% alpha, made once as a 512×128 texture from noise, drifting at different speeds.
  - Falling ash: 140 particles of 2–3 px, off-white at 30–70% alpha. They fall slowly with a sideways sway and wrap around the screen, positions from a seeded random.
  - The hills stay, in near-black storm tones.
  - The moon is removed.
- [ ] Title: `Spaced("Beast")` in `DisplayTitle`, at the upper right (x = 58% of width, y = 14%). Subtitle "A tale of the Hollows" in `InkSmall` below it at 70% alpha.
- [ ] Home menu:
  - A left-aligned list at x = 7% of width, y = 46% of height.
  - Items in `MenuItem` spaced capitals, 54 px apart: Continue (if any save), New Game, Load Game (when saves exist; otherwise drawn at 35% alpha and not clickable), Settings, Quit.
  - The hovered item, or the first item when nothing is hovered, draws `Swash` behind it (width 420, height 52, starting 18 px left of the text), with its text in ink. Other items are off-white.
  - The Continue detail line (slot, day and play time) sits under the list in `InkSmall` at 70% alpha.
- [ ] Slot picker: `ParchmentWindow(760, …, "New Game" / "Load Game", subtitle)`.
  - Slot cards are `PaperCard`, with text in `PaperBody` and `PaperMuted`.
  - Buttons are `BrushButton` (Start here, Load, Delete, Overwrite, Cancel, Back).
  - Confirm text is in vermilion.
- [ ] Character creator: `ParchmentWindow(980, 600, "Create your hero")`.
  - The preview stage is an `InkPanel`, with turn buttons as `BrushButton` "‹" / "›".
  - Labels are in `PaperBody`.
  - The swatch frame is ink, or vermilion when selected.
  - Steppers keep their logic but draw as paper cards with brush arrows.
  - The weapon options are paper cards, the selected one a red block.
  - Back, Randomise and "Begin your journey" are brush buttons.
- [ ] The Settings panel keeps its current look until step 2. It's still drawn over the storm.
- [ ] Footer: the version line in `InkSmall` at 50% alpha, bottom left; `KeyHints` ("Esc" Back) at the bottom right on the slot and creator pages.

### Task 1.6: Verification

- [ ] Compile check with the scratchpad `build.ps1`: zero errors.
- [ ] New `Docs/QA/harnesses/StyleHarness.cs` (`-styleharness`, tag `[StyleTest]`):
  - Check `UITheme.FontsLoaded`.
  - Check that the ink textures exist and are 2D textures of the expected size.
  - Then screenshot: the title with nothing hovered; the title with the swash on New Game (simulated hover by setting `hoverIndex` through reflection); the New Game slot picker; the creator; Load Game with one save; the in-world HUD, to see the font change.
  - Add it to `run_all_harnesses.ps1`.
- [ ] The full suite stays green: QA, UI, Rep, Climb, AI, Sky, Night, Menu, Settings, Look and Style.
- [ ] Look at every screenshot. Text must not clip or overflow anywhere, the HUD included: the new body font has different widths.
- [ ] Docs: checklist section 13q, step 1 part (title screen); GDD §20 note and change log v1.13.

---

## Step 2 — Menu shell and Options tab (scope fixed; detailed tasks appended after step 1)

- `GameMenu`:
  - Opens on M / J / Tab / I / C / Esc while Playing, at that key's tab. While it's open, Q/E cycle tabs and Esc closes it (or backs out of a sub-view first).
  - It draws the top bar, footer and key hints, and owns the `InGameMenu` state.
- Options tab: tiles (Controls, Display, Audio, Interface) plus the brush-button list (Resume, Save game, Load last save, Quit to title, Quit to desktop, with confirms).
  - Choosing a tile shows SettingsPanel's rows inline; Controls shows the key layout.
- The SettingsPanel restyle also applies on the title screen.
- Until step 3, the other tabs show the existing screens drawn inside the content rect.
- **Acceptance:** Esc → Options in the world; every key opens its tab; Q/E cycle; the Menu and Settings harnesses are updated and green; StyleHarness screenshots every tile.

## Step 3 — Bag, Character, Journal and Map tabs

- Each screen's logic is kept, with the layouts from the spec's tab table.
- The Map tab shows the map camera's render texture while paused.
- **Acceptance:** the QA, UI and Look harnesses are updated for the tab paths and green; StyleHarness screenshots each tab (Character with each card).

## Step 4 — Pop-ups and HUD

- Shop, Contract board and Dialogue as parchment cards / ink band.
- HUD: new fonts, thinner vermilion health bar, new key-cap style.
- **Acceptance:** all harnesses green; screenshots of each pop-up and of the HUD by day and night; checklist 13q complete.

---

## Step 2 — detailed tasks (written after step 1 landed)

**Findings:**
- `SaveService.Save/Load` only run in `Playing` or `Paused`. So the **Options tab uses `Paused`**, the state Esc already sets, and the other tabs use `InGameMenu`.
- The UI action map's `CloseMenu` (Tab / I / C / J) closes any `InGameMenu`. While the shell is open it sets `BlockHotkeyClose` every frame and reads those keys itself: another tab's key switches to that tab; the current tab's key closes the menu.

### Task 2.1: shared pieces
- [ ] `IGameMenuTab` (Gameplay/UI):
  - `bool CanOpen`
  - `void OnTabOpened()`
  - `void DrawTab(Rect content)`
  - `string FooterTip`
  - `(string key, string label)[] KeyHints`
  - `bool HasSubView` (Esc backs out of it first)
  - `void CloseSubView()`
- [ ] `UITheme.WindowIn(Rect bounds, w, h, title, subtitle, footer)`: the old dark frame centred in `bounds`, with no backdrop or ✕. Screens use it until step 3.
- [ ] InkTheme: `PaperSlider` (ink track, vermilion fill, ink thumb) and `PaperToggle` (paper box, vermilion check).

### Task 2.2: `GameMenu` (new MonoBehaviour, added by UIBootstrap)
- [ ] Tabs, in order: Map (Minimap), Journal (QuestJournal), Bag (InventoryScreen), Character (CharacterScreen), Options (PauseMenu). Each is found with `FindFirstObjectByType`.
- [ ] While Playing, poll the Player map actions: Inventory → Bag, Character, Journal, Map. On a press: `Open(tab)`.
- [ ] `GameStateChangedEvent` → Paused while closed → `Open(Options)`. Paused or InGameMenu while open → stay open. Anything else → close.
- [ ] `Open(tab)`: sets the active tab, then the state (Paused for Options, InGameMenu otherwise), then calls `OnTabOpened`.
- [ ] While open (Update):
  - Set `BlockHotkeyClose` every frame.
  - Keyboard Q / E and gamepad shoulders cycle tabs.
  - Tab / I / C / J / M jump to that tab, or close if it's the current one.
  - Set `HoldPause` while the tab has a sub-view, so Esc backs out of it first (IMGUI KeyDown Escape → `CloseSubView()`).
- [ ] OnGUI (depth −3):
  - Full-screen parchment.
  - An ink top bar, 64 high: Q chip, the tabs (active = parchment block with ink text, hovered = vermilion underline, click to switch), E chip, and "Renown N · ● gold" on the right.
  - The content rect.
  - `FooterTip` at the bottom left.
  - `KeyHints` at the bottom right: the tab's own hints, plus "[Q / E] Switch" and "[Esc] Close".
- [ ] Public: `IsOpen`, `ActiveTab`, `Open(GameMenuTab)`, `Close()`. Tests use these.

### Task 2.3: tab screens
- [ ] InventoryScreen, CharacterScreen and QuestJournal:
  - Drop their own open-key polling, `isOpen` and self-drawing.
  - `DrawTab(content)` draws their current layout through `WindowIn`.
- [ ] Minimap:
  - M no longer toggles an unpaused overlay.
  - The Map tab draws the large map with its legend inside the content rect, and the map camera renders while the Map tab is showing.
  - The corner minimap is unchanged.

### Task 2.4: Options tab (PauseMenu)
- [ ] Four tiles: Controls, Display, Audio, Interface. They're paper cards with a spaced-capitals label; hovered or selected = red block.
- [ ] Brush-button list:
  - Resume.
  - Save game, with the result message.
  - Load last save, with the last-saved line.
  - Quit to title / Quit to desktop, with an inline vermilion confirm.
- [ ] A tile opens its sub-view inline:
  - Display / Audio / Interface draw `SettingsPanel.DrawCategory`.
  - Controls shows the sensitivity rows plus the key list on paper.
  - "‹ Back" and Esc return to the tiles.

### Task 2.5: SettingsPanel restyle
- [ ] `public void DrawCategory(Rect area, int category)` with paper rows:
  - Labels in `PaperBody`.
  - `PaperSlider`, `PaperToggle`, `PaperOptions` and `PaperStepper` as the controls.
  - Values in gold-dark.
  - The keep/revert banner as an ink strip.
- [ ] `Draw()`, the standalone title-screen window: `ParchmentWindow` with `PaperOptions` tabs, `DrawCategory`, and a footer with Reset to defaults (+ confirm) and Back as brush buttons.

### Task 2.6: tests and docs
- [ ] Harnesses switch from `isOpen` reflection and PauseMenu fields to `GameMenu.Open(...)` and the Options-tab API (UI, QA, Rep, Look, Climb, Settings, Menu).
- [ ] StyleHarness step 2 screenshots: each tab, each Options tile, the title-screen Settings window. Checks:
  - Esc → Options (Paused).
  - C → Character (InGameMenu).
  - E cycles.
  - The active tab's key closes the menu.
  - Esc from a sub-view backs out, then closes.
- [ ] Checklist 13q step 2 section; GDD change log v1.14.

---

## Step 3 — done (2026-10-08)

- **Bag, Character and Journal:** rewritten on the ink widgets.
  - New InkTheme helpers: `ThinBar`, `Line`, `DiamondNode`, `InkTooltip`, `CountChip`, `PaperSlot`, `FilterCard`.
  - Paper and ink variants of the good / info / quest-type colours.
- **HarnessFocus:** a new test-only script that keeps the virtual keyboard working while the player window is unfocused.

## Step 4 — detailed tasks

- [ ] **Shared HUD pieces:**
  - The HUD panel and pill textures become see-through ink with a faint light hairline (no brown or gold).
  - `KeyHint` key caps become ink chips with off-white letters.
  - `KeyStyle` is off-white.
- [ ] **ShopScreen:** `ParchmentWindow`.
  - Greeting in italic muted text.
  - Buy and Sell columns of paper rows: icon, name, price in gold-dark (vermilion when you can't afford it), stock or market.
  - Brush buttons (Buy, ×5, Sell, All). The trade message in the footer. `[Esc] Leave` hint.
- [ ] **ContractBoardScreen:** `ParchmentWindow`.
  - Contracts as paper cards; the tracked one has a vermilion frame.
  - A status tag in spaced capitals (available / in progress / ready / done / locked), the summary, objectives and reward.
  - A thin progress bar, plus brush buttons for Accept / Turn in / Track.
- [ ] **DialogueBox:**
  - A full-width ink band at the bottom, its ragged ends off-screen.
  - The portrait framed in paper. The speaker name in spaced vermilion capitals; off-white text, narration italic and muted.
  - Choices as paper rows with an ink number chip; the selected one is a red block.
  - An off-white "[Space] Continue" hint.
- [ ] **GameHud:**
  - Vitals: a thin vermilion health bar on an ink track with the numbers beside it, thin stamina and poise bars, the class and style line in off-white.
  - Action slots: ink cards and key caps.
  - The prompt and climb hints on an ink strip.
  - The clock on an ink panel with a gold coin.
  - Notes on ink pills with a coloured mark.
  - Defeat and sleep fades in spaced display capitals.
- [ ] **QuestTracker:**
  - The panel on ink, using the on-ink quest colours.
  - The big banner on an ink band, its title in spaced display capitals with a thin line in the banner colour.
- [ ] **Minimap:** the corner frame is a thin ink frame instead of the brown panel.
- [ ] **StyleHarness:** shop, contract board, dialogue line, dialogue choices, the HUD by day and by night, and a banner.
- [ ] **Docs:** checklist 13q step 4; GDD change log v1.16; mark the plan complete.

## Status: complete (2026-10-08)

All four steps are done and verified by the harness suite (StyleHarness screenshots every restyled screen). Follow-ups for later:
- UI Toolkit port with controller navigation.
- Painted art for cards and tiles.
