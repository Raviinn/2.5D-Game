# UI Restyle — Ghost of Tsushima Reference (Design)

**Date:** 2026-10-07 · **Status:** approved in chat, awaiting spec review · **Milestone:** 21 (UI restyle)

## Goal

Restyle every prototype (IMGUI) screen after Ghost of Tsushima's UI.
- **Layout:** a tabbed in-game menu, tile cards, a tip strip and key hints.
- **Colour:** parchment, ink, vermilion and gold.
- **Refinement:** brush-stroke shapes, spaced capitals, generous whitespace.

The game stays medieval fantasy; only the presentation borrows Tsushima's language.

## Decisions (made in chat)

| Question | Choice |
|---|---|
| Menu base | **Parchment and ink** (light) for in-game menus. The title screen stays dark and stormy. The HUD stays dark and see-through. |
| Navigation | **One tabbed menu** (Map · Journal · Bag · Character · Options). Shop, dialogue and the contract board stay separate pop-ups. |
| Lettering | **Free fonts** (SIL Open Font License): Cormorant Garamond for titles and tab names, Alegreya Sans for body text. Downloaded from the Google Fonts GitHub repo into `Assets/_Project/Resources/UI/Fonts` (Resources, so builds include them). |
| Approach | **Restyle the existing IMGUI system** (UITheme and the screens). The UI Toolkit port stays a later milestone. |

## Visual language

### Palette (UITheme tokens)

| Token | Value | Job |
|---|---|---|
| Parchment | `#E9E6DF` | Menu background, with a soft ink-wash fade toward the edges (generated texture) |
| Ink | `#141414` | Top bar, brush strips, buttons, details panels, body text on parchment |
| Paper | `#F4F2EE` | Cards, slots, rows |
| Vermilion | `#D9534F` | The only selection colour: selected card / tab tile / slot frame, hovered brush button, speaker names |
| Gold | `#C9A86A` (`#8A6A2C` on parchment) | Small accents: requirements, currency, "Hold:"-style prefixes, learned skill rims |
| Off-white | `#F2EFE8` | Text on ink |
| Muted | `#55514A` on parchment, `#BDB7AA` on ink | Secondary text |

Good, bad and info stay as status colours, retuned to read on both parchment and ink. The HUD keeps the health, stamina and poise bar colours. Health moves to vermilion.

### Shapes (generated in code; no art files)

- **Ink strip:** a dark band with ragged, dry-brush ends. Used for buttons, the footer tip and the dialogue band.
- **Red brush block:** a vermilion fill with a slightly ragged edge, for selected cards and tiles.
- **White brush swash:** the highlight behind the selected title-screen item.
- **Diamond node:** a skill. Gold rim when learned, grey when locked, vermilion when selected.
- **Paper card / slot:** flat paper with a hairline ink border.

### Typography

- Cormorant Garamond: titles, tab names and tile labels in spaced capitals.
- Alegreya Sans: everything else.
- The current type scale stays (the 1080-unit virtual screen); only the faces and letter spacing change.
- If a font is missing at runtime, the screens fall back to Unity's default font.

### Shared frame for every tab

- **Top bar:** an ink strip.
  - Q on the left and E on the right.
  - Tab names in capitals; the selected tab is a parchment-filled block with ink text.
  - Level and gold on the far right.
- **Footer:** an ink tip strip at the bottom left, with one contextual tip.
- **Key hints:** bottom right, e.g. "[F] Select · [Esc] Back".

## Screens

### 1. Title screen (MainMenu)

- **Backdrop:** a stormy grey-blue sky with drifting cloud bands. Falling ash or petals drawn as small particles. The silhouetted hills are kept.
- **Title:** "BEAST" in widely spaced Cormorant capitals.
- **Menu:** a plain left-aligned list (Continue · Load Game · New Game · Settings · Quit). The highlighted item sits on the white brush swash.
- **Panels:** the slot picker, the character creator and the settings panel become parchment cards over the storm.

### 2. Game menu shell (new `GameMenu`)

- One component owns open/closed state, the active tab and the shared frame.
- The game stays paused while it's open (the `InGameMenu` state, as now).
- **Opening keys:** M → Map, J → Journal, Tab or I → Bag, C → Character, Esc → Options.
- **Inside the menu:** Q / E move between tabs; Esc closes the menu from any tab, unless a sub-view or confirm is open, in which case Esc backs out of that first.
- **Tabs:** each tab is drawn by its screen class (`Draw(Rect content)`). The existing screens become tab content, not windows.

| Tab | Content |
|---|---|
| **Map** | The large map render, framed on parchment, plus a legend. The tracked quest stays marked. (M no longer toggles an unpaused large overlay; the minimap HUD is unchanged.) |
| **Journal** | Filter cards on the left: Story, Side, Contracts, Completed. Quest list in the middle. Ink details panel on the right with Track and Abandon brush buttons (the Abandon confirm is kept). Town standing card at the bottom. |
| **Bag** | Category cards on the left: All, Food, Materials, Seeds & crops, Gear. Paper slot grid with a red frame on the selected slot. Ink details panel on the right with Use and Equip brush buttons. Right-click use / equip is kept. |
| **Character** | Cards on the left: Combat, Farming and Gear.<br>• Combat and Farming show the skill tree as diamond nodes joined by lines. Requirements show in gold, and the Learn button sits in the details panel.<br>• Gear shows the character's sprite large in the middle (a front idle frame from PlayerAppearance), with equipment slots around it and stats in the ink panel. |
| **Options** | Tiles: Controls, Display, Audio, Interface. Brush buttons: Resume, Save game, Load last save, Quit to title, Quit to desktop. The quit buttons keep their confirm.<br>• A tile opens that category's settings rows inline on the parchment, reusing SettingsPanel's row logic with no separate window.<br>• Controls shows the key layout. |

### 3. Pop-ups (stay separate)

- **Shop and Contract board:** a parchment card with an ink title strip and red selection. Behaviour is unchanged.
- **Dialogue:** an ink band at the bottom. The speaker name is in vermilion capitals; choices are paper rows, the chosen one red. Accept / Decline behaviour is unchanged.

### 4. HUD (GameHud, QuestTracker, Minimap frame, notices)

- The layout is unchanged; panels stay dark and see-through.
- New fonts, off-white text, and a thinner vermilion health bar.
- Key hints use the new key-cap style.

## Architecture

- **UITheme:**
  - New palette constants.
  - Brush textures generated once on build, with a fixed random seed so they look the same every time.
  - Font loading from `Resources/UI/Fonts`, with a fallback to the default font.
  - New widgets: `BrushButton`, `Card` (selected = red block), `TopBar`, `FooterTip`, `KeyHints`, `DiamondNode`.
  - Existing widget names keep working, so screens migrate one at a time.
- **GameMenu (new):** the tab shell. It owns its input actions: the existing open actions, plus Q/E handled while the menu is open.
- **The screens:** InventoryScreen, CharacterScreen, QuestJournal and the Minimap large view keep their logic. They gain a `DrawTab(Rect)` entry point and stop opening themselves. PauseMenu's actions move into the Options tab.
- **Fonts:** the `.ttf` files live under `Resources/UI/Fonts` so builds include them. Each font folder carries a credits note (`OFL.txt`).

## Build steps (each tested and shown before the next)

1. **Theme, fonts and title screen.**
2. **The menu shell and Options tab** (including Settings and Controls).
3. **Bag, Character, Journal and Map tabs.**
4. **Pop-ups and HUD.**

## Testing

- **Automated:**
  - The existing harnesses keep passing: QA, UI, Menu and Settings use the open/close paths, which are updated alongside.
  - A new UI harness pass screenshots every tab, every Options tile and each pop-up at 1600×900 and 1920×1080.
- **Manual:** a new checklist section 13q (UI restyle).

## Out of scope

- The UI Toolkit port.
- Controller navigation of menus (planned with the UI Toolkit port).
- Real painted ink-wash art for cards: generated textures until real art exists.
