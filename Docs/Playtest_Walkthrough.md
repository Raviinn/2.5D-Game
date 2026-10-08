# Beast — Full Playtest Walkthrough (state of 2026-10-09, Milestones 1 → 53)

One complete playthrough of the game in the order a player meets things, split into **sittings** of 20–40 minutes, so you can stop between them. Every step is **Action → Expected result**.

- Tick a step only if the expected result happens **and the Console shows no new red errors or yellow warnings**.
- If a step fails, write it in the **Results log** at the bottom (step number, what happened) and carry on if you can.
- For extra detail on any system, the matching section of [Test_Checklist.md](Test_Checklist.md) is named in brackets, e.g. *(TC §8)*.

---

## Keys at a glance

| Action | Keyboard / mouse | Gamepad |
|---|---|---|
| Move / camera | WASD / mouse | Left stick / right stick |
| Jump · climb · vault | Space | A |
| Sprint (hold) · dodge (tap) | Left Shift | B |
| Light attack (hold for heavy) | LMB | X (Y = heavy) |
| Block (tap just before a hit = parry) | RMB | LB |
| Lock on | Middle mouse | R3 |
| Swap weapon | X | D-pad → |
| Interact (talk, farm, sleep, workbench…) | F | D-pad ↑ |
| Switch seeds | V | D-pad ← |
| Eat / quick item | R | D-pad ↓ |
| Skills | E / Q | RB / RT |
| Track next quest | T | L3 |
| Bag · Character · Journal · Map | Tab or I · C · J · M | View (Back) |
| Game menu / pause | Esc | Start |
| Menus: move · choose · back | Arrow keys · Enter · Esc | D-pad · A · B |
| Menus: previous / next tab | Q / E | LB / RB |

**Dev keys** (Editor and Development builds only): F1 +1 day · F2 +1 hour · F3 hide the DEV line · F4 weather · F5 save · F9 load · F6 / F7 +100 Combat / Farming XP · F8 +50 standing.

---

## Sitting 0 — Preparation (10 min, once)

| # | Action | Expected |
|---|---|---|
| 0.1 | Open the project `Documents/Unity/2.5D-Game` in Unity. Wait for the import and compile to finish | No red errors in the Console |
| 0.2 | Console window → turn on **Error Pause**; press **Clear** | Play will pause on the first error |
| 0.3 | Close Play Mode. Open **File Explorer** (Windows key + E, or the yellow folder icon on the taskbar), click the address bar at the top, paste `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Beast\saves`, press Enter, then select everything in the folder (Ctrl+A) and delete it. If Windows says the folder doesn't exist, there are no saves yet: skip this step | A clean start: no old saves |
| 0.4 | Run each of **Beast → Setup → Run Milestone 24, 25, 26, 27, 28, 31, 33 Setup**, then **Run Milestone 23 Setup (Cleanup)**, then **37, 40, 43, 44, 45, 46, 47, 48, 49, 51, 53** | Each ends with a "… setup complete" line. No errors |
| 0.5 | Run **Milestone 26 Setup** a second time | It says things were kept or rebuilt; no second workbench in the Hierarchy |
| 0.6 | **Beast → Dialogue → Compile Ink Story** | "Compiled Assets/_Project/Dialogue/Main.json", no [Ink] errors |
| 0.7 | **Beast → Data → Rebuild Game Database** | "Rebuilt with N entries", no "Duplicate Id" errors |
| 0.8 | **File → Build Profiles** | Scene list: Bootstrap, MainMenu, World_Test (nothing else) |
| 0.9 | Open **World_Test** without pressing Play; look at the Scene view | Player, Bandits, the archer and the NPCs show as **sprites**, not just boxes, and turn to face the scene camera as you orbit *(TC §13z)* |
| 0.10 | Turn on your speakers / headphones at a normal level | — |
| 0.11 | Save the scene if Unity marks it changed (Ctrl+S) | — |

---

## Sitting 1 — Title screen, settings and a new character (20 min)

Open the **Bootstrap** scene and press **Play**.

### Title screen
| # | Action | Expected |
|---|---|---|
| 1.1 | Wait for the title | Brief black screen, then the title: stormy sky, drifting clouds and ash, **B E A S T** upper right, a menu list on the left |
| 1.2 | Listen | A slow, calm **title tune** (drone and harp, later a flute) plus a quiet wind |
| 1.3 | Hover each menu item | A white brush swash moves behind it; a soft tick sounds |
| 1.4 | Look at **Load Game** with no saves | Greyed out; **Continue** isn't shown |
| 1.5 | Press **↓ / ↑** (or W / S), then **Enter** on **Settings** | The swash follows the keys; Settings opens |

### Settings *(TC §13l)*
| # | Action | Expected |
|---|---|---|
| 1.6 | Look at the window | Parchment window, four category cards across the top: Controls, Display, Audio, Interface |
| 1.7 | **Audio** → drag **Music** to 0 | The title tune goes silent. Drag back to 100% → it returns |
| 1.8 | **Master** to 0 | Everything silent. Put it back |
| 1.9 | **Interface → Interface size 120%** | Text and windows get bigger. Set back to 100% |
| 1.10 | **Controls** → note the mouse sensitivity value | — |
| 1.11 | **Esc** or **Back** | Back on the title list |

### New game and character creator *(TC §13o, §13v)*
| # | Action | Expected |
|---|---|---|
| 1.12 | **New Game** | Parchment window with three slot cards, all Empty |
| 1.13 | **Start here** on Slot 1 | The creator: your character idling on a black stage, **Name** field at the top showing "Wanderer (type a name)" |
| 1.14 | Click the Name field and type `Edda7!` | Only "Edda" appears (digits and symbols ignored) |
| 1.15 | Type until it's very long | It stops at 16 letters. Backspace back to a name you like |
| 1.16 | Change **Hair**, **Hair colour**, **Skin**, **Outfit** | The preview changes at once |
| 1.17 | Press **<** / **>** | The preview turns through 8 directions |
| 1.18 | **Preview with: Greatsword** | A long two-handed sword, no shield. Switch back to Sword & Shield |
| 1.19 | **Randomise** | A new look; **the name is kept** |
| 1.20 | **Random** (next to the name) | A random fantasy name |
| 1.21 | **Esc** | Back to the slot list. **Start here** again → your choices are reset or kept, no errors |
| 1.22 | Set a name and look you'll recognise, then **Begin your journey** | The world loads on **Spring 1, 08:00** (the HUD clock reads "Spr 1"); a **FIRST DAY** card at the top left starts the guide; the music crossfades to the **day tune** (lute and flute) |

---

## Sitting 2 — HUD, movement, camera (15 min)

| # | Action | Expected |
|---|---|---|
| 2.1 | Look at the HUD | Bottom centre: your **name in spaced capitals**, "KNIGHT · Sword & Shield", thin red health bar with numbers, stamina and poise bars. Bottom right: action slots **E, Q, R, X**. Top right: minimap, then the clock (sun icon) and gold (10), then the quest tracker |
| 2.2 | Bottom left | Key hints (Esc Menu · Tab Bag · C Character · J Journal · M Map) and the grey DEV line |
| 2.3 | **WASD** | You move relative to the camera and face the way you walk; footsteps sound |
| 2.4 | Hold **Shift** while moving | Sprint; stamina drains |
| 2.5 | Tap **Shift** | A dodge (whoosh); with no direction held you step back |
| 2.6 | **Space** | Jump; you land cleanly |
| 2.7 | Move the mouse in a full circle | The camera orbits; you can't look under the ground; your sprite shows front / side / back views |
| 2.8 | Put a house between you and the camera | The camera moves in, no clipping or jitter |
| 2.9 | Walk to the edge of the world past the trees | An invisible wall stops you |
| 2.10 | **Esc** | The game menu: black bar with tabs, **Options** selected, music gets quieter, world paused |
| 2.11 | **Esc** | Back in the game, music back to full |
| 2.12 | **M** | Map tab with the legend; time frozen. **M** again closes it |

---

## Sitting 3 — Fighting basics (25 min)

### Training dummy *(TC §3)*
| # | Action | Expected |
|---|---|---|
| 3.1 | Find the **Training Dummy** and tap **LMB** repeatedly | 4-hit Sword & Shield combo: lunge, hit-stop, screen shake, white flash, damage numbers, thud sound |
| 3.2 | Wait 1 s between hits | The combo restarts from hit 1 |
| 3.3 | Hold **LMB** | A light attack, then the charged Shield Bash |
| 3.4 | Keep hitting until the purple poise bar empties | The dummy staggers (purple tint) |
| 3.5 | **X** | "Weapon: Rusted Greatsword"; your sprite holds a two-handed sword; the combo is 3 slow, big hits. **X** again → back |
| 3.6 | Hold **RMB** | You block, move slower and face the camera direction |
| 3.7 | Listen while hitting the dummy for 20 s | **No combat music** (the dummy doesn't count as a fight) |
| 3.8 | **C** → Character tab, check Combat XP | A dummy kill gives +2 Combat XP |

### Bandits (northeast) *(TC §4, §5, §13h)*
| # | Action | Expected |
|---|---|---|
| 3.9 | Walk towards the bandit camp | Within ~12 m Bandits chase you. Within about a second the **combat music** (drum, fast strings) takes over |
| 3.10 | Watch a Bandit attack | It glows orange during the wind-up, then swings. Getting hit: red numbers, your HP drops |
| 3.11 | Sidestep after the wind-up | The swing misses |
| 3.12 | Block a swing facing it | Blue flash, clang, reduced damage, stamina drops |
| 3.13 | Tap **RMB** just before a hit | "PARRY!", yellow flash, ring sound; the Bandit staggers (purple) |
| 3.14 | Block until stamina runs out | "GUARD BREAK"; you're staggered |
| 3.15 | Fight all three Bandits at once | At most two attack at a time; the third circles and waits |
| 3.16 | **Middle mouse** on a Bandit | "[ LOCK ]" over it; the camera keeps it framed. Middle mouse again releases |
| 3.17 | Kill a Bandit | Death thump; loot cards pop out and fly to you ("+2 Bandit Cloth", gold coins); "+25 Combat XP"; "+2 Standing". It respawns ~6 s later |
| 3.18 | Run more than ~18 m away | They give up and walk home. About 5 s later the **day tune** comes back |
| 3.19 | Lure a Bandit behind a house | It walks **around** the house, not into the wall |

### The archer *(TC §13s)*
| # | Action | Expected |
|---|---|---|
| 3.20 | Approach the green-clad **Bandit Archer** at the camp | It keeps about 9 m away; walk at it and it backs off |
| 3.21 | Watch it shoot | It flashes red while drawing, then an arrow flies in a slight arc |
| 3.22 | Sidestep during the draw | The arrow misses |
| 3.23 | Block facing it | Arrow blocked: reduced damage, stamina used |
| 3.24 | Dodge through an arrow | No damage |
| 3.25 | Hide behind a house | The archer walks round until it can see you again |
| 3.26 | Kill it | Loot and Combat XP like a Bandit; it respawns later |

### Defeat
| # | Action | Expected |
|---|---|---|
| 3.27 | Let the Bandits kill you | Red tint, "D E F E A T E D", a countdown; you respawn at the start with full HP and stamina; levels and items kept |
| 3.28 | Watch the Bandits while you're down | They go idle |

---

## Sitting 4 — Climbing, falling and enemies that follow (25 min)

Go to the **climbing course** west of the village (grey blocks on the map). *(TC §13f, §13r, §13s)*

| # | Action | Expected |
|---|---|---|
| 4.1 | Run at the **1.4 m block**, press **Space** | You vault up onto it |
| 4.2 | Same at the **2.3 m block** | Vaulted, a little slower |
| 4.3 | Run at the long **3.2 m wall**, press **Space** | You catch the edge and hang; prompt "[Space] Climb up · [A / D] Shimmy · [Shift] Let go"; stamina drains slowly |
| 4.4 | **A / D** | Shimmy; you stop at the wall's end |
| 4.5 | **Space** | Pull up onto the wall |
| 4.6 | Walk to the wall's east edge (wooden lip), stop facing the drop | **[Space] Climb down** shows |
| 4.7 | **Space** | You step over, turn and hang from the edge |
| 4.8 | **Shift** | You drop. No fall damage (3.2 m) |
| 4.9 | Hang until stamina runs out | "Too tired to hold on"; you fall |
| 4.10 | From the wall top, face the ivy cliff, press **Space** | You grab the ivy. **WASD** climb in all directions |
| 4.11 | Climb to the top | You pull up onto the cliff |
| 4.12 | On top, stop at the edge facing the wall below, press **Space**, hold **S** | You go onto the ivy and climb down onto the wall. No damage |
| 4.13 | Jump off the 6 m cliff | Little or no damage |
| 4.14 | Climb the **10 m ivy tower** to the top, then walk off | You lose about a third of your health, heavy thud, camera shake, short stun |
| 4.15 | Climb the tower again and climb **down** the ivy instead | No damage |
| 4.16 | Jump at a **house** wall | No grab (houses aren't climbable) |
| 4.17 | Lure a Bandit to the course; stand on the **2.3 m block** | It vaults up after you and attacks |
| 4.18 | Stand on the **3.2 m wall** | Melee Bandits wait below, then give up after a few seconds and walk home |
| 4.19 | Drop down while a Bandit stands on a block | It jumps down after you |
| 4.20 | Hang or climb where the archer can see you | Arrows hit and knock you off ("Knocked off!") |
| 4.21 | With low health, walk off the tower | Normal defeat and respawn; no extra damage afterwards |

---

## Sitting 5 — Bag, gear, skills (25 min)

### Bag *(TC §7, §13q)*
| # | Action | Expected |
|---|---|---|
| 5.1 | **Tab** | Game menu on **Bag**: category cards (All, Food, Materials, Seeds & crops, Gear) with counts; items have picture icons |
| 5.2 | Click **Bread** | Red frame; the black panel shows its icon, "Restores 30 health." and **Use** |
| 5.3 | **Use** (when hurt) | HP up, crunching sound, count goes down |
| 5.4 | Right-click a Healing Draught | Used straight away; gulping sound |
| 5.5 | **Q / E** | Previous / next tab; tabs wrap around |
| 5.6 | Close with **Tab**, then open and close with **I**, then with **Esc** | Each closes the menu and resumes the game |
| 5.7 | In the world, hurt, press **R** | Eats your first food; ~0.8 s cooldown between presses |

### Gear and stats *(TC §12)*
| # | Action | Expected |
|---|---|---|
| 5.8 | **C** → **Gear** | Your character idles in the middle with your name under it; weapons and the accessory on the left, head / body / legs on the right; the weapon in hand says "in hand" |
| 5.9 | Click the Body slot (Gambeson) | Black panel: **Unequip**, spare gear that fits, your stats |
| 5.10 | **Unequip**, then **Equip** it again | Defense drops, then returns |
| 5.11 | Press **X** in the world, then check Gear | Attack changes with the active weapon |

### Levels and skills *(TC §11, §13)*
| # | Action | Expected |
|---|---|---|
| 5.12 | **F6** once | "Combat level 2! (+1 skill point)"; max HP goes up; your health fill stays the same percentage; a chime |
| 5.13 | **C** → **Combat** | Skill diamonds in tiers; learnable ones black, locked grey |
| 5.14 | Learn **Veteran's Vigor**, then **War Cry** (F6 again if you need points) | Gold rim; War Cry goes on **E**; the HUD's E slot shows it |
| 5.15 | Press **E** near a Bandit | A shout knocks it back; Attack +20% for 10 s; an 18 s cooldown shades the slot |
| 5.16 | Reach level 3–4 (F6), learn **Shield Charge** and **Whirlwind** | Shield Charge on Q. **Set Q** on Whirlwind replaces it |
| 5.17 | With the Sword & Shield, use Shield Charge; with the Greatsword, use Whirlwind | Charge dashes into an enemy; Whirlwind spins and hits all around. Shield Charge with the Greatsword says it needs Sword & Shield |
| 5.18 | **F7** a few times → **Farming** tab | Learn **Green Thumb** (and others) |

---

## Sitting 6 — Farming, sleep and the homestead (30 min)

### Farming *(TC §8)*
Go to the dark 6×6 field southeast of the spawn.

| # | Action | Expected |
|---|---|---|
| 6.1 | Face a tile | Glowing outline, "[F] Till soil" |
| 6.2 | **F**, **F**, **F** | Tilled (brown, sound) → planted (sprout) → watered (darker); each has its own sound |
| 6.3 | Hold **F** while walking along a row of grass | It tills the whole row; hold again on that row to plant, then to water |
| 6.4 | **V** | "Seeds: Healroot Seeds". Plant a few Healroot too |
| 6.5 | **F1** (next day) | Watered crops grow one stage; the soil dries |
| 6.6 | Water and F1 until the Turnips are ripe (4 watered days) | White bulbs; "[F] Harvest Turnip" gives 1–2 Turnips and +6 Farming XP each |
| 6.7 | Leave one crop unwatered for 2 days | It wilts; water it and it recovers. 3 dry days → it dies; "[F] Clear dead Turnip" |
| 6.8 | Keep Healroot growing to ripe (6 watered days) | Red berries; harvest leaves the plant to regrow |
| 6.9 | **F4** until it rains | Rain streaks, fog, rain sound; the field counts as watered |

### Sleep *(TC §9)*
| # | Action | Expected |
|---|---|---|
| 6.10 | Stand by the red bed | "[F] Sleep until 06:00" |
| 6.11 | **F** | Black screen "D A Y  N · Morning, 06:00 · You feel rested · Game saved", fade back in; HP and stamina full |
| 6.12 | Hold **F** at the bed for 3 s | You sleep only once |

### Workbench *(TC §13t)*
| # | Action | Expected |
|---|---|---|
| 6.13 | Walk to the workbench beside the bed | "[F] Use workbench" |
| 6.14 | **F** | The **Workbench** window; game paused; cards **Alchemy, Cooking, Smithing** with "can make / total" numbers |
| 6.15 | Look at a recipe row | Result icon and name, what it needs (missing items in red), what it does |
| 6.16 | With 2+ Healroot, **Craft** a **Healing Draught** | 2 Healroot gone, +1 draught, pouring sound, "Made Healing Draught." Without enough, the button is greyed |
| 6.17 | **×5** | Makes as many as you can (up to 5) and reports how many |
| 6.18 | **Cooking → Turnip Stew**, craft, then eat it | Healed, and "+15 Defense 3:00" counts down under the health bar; Character → Gear shows higher Defense; it ends when the time runs out |
| 6.19 | Make and drink a **Stamina Tonic** | Stamina refills; "+30% Stamina Regen" countdown |
| 6.20 | Get 6 Iron Scrap and 2 Bandit Cloth (Bandits drop them), with the Rusted Sword & Shield equipped → **Smithing → Iron Sword & Shield** | The ingredient line says "(equipped)". Craft → clang; you now hold the **Iron** Sword & Shield, still equipped; the rusted one is gone |
| 6.21 | Make a **Padded Cap** (4 cloth) | It goes into the bag |
| 6.22 | Wear the cap, then craft an **Iron Helm** | The cap is upgraded in place |
| 6.23 | **Esc** | The window closes; the game resumes |

### Storage chest *(TC §13u)*
| # | Action | Expected |
|---|---|---|
| 6.24 | Walk to the chest beside the bed, **F** | Bag / Chest window; top right shows how full both are |
| 6.25 | **Store** and **All** on a bag item; **Take** and **All** on a chest item | Items move; no "+N item" pop-ups while moving |
| 6.26 | **Store crops, seeds & materials** | Crops, seeds and materials go into the chest; food, potions and gear stay in the bag |
| 6.27 | With Healroot only in the chest, open the workbench | The Healing Draught row says "(chest)" and can be crafted; the Healroot comes out of the chest |
| 6.28 | Fill your bag (buy seeds), then **Take** | "Your bag is full." |

### Mirror *(TC §13v)*
| # | Action | Expected |
|---|---|---|
| 6.29 | Walk to the standing mirror, **F** | "The mirror": your current look and name, holding your current weapon |
| 6.30 | Type a name containing **I, C, J** | The letters appear; the window doesn't close |
| 6.31 | Change hair and name → **Keep this look** | Your sprite changes; "You are now …"; the HUD name updates |
| 6.32 | Open it again, change things → **Cancel** (and once with **Esc**) | Nothing changes |

---

## Sitting 7 — Town, people and quests (40 min)

### Talking *(TC §13b)*
| # | Action | Expected |
|---|---|---|
| 7.1 | Walk to **Oswin** (green, north of the field) | He has a "!" marker; "[F] Talk to Oswin" |
| 7.2 | **F** | Dark dialogue band at the bottom, portrait, red name, text typing out; the game pauses |
| 7.3 | Press Space while text types | The whole line shows; Space again → next line |
| 7.4 | Pick choices by clicking, by ↑/↓ + Enter, and by number keys | Each works; clicking empty space does nothing |
| 7.5 | Ask about the land, then **decline** the Bandit quest if offered, or accept it | Every quest offer has an accept **and** a decline choice; declined offers can be asked again later |
| 7.6 | Accept **Bandit Trouble** | "QUEST ACCEPTED" banner; tracker shows "Defeat Bandit (0/3)"; a ◆ waypoint over the nearest Bandit; chime |
| 7.7 | Mid-conversation press **Tab, I, C, J** | Nothing happens. **Esc** ends the conversation |

### Quest chain
| # | Action | Expected |
|---|---|---|
| 7.8 | Kill 3 Bandits | Tracker 1/3 … 3/3; "OBJECTIVES COMPLETE — return to Oswin"; Oswin shows "?"; the dummy doesn't count |
| 7.9 | Hand in to Oswin | "QUEST COMPLETE" (+50 gold, +60 Combat XP, standing); he offers **Roots of the Blight** |
| 7.10 | Say **"Not now."**, then later ask **"About Brenna's ash..."** and accept | No quest on decline; it starts on accept |
| 7.11 | Talk to **Brenna**, bring 1 Healroot, talk again | Quest completes; the Healroot is taken |
| 7.12 | Oswin "Need anything?" → **A Taste of Home** (5 Turnips) | Done when you hold 5; selling below 5 un-ticks it; hand-in takes 5 Turnips |
| 7.13 | Brenna "Any work?" → **Scrap Run** (4 Iron Scrap) | Hand-in takes 4 scrap, +60g |

### Shops *(TC §10)*
| # | Action | Expected |
|---|---|---|
| 7.14 | Oswin → "I'd like to trade." | Parchment shop: his name in spaced capitals, your gold and standing, Buy and Sell columns |
| 7.15 | Buy Turnip Seeds; **×5** | Gold down, seeds up, stock down; the footer says what happened |
| 7.16 | Sell a big stack with **All** | A "market NN%" tag; the price falls as you sell more |
| 7.17 | Brenna → "Show me your wares." | Weapons and armour for sale; she only buys materials and equipment |

### Contracts board, journal, reputation *(TC §13b, §13c, §13e)*
| # | Action | Expected |
|---|---|---|
| 7.18 | The brown board near the spawn → **F** | Contract cards with status (Available, In progress, Ready, Done today, Locked) |
| 7.19 | **Accept** "Bounty: Road Bandits", kill 3 Bandits, **Turn in** | Rewards; "Done today". After F1 it's available again |
| 7.20 | Look at **Town Patrol** | **LOCKED** — "Requires Trusted standing" |
| 7.21 | **J** | Journal: filter cards with counts, quest rows, the tracked one with a gold diamond, standing card bottom left |
| 7.22 | With 2+ quests active press **T** in the world | "Tracking: …"; tracker, waypoint and minimap ◆ all switch |
| 7.23 | **Abandon** a contract in the journal | Asks first, mentions −5 standing; confirming removes it |
| 7.24 | **F8** three times | "STANDING RAISED" banners as you pass Known (50) and Trusted (150); shop prices improve; Town Patrol unlocks |
| 7.25 | As Trusted, talk to Oswin | A one-time gift (2 Bread and a Healing Draught) |

### NPC routines *(TC §13w)*
| # | Action | Expected |
|---|---|---|
| 7.26 | Morning | Oswin at his stall, Brenna at her forge |
| 7.27 | **F2** to ~12:00 | Oswin walks to the well along the paths, around houses; back at 13:00 |
| 7.28 | ~13:00 | Brenna walks to the contract board; back at the forge by 14:00 |
| 7.29 | Stand next to someone walking | They stop; you can talk (Oswin's shop still opens); step away and they carry on |
| 7.30 | Evening | Both stand by the well |

---

## Sitting 8 — Night, weather and fatigue (15 min)

*(TC §13i, §13j, §13x)*

| # | Action | Expected |
|---|---|---|
| 8.1 | **F2** to ~19:00 | Orange light, long shadows, lanterns start glowing |
| 8.2 | ~21:00 | "Night falls. Bandits are bolder now…"; Bandit name plates red with a moon; the **night tune** (quiet, sparse); crickets |
| 8.3 | ~22:30+ | Oswin and Brenna have gone home: no sprites, markers, minimap dots or Talk prompts |
| 8.4 | Fight Bandits at night | They notice you from further, hit harder, drop more loot; the archer's arrows hurt more |
| 8.5 | Stay up past midnight | "You're getting tired…", **TIRED** under the bars, shorter stamina |
| 8.6 | Stay up to ~04:00 | "You're exhausted…", red **EXHAUSTED**, lower attack |
| 8.7 | Sleep | Badge gone; at 06:00 the NPCs are straight back at work; day tune |
| 8.8 | **F4** three times | Cloudy → Rain → Clear, with matching sky, sound and HUD icon |

---

## Sitting 9 — Controller (15 min, if you have a pad)

*(TC §13y)* Plug in an Xbox-style pad.

| # | Action | Expected |
|---|---|---|
| 9.1 | Play with the pad: move, camera, A jump, X attack, Y heavy, LB block, B dodge / sprint | All work |
| 9.2 | Look at the HUD hints | They show pad buttons ([A] [B] [LB / RB], ↑ for interact). Touch the mouse → keyboard hints again |
| 9.3 | **Start** | Game menu. The D-pad moves an ink-gold focus frame; **A** presses; **LB / RB** switch tabs; **B** goes back or closes |
| 9.4 | Bag with the pad | The focused slot shows its details; **A** uses or equips |
| 9.5 | Character → skills and gear slots with the D-pad | Reachable; A learns / equips |
| 9.6 | Shop, board, workbench, chest and mirror with the pad | Everything reachable; A buys / crafts / stores / accepts; B leaves |
| 9.7 | Settings with the pad | Left / right move sliders; A flips toggles; long lists scroll to keep the focus visible |
| 9.8 | Move the mouse | The focus frame hides; the mouse works everywhere as before |
| 9.9 | Quit to title; use the D-pad there and in the creator | Navigation works; **Random** gives a name without typing |
| 9.10 | Keyboard only: arrow keys + Enter in any menu | Same navigation; typing a name isn't interrupted |

---

## Sitting 10 — Saving and loading everything (20 min)

*(TC §13k, §14)* The most important sitting.

| # | Action | Expected |
|---|---|---|
| 10.1 | Set up a varied state: crops at different stages (one wilted), items in the chest, crafted Iron gear equipped, the Greatsword active, a skill on E/Q, an active quest half done, a contract done today, some standing, a changed name and look, an active food buff | — |
| 10.2 | **Esc → Options → Save game** | "Game saved." |
| 10.3 | Note: position, time, gold, HP, levels, chest contents | — |
| 10.4 | **Quit to title** (confirm) | Back on the title; **Continue** shows "Slot 1 · Day … · played …" |
| 10.5 | **Load Game** | Slot 1's card begins with **your name**, then day, gold, levels, standing, play time |
| 10.6 | Load it | Everything from 10.1/10.3 is back exactly. The food buff has ended (by design) |
| 10.7 | Attack | The combat style matches the restored active weapon |
| 10.8 | **F5**, change lots of things, **F9** | Back to the F5 moment |
| 10.9 | Open the bag and press **F9** | Refused ("Can't load right now"); close the bag and F9 works |
| 10.10 | Save at night, load | Oswin and Brenna still at home. Save at noon, load → Oswin at the well |
| 10.11 | Save on top of the tower, walk off, **F9** while falling | Back on the tower, no damage |
| 10.12 | Quit to title → **New Game** on Slot 2 with the name left empty | You're "Wanderer"; Day 1; empty chest; starting gold. Slot 1 untouched |
| 10.13 | **Load Game → Delete** Slot 2 | Asks first; then Empty |
| 10.14 | Stop Play, press Play in Bootstrap again | Settings from Sitting 1 are still as you left them |

---

## Sitting 11 — Edge cases (15 min)

*(TC §15)*

| # | Action | Expected |
|---|---|---|
| 11.1 | Open a menu during hit-stop (right after a hit) | Pauses properly; normal speed after closing |
| 11.2 | **Esc** mid-attack, resume | The attack carries on normally |
| 11.3 | Die while blocking or charging a heavy | Normal state after respawn |
| 11.4 | Sleep while a Bandit chases you | Works; no errors |
| 11.5 | **F1** ten times quickly | No errors; crops and shops update each day |
| 11.6 | Attack Oswin or Brenna | No damage; lock-on never targets them |
| 11.7 | Hold **F** on the field while a Bandit hits you | Interacting stops while staggered and resumes |
| 11.8 | Resize the Game view (Full HD → 1280×720 → small) | HUD and menus scale; nothing overlaps or runs off-screen |
| 11.9 | **Window → Analysis → Profiler**, 2 minutes of combat | No steady GC spikes; frame time doesn't climb |

---

## Sitting 11b — Batch 2 features (45 min)

The details for each are in Test_Checklist.md §13ab–13aj.

| # | Action | Expected |
|---|---|---|
| B.1 | Talk to anyone | The camera glides over your right shoulder; the name and line are centred at the bottom, choices on the right |
| B.2 | Look beside the bed | The storage chest is there, not on the field |
| B.3 | Track a quest, look at the minimap | A gold dotted trail leads to the target |
| B.4 | North-west woods | Three wolves: they circle behind you and dart away after biting |
| B.5 | Bandit camp | The Shieldbearer blocks from the front; hit him from behind or break his guard |
| B.6 | Past the camp at night | The Blighted Brute; gone by day |
| B.7 | Meet Maren, Tobin and Captain Hale (Hale at night) | Each talks, uses your name, offers a quest; Maren heals you and sells remedies |
| B.8 | Fight a while, then check your gear | Condition drops; Brenna or the workbench (Smithing → Mend) repairs it |
| B.9 | The pen: fill the trough, sleep, collect | Eggs from the hens, milk from Bess |
| B.10 | F1 to day 15, 29, 43 | Summer, autumn (orange), winter (snow, bare oaks); the frost kills turnips; only frost kale grows |
| B.11 | Gather wild food; buy a rod and fish at the pond | Gathering works; the fishing timing game lands fish |
| B.12 | The contracts board, two days running | A different set of four each day |
| B.13 | The plans by the bed | Build the Kitchen Garden and the rest with gold and scrap |
| B.14 | Settings → Controls → Keys | Rebind Interact; the HUD prompt shows the new key; Reset keys |
| B.15 | A bandit behind a house; lock on; get hit from the side | No plate through the wall; a target bar at the top; a red arrow toward the hit |
| B.16 | Esc → How to play | The guide page; Esc backs out |

---

## Sitting 12 — The Windows build (20 min)

*(TC §13z)*

| # | Action | Expected |
|---|---|---|
| 12.1 | **Beast → Build → Windows (Release)** | Build finishes; Explorer opens `Builds/Windows` with **Beast.exe** |
| 12.2 | Run Beast.exe | Title screen with music; no DEV line |
| 12.3 | **Settings → Display**: change Resolution or Window mode | "Keep these display settings? Reverting in 12 s"; wait → it reverts. Change again → **Keep** → it stays |
| 12.4 | New game, play 5 minutes (fight, farm, craft), **Esc → Save game**, **Quit to desktop** | Closes cleanly |
| 12.5 | Run Beast.exe again → **Continue** | Everything as saved |
| 12.6 | Press F1, F5, F9 | Nothing (dev keys are off in release) |
| 12.7 | Listen throughout | Sound and music play. **Also note whether you hear sound in the Editor** (the open "no sound in editor" report) |
| 12.8 | **Beast → Build → Windows (Development)** and run it | Same, plus the DEV line and dev keys |
| 12.9 | `git status` in the project | Nothing under `Builds/` |

---

## Known limitations (not bugs — don't report)

- All art, icons, portraits, sounds and music are generated placeholders.
- The UI is the IMGUI prototype layer (a UI Toolkit port comes later with real art).
- Gear condition is kept per kind of item: two identical swords share one condition.
- Merchants have unlimited gold.
- Food buffs end when you load a save.
- Thin shadows (e.g. a sword) don't show on other characters.
- Re-running the Milestone 9 setup rebuilds `Environment_Dressing` from scratch.
- In the Editor, Window mode and Resolution follow the Game view; test them in a build.

---

## Results log

| Step | Pass / Fail | What happened (for failures: expected vs actual, Console message, every time or x of y) |
|---|---|---|
| | | |
| | | |
| | | |

When you're done, send me the failed steps (or paste this table) and I'll fix them in order.
