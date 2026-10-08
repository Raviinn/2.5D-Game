# Game Design Document — *Working Title: MegaGame101*

| | |
|---|---|
| **Version** | 1.25 |
| **Last updated** | 2026-10-08 |
| **Author** | Joseph |
| **Engine** | Unity 6 LTS (6000.0.34f1), URP |
| **Unity project** | `Documents/Unity/Beast` |
| **Platform** | PC (Steam) — keyboard/mouse + controller |
| **Team** | Solo, part-time (open to adding people later) |
| **Goal** | Commercial release on Steam |
| **Current phase** | Milestone 4 — Systems (4a Items ✅ · 4b Farming ✅ · 4c Economy ✅ · 4d Progression ✅ · 4e Dialogue & Quests 🟡 · polish pass: UI/UX + environment art 🟡 · light reputation 🟡 · climbing & ledges 🟡 · sprite shadows 🟡 · smarter enemies 🟡 · day/night & weather 🟡 · night danger & fatigue 🟡 · main menu & save slots 🟡 · settings 🟡) |

**Status legend:** ✅ Decided / built · 🟡 Draft / prototype · ⬜ Not started / open

> **How to use this doc:** This is the single source of truth for the game's design. Edit freely. Mark sections ✅ once locked. Every unanswered question lives in **§25 Open Questions**. Log major changes in **§28 Change Log**.

---

## Table of Contents

**Part I — Vision**
1. [Overview](#1-overview)
2. [Design Pillars](#2-design-pillars)
3. [Scope & Constraints](#3-scope--constraints)

**Part II — Gameplay**
4. [Core Loop](#4-core-loop)
5. [Combat](#5-combat)
6. [Classes](#6-classes)
7. [Enemies](#7-enemies)
8. [Items, Inventory & Loot](#8-items-inventory--loot)
9. [Homestead & Farming](#9-homestead--farming)
10. [Economy & Shops](#10-economy--shops)
11. [Progression](#11-progression)
12. [Time, Day/Night & Pressure](#12-time-daynight--pressure)
13. [Traversal](#13-traversal)
14. [Controls](#14-controls)

**Part III — World & Narrative**
15. [World & Story](#15-world--story)
16. [Protagonist](#16-protagonist)
17. [Factions & Reputation](#17-factions--reputation)
17b. [Dialogue & Quests](#17b-dialogue--quests)

**Part IV — Presentation**
18. [Visual Style & Sprite Technology](#18-visual-style--sprite-technology)
19. [Audio](#19-audio)
20. [UI / UX](#20-ui--ux)

**Part V — Technical**
21. [Architecture](#21-architecture)
22. [Project Structure & Setup Tools](#22-project-structure--setup-tools)
23. [Content & Tuning Reference](#23-content--tuning-reference)

**Part VI — Production**
24. [Roadmap & Milestones](#24-roadmap--milestones)
25. [Open Questions](#25-open-questions)
26. [Backlog & Known Compromises](#26-backlog--known-compromises)
27. [Risks](#27-risks)
28. [Change Log](#28-change-log)

---

# Part I — Vision

## 1. Overview
**Status:** 🟡

| | |
|---|---|
| **Genre** | 2.5D single-player open-world action RPG with homestead, farming and economy systems |
| **Unique hook** | 2D characters and enemies (8-directional sprites) in a fully 3D world with a free third-person camera |
| **Player fantasy** | **"Adventurer who builds a home."** Action and exploration lead; the homestead is the reward, base and engine |
| **Tone** | Grounded / gritty — in a high-magic world. Magic is common, but the war turned it into the source of the world's suffering |
| **Target audience** | ⬜ TBD — e.g. action-RPG fans (16–35) who also enjoy base-building / farming games |

### Elevator Pitch
> A war-scarred medieval frontier. You are a veteran who claims an abandoned homestead — fighting through blight-twisted wilds in fast, flashy action combat, while turning your farm into the backbone of a struggling region where what you grow decides who starves and who survives.

### Reference Games
| Game | What we take from it |
|---|---|
| Octopath Traveler / Triangle Strategy | 2D-in-3D (HD-2D) visual approach |
| Genshin Impact | Fast third-person action, exploration, dodge/sprint feel |
| Ni no Kuni II | Adventuring + building your home base |
| Skyrim | Single-player open world, freedom, world reactivity, pausing menus |
| Stardew Valley | Farming loop, day structure, sleep-to-save |
| Kingdom Come / The Witcher | Grounded tone, harsh world |
| Devil May Cry | Combo flow, cancels, hit impact |

---

## 2. Design Pillars
**Status:** ✅

Every feature must serve at least one pillar. If it doesn't, cut or simplify it.

1. **Flashy combat with impact** — Fast combos, cancels and dodges. Weight comes from hit-stop, screen shake, knockback and stagger. Every class feels distinct; every hit lands.
2. **Earn your home** — The homestead is your reward, base and engine. Adventure feeds the farm; the farm fuels adventure.
3. **A harsh world that pushes back** — Scarcity is real. Prices, danger and time shape your choices.

---

## 3. Scope & Constraints
**Status:** ✅

### Development Constraints
| Constraint | Implication |
|---|---|
| Solo, part-time | Scope ruthlessly; one polished region first; cheap art pipeline |
| Balanced code + art skills | Can prototype systems and art style in parallel |
| Commercial Steam release | Realistic horizon ~3–4 years to v1; consider Early Access |
| PC first | Keyboard/mouse + controller from day one |

### v1 vs Post-Launch
| Feature | v1 | Post-launch |
|---|---|---|
| Action combat + classes | ✅ Full focus (3 classes, multiple styles each) | More classes, multiclass |
| Farming + economy | ✅ Full focus | Seasons, livestock, regional markets |
| Homestead | Fixed build slots, upgradable | Free-form editing, buying more land |
| Reputation | One town standing | Full multi-faction system |
| World | One region | Additional regions |
| Traversal | Walk, sprint, jump, dodge + **climbing & ledges** | — |
| Cutscenes | 2–3 staged + in-engine dialogue scenes | More |
| Early Access on Steam | ⬜ Consider | — |

---

# Part II — Gameplay

## 4. Core Loop
**Status:** ✅ *(playable end-to-end in greybox as of Milestone 4c)*

### Moment-to-moment (seconds)
Fight → dodge / read enemy tells / punish → loot.

### Session loop (30–60 min)
```
Morning at the homestead: tend crops, sell at market, buy supplies
        ↓
Explore the wilds / take a contract (combat, gathering, discovery)
        ↓
Return with loot, seeds and materials before nightfall (nights are dangerous)
        ↓
Sell, invest (seeds, gear, upgrades), sleep (saves) → next day
```

### Long-term loop (hours)
Unlock homestead buildings → better crops & gear → push into more dangerous blighted zones → uncover the truth behind the Blight.

### Combat ↔ Farming (mutual dependency)
| The wilds give the farm… | The farm gives combat… |
|---|---|
| Seeds (✅ bandit loot) & rare plant cuttings | Food that heals (✅ Turnip) |
| Materials (✅ cloth, scrap; later wood, ore, hide) | Potions and meals from crops (✅ Healroot → draughts and tonics, turnip stew) |
| Cleared land (kill nests → expand fields) | Money for gear & training (✅ selling crops) |
| Livestock (tamed / rescued) | Supplies for long expeditions |

### Currently Playable Loop (greybox)
Kill Bandits → loot seeds, cloth, scrap, gold → plant & water on the field → sleep → harvest Turnips / Healroot → brew draughts and cook stew at the workbench, forge the scrap into better gear → sell the rest to Oswin → buy more seeds → fight again.

---

## 5. Combat
**Status:** ✅ decisions · 🟡 prototype (Milestone 2, verified fun)

### Core Decisions
| Mechanic | Decision |
|---|---|
| Pacing | **Fast & flashy** (Genshin / DMC speed); weight via hit-stop, shake, knockback, poise & stagger |
| Defense | **Dodge** (i-frames) + **block** (reduces damage, costs stamina) + **parry** (tap block just before a hit → attacker staggered) |
| Guard break | Blocking with too little stamina breaks your guard (stagger) |
| Targeting | **Soft-aim**: attacks snap to the best enemy in front · **optional hard lock-on** (camera and facing track the target) |
| Stamina | Used by dodging and blocking; regenerates after a short delay |
| Cancels | Light attacks cancel into dodge at any time; combos chain once the strike frames finish; dodge-attacks allowed |
| Input buffer | 0.25 s — presses during an attack or dodge aren't lost |
| Dodge vs sprint | Same button: **tap** (released < 0.2 s) = dodge on release · **hold** = sprint (no dodge) |
| Heavy attack | **Charged attack:** tap attack = light; keep holding (0.35 s) = heavy, chained as the follow-up (Genshin-style). Gamepad also has a dedicated heavy button |
| Styles | Data-driven: **the equipped weapon decides the combat style** (its moveset). Two weapons equipped; quick-swap between them |
| Active skills | Two equipped skills on **E / Q** with cooldowns; they perform special attacks and can grant timed buffs; they can cancel an attack's recovery |
| Poise | Hits drain poise; at zero the target is staggered; poise regenerates |
| Enemy telegraph | Enemies **glow orange** during their wind-up; late sidesteps dodge swings (enemies only track during wind-up) |
| Feedback colours | White = hit · blue = blocked · yellow = parry · purple = stagger · pale = i-frames |
| Death (prototype) | Player respawns at spawn after 2 s; enemies respawn after a delay |

### How Attacks Work
Every attack is a data asset with **Startup → Active → Recovery** timings, a hitbox, damage, poise damage, knockback, lunge, hit-stop and camera shake. The same attack system runs player and enemy attacks. Sprite animations are **synced to these timings**, so the strike frame always shows exactly when the hitbox is live.

### Open
- ⬜ Solo or companions · ⬜ Class switching / multiclass · ⬜ Aerial attacks · ⬜ Ranged & magic combat (Ranger / Battlemage)

---

## 6. Classes
**Status:** 🟡 (Knight prototyped; Ranger & Battlemage not started)

The protagonist's class is **the unit they served in** during the war.

| Class | Wartime role | Fantasy | Status |
|---|---|---|---|
| **Knight** | Heavy infantry | Unbreakable wall / devastating greatsword | 🟡 Prototyped |
| **Ranger** | Scouts | Mobile hunter (bow + dagger) | ⬜ |
| **Battlemage** | War-casters | Destructive magic | ⬜ |

### Knight (prototype)
**Base stats:** 150 HP · 60 poise · 100 stamina (regen 35/s after 0.7 s) — grown by Combat level, gear and skills (§11).

| Style (weapon) | Feel | Light combo | Heavy | Dodge | Parry window |
|---|---|---|---|---|---|
| **Sword & Shield** | Fast, safe | 4 hits (12 · 12 · 18 · 26 dmg) | Shield Bash (20 dmg, 45 poise) | 4.5 m, 20 stamina | 0.18 s |
| **Greatsword** | Slow, huge hitboxes, heavy poise damage | 3 hits (22 · 24 · 40 dmg) | Overhead (45 dmg, 70 poise) | 4.0 m, 25 stamina | 0.12 s |

### Knight Skill Tree (Combat Discipline)
| Tier | Skill | Type | Req. level | Effect |
|---|---|---|---|---|
| 1 | **Veteran's Vigor** | Passive | 1 | +10% max health |
| 1 | **War Cry** | Active · 18 s | 2 | Shout that shoves nearby enemies (30 poise dmg) + buff: +20% attack, +30% poise for 10 s |
| 2 | **Second Wind** | Passive | 3 | Perfect parry restores 20 stamina |
| 2 | **Shield Charge** | Active · 8 s · Sword & Shield | 3 (needs War Cry) | 5 m shield dash, 18 dmg, 55 poise, heavy knockback |
| 3 | **Iron Will** | Passive | 4 | +20% max poise, +15 poise damage |
| 3 | **Whirlwind** | Active · 10 s · Greatsword | 4 (needs War Cry) | 360° spin, 38 dmg, 45 poise |

### Open
- ⬜ Ranger & Battlemage kits, resources, styles and skill trees · ⬜ More Knight skills toward level 30

---

## 7. Enemies
**Status:** 🟡

| Enemy | Faction | Status | Notes |
|---|---|---|---|
| **Bandit** | Human (deserters) | 🟡 Prototyped | 70 HP, 30 poise; Slash (0.45 s wind-up) & Overhead (0.75 s wind-up); chases within 12 m; drops cloth, scrap, seeds, bread, gold |
| **Bandit Archer** | Human (deserters) | 🟡 Built (Milestone 25) | 45 HP, 20 poise; keeps ~9 m away (backs off if you close in), draws for 0.9 s (the red warning) then looses an arrow (14 damage, every ~2.6 s, up to 16 m). Needs a clear line of sight and moves to find one. Shoots you while you climb (a hit knocks you off). Arrows can be blocked from the front, parried and dodged. One at the bandit camp; same loot as a Bandit |
| **Training Dummy** | — | 🟡 Prototyped | 200 HP, never attacks; for testing combos |
| Blight-twisted beasts | Blight | ⬜ | Wilds; stronger at night |
| The Ashen | Cult | ⬜ | Raids, dungeons |
| Bosses | — | ⬜ | Farm-reclaim boss (prologue) + dungeon bosses |

**AI (Milestone 13):** Idle → Chase → Attack (telegraphed) → cooldown; staggers.
| Behaviour | How |
|---|---|
| **Pathfinding** | A navigation mesh is built from the level's colliders when the scene loads (nothing baked, so level edits just work). Enemies walk around buildings, walls, trees and the climbing course |
| **Taking turns** | At most **2** enemies close in and attack at once (attack "turns"). The others hold a ring ~3.5 m out, circling slowly, and step in when a turn frees up. A turn passes on after a swing, or after 5 s without one |
| **Spacing** | Enemies keep ~1.4 m apart instead of stacking |
| **Jump links (Milestone 25)** | Enemies **vault up** edges up to **2.4 m** (the same as you; not Not Climbable ones) and **jump down** drops up to **4.5 m** to reach you. Links are found automatically along the navigation mesh's edges when the scene loads |
| **Out of reach** | Climb onto something taller than 2.4 m and melee enemies wait at the closest spot below; after **5 s** they give up and walk home. They can't hit you from more than 1.2 m below. Archers keep shooting as long as they can see you |
| **Leash** | Never chase more than **22 m** from home or 18 m from you; walking home ignores you, and they **heal fully** on arrival (kiting a wounded enemy away doesn't pay) |
| Tuning | Per enemy type on EnemyData: Hold Distance, Give Up After, Leash Range |

### Open
- ✅ Pathfinding · ✅ Group tactics (turn-taking) · ✅ Archer (hits climbers) · ✅ Vaulting up and jumping down · ⬜ More archetypes (brute, beast) · ⬜ Enemies that climb walls · ⬜ Arrow hit/whoosh sounds and a proper arrow sprite

---

## 8. Items, Inventory & Loot
**Status:** ✅ decisions · 🟡 prototype (Milestone 4a)

| Decision | Choice |
|---|---|
| Bag | 30 slots, stackable (per-item max stack), **no weight limit** (keeps fast combat snappy) |
| Currency | **Gold** as a wallet, not an item (⬜ in-world currency name) |
| Icons | 🟡 32 px pixel-art placeholder icon for every item (Milestone 18), shown in the bag, shop, character screen and quick slot. Dropped items show their icon on a small card that faces the camera (gold: a coin stack). Real icons: assign another sprite to the item's **Icon** — the setup never overwrites it |
| Categories | Material · Consumable · Seed · Crop · Equipment · Quest |
| Loot | Per-enemy loot tables (independent % rolls + gold range). Drops pop out as pickups that fly to the player when close; stay on the ground if the bag is full; despawn after 3 min |
| Consumables | Quick-use button eats the first food/potion in the bag; not usable mid-attack/dodge; 0.8 s cooldown. Also usable from the inventory screen |
| Inventory screen | Pauses the game (Skyrim-style). Prototype UI (mouse only) |
| Saving | Items are saved by stable ID; deleted items are skipped with a warning |

### Current Items
| Item | Category | Effect | Value |
|---|---|---|---|
| Bread | Consumable | Heal 30 | 5 |
| Healing Draught | Consumable | Heal 60 + 50 stamina | 25 |
| Turnip | Crop (edible) | Heal 15 | 12 |
| Healroot | Crop | Draught and tonic ingredient | 20 |
| Bandit Cloth | Material | Smithing (caps, weapon grips) | 3 |
| Iron Scrap | Material | Smithing | 8 |
| Stamina Tonic | Consumable (crafted) | +100 stamina, then +30% stamina regen for 2 min | 30 |
| Roast Turnips | Consumable (crafted) | Heal 35 | 12 |
| Turnip Stew | Consumable (crafted) | Heal 55 + 30 stamina, then +15 Defense for 3 min | 30 |
| Turnip Seeds | Seed | Plants Turnip | 2 |
| Healroot Seeds | Seed | Plants Healroot | 6 |

### Equipment ✅ *(Milestone 4d)*
| Decision | Choice |
|---|---|
| Slots | **2 weapons** (quick swap) · Head · Body · Legs · Accessory |
| Weapons | The weapon's **moveset is the combat style**; only the active weapon's stats apply |
| Stats | Weapons: Attack (+ poise damage). Armor: Defense, Poise. Accessories: special bonuses |
| Equipping | Click gear in the inventory or character screen; replaced gear returns to the bag |
| Starting gear (Knight) | Rusted Sword & Shield, Rusted Greatsword, Gambeson |

| Item | Slot | Stats | Value |
|---|---|---|---|
| Rusted Sword & Shield | Weapon (S&S) | — | 20 |
| Iron Sword & Shield | Weapon (S&S) | +15 Attack, +5 Defense | 140 |
| Rusted Greatsword | Weapon (GS) | — | 25 |
| Iron Greatsword | Weapon (GS) | +18 Attack, +10 Poise Damage | 170 |
| Padded Cap | Head | +4 Defense | 25 |
| Iron Helm | Head | +10 Defense, +5 Poise | 110 |
| Gambeson | Body | +10 Defense, +5 Poise | 60 |
| Chain Hauberk | Body | +22 Defense, +10 Poise | 220 |
| Leather Leggings | Legs | +5 Defense | 35 |
| Soldier's Token | Accessory | +15 Stamina | 90 |

### Open
- ⬜ Class restrictions on weapons · ⬜ Tiers beyond Iron · ✅ Crafting, part one (below) · ✅ Storage chest (below) · ✅ Item icons (placeholder) · ⬜ Hotbar

### Storage 🟡 *(Milestone 27)*
| Decision | Choice |
|---|---|
| Where | A **storage chest** beside the homestead bed: walk up, **E** → the chest window (pauses the game) |
| Size | **60 slots** (the bag has 30), same stacking rules |
| Moving | Bag on the left (Store / All), chest on the right (Take / All). **Store crops, seeds & materials** moves all of those in one go; food, gear and quest items stay with you. No pickup notices when moving |
| Crafting | The workbench uses ingredients from the bag first, then the chest (marked "(chest)" in the recipe list) |
| Saving | Saved with the game under its own ID (`storage.homestead`) |
| Later | ⬜ More chests (homestead upgrades) · ⬜ Sorting · ⬜ Drag and drop |

### Crafting 🟡 *(Milestone 26)*
| Decision | Choice |
|---|---|
| Where | The **workbench** beside your bed at the homestead: walk up, **E** → the Workbench window (pauses the game) |
| Kinds | **Alchemy** (draughts and tonics) · **Cooking** (meals) · **Smithing** (gear) — cards on the left show how many you can make right now |
| Ingredients | From the bag, then from gear you're wearing. Gear made from a piece you have equipped (same slot) **replaces it in place**, so an upgrade stays equipped |
| Batches | Food and potions: Craft or ×5. Gear: one at a time |
| Buffs | Some food gives a timed stat bonus (shown under the health bar with a countdown). Eating the same food again restarts it; different foods stack. Counts down only while playing; ends when you load a game |
| Data | Recipes are assets (**Create → Beast → Crafting → Recipe**) listed in `Data/Crafting/RecipeBook`. Add one, then *Rebuild Game Database* |

| Recipe | Kind | Needs | Makes |
|---|---|---|---|
| Healing Draught | Alchemy | 2 Healroot | Healing Draught |
| Stamina Tonic | Alchemy | 1 Healroot + 1 Turnip | Stamina Tonic |
| Roast Turnips | Cooking | 2 Turnip | Roast Turnips |
| Turnip Stew | Cooking | 2 Turnip + 1 Bread | Turnip Stew |
| Iron Sword & Shield | Smithing | Rusted Sword & Shield + 6 Iron Scrap + 2 Bandit Cloth | Iron Sword & Shield (upgrade) |
| Iron Greatsword | Smithing | Rusted Greatsword + 8 Iron Scrap + 2 Bandit Cloth | Iron Greatsword (upgrade) |
| Padded Cap | Smithing | 4 Bandit Cloth | Padded Cap |
| Iron Helm | Smithing | Padded Cap + 5 Iron Scrap | Iron Helm (upgrade) |

- ⬜ Crafting discipline (XP, recipes unlocked by level) · ⬜ Recipes found in the world · ⬜ Crafting animation · ⬜ More stations (forge, kitchen) as homestead upgrades

---

## 9. Homestead & Farming
**Status:** ✅ decisions · 🟡 prototype (Milestone 4b)

### Homestead
- Acquired after the prologue.
- **Fixed build slots** (barn, forge, fields, storehouse…) that are upgraded — no free-form editing in v1.
- Free-form land editing and buying more land → post-launch.
- **Bed:** sleep until 06:00, full heal + stamina, **autosave**.

### Farming
| Decision | Choice |
|---|---|
| Layout | Grid field of 1 m tiles (test field: 6×6) |
| Actions | One context button: **till → plant → water → harvest / clear dead**. **Hold** to repeat — work a whole row while walking |
| Seeds | Plants the selected seed; **Cycle Seeds** switches seed type |
| Growth | +1 growth for each day a tile **was watered**; soil dries overnight |
| Neglect | 2 dry days → **wilts** (stops growing, recovers when watered) · 3 dry days → **dies** (must be cleared) · ripe crops never spoil |
| Regrowth | Per crop: single harvest, or regrows after N watered days |
| Tools | None yet — implied hoe & watering can, unlimited water |
| Seeds from | Bandit loot, merchants; rare seeds from dungeons (planned) |

### Crops
| Crop | Growth | Yield | Regrows | Use |
|---|---|---|---|---|
| **Turnip** | 4 watered days (stages 1·1·2) | 1–2 | No | Food (heal 15), sells ~6g |
| **Healroot** | 6 watered days (stages 2·2·2) | 1–3 | Every 3 days | Draught ingredient, sells ~10g |

### Open
- ⬜ Seasons · ⬜ Tools, watering-can capacity, well · ⬜ Rain auto-watering (with weather) · ⬜ Soil quality / Blight contamination · ⬜ Livestock · ⬜ Crop raids / scarecrows · ⬜ Field expansion by clearing land

---

## 10. Economy & Shops
**Status:** ✅ decisions · 🟡 prototype (Milestone 4c)

### Pricing
| Price | Formula |
|---|---|
| **Buy** (player pays) | BaseValue × **1.25** (per-shop markup) × reputation modifier |
| **Sell** (player receives) | BaseValue × **0.5** (per-shop rate) × **demand** ÷ reputation modifier |

The reputation modifier comes from your standing tier with the Free Hollows (§17): 1 for a Stranger, down to 0.8 for a Hero of the Hollows, so better standing makes buying cheaper *and* selling more profitable. Buying always costs more than selling earns, so there is no buy-and-resell loop.

### Supply & Demand
- Each unit sold to a merchant lowers their demand for that item by **4%**, down to a floor of **40%**.
- Demand recovers **35% per day**.
- Tracked **per shop**, so different towns act as different markets.

### Shops
| Decision | Choice |
|---|---|
| Stock | Per-item daily stock (or unlimited); restocks every morning |
| What merchants buy | Per-shop category list (default: materials, crops, consumables, seeds); never quest items |
| Merchant gold | Unlimited (for now) |
| Trade screen | Pauses the game · Buy (Shift = ×5) · Sell 1 · Sell all · shows "market %" when saturated |

| Shop | Sells (daily stock) | Buys |
|---|---|---|
| **Oswin's Provisions** | Turnip Seeds 20 · Healroot Seeds 5 · Bread 10 · Healing Draught 3 | Materials, crops, consumables, seeds |
| **Brenna's Forge** (blacksmith) | Iron Sword & Shield 1 · Iron Greatsword 1 · Iron Helm 1 · Chain Hauberk 1 · Padded Cap 2 · Leather Leggings 2 · Soldier's Token 1 | Materials, equipment |

### Money Flow
| Faucets (money in) | Sinks (money out) |
|---|---|
| ✅ Enemy gold drops | ✅ Seeds |
| ✅ Selling crops & materials | ✅ Food & potions |
| ⬜ Contracts / quests | ✅ Gear (Brenna's Forge) · ⬜ repairs |
| | ⬜ Homestead upgrades |
| | ⬜ Taxes / tribute? |

### Open
- ⬜ Merchant gold limits · ⬜ Buyback · ⬜ Haggling · ⬜ Regional price differences · ⬜ Blight scarcity events raising food prices · ⬜ More merchants (blacksmith, herbalist)

---

## 11. Progression
**Status:** ✅ decisions · 🟡 prototype (Milestone 4d)

### Disciplines — "you level what you do"
Instead of one character level, each area of play levels separately from its own activity. Each Discipline has its own XP bar, level, stat growth and skill tree.

| Discipline | XP from | Each level gives | Skill tree |
|---|---|---|---|
| **Combat** | Enemy kills (per-enemy XP: Bandit 25, Dummy 2), successful parries (3) | +8 health, +2 poise, +2 stamina | Class skills: actives on E / Q + passives (§6) |
| **Farming** | Harvests (per-crop XP × units: Turnip 6, Healroot 8) | +1% extra harvest chance | Perks (below) |
| *Exploration* | *Later — discovery, chests, camps, climbing* | *Stamina, traversal* | *Later* |
| *Trade / Crafting* | *Later, once those systems exist* | — | — |

| Rule | Value |
|---|---|
| Level cap | 30 per Discipline |
| XP to next level | 100 × level^1.5 (100 → 283 → 520 → 800 → 1118…) |
| Points | 1 skill point per level, spent only in that Discipline's tree |
| **Renown** | Sum of all Discipline levels — **display only**, a sense of total progress |
| World difficulty | Set by region, **not** scaled to the player |

### Farming Perks
| Tier | Perk | Req. level | Effect |
|---|---|---|---|
| 1 | **Green Thumb** | 2 | +20% chance of +1 produce per harvest |
| 2 | **Deep Roots** | 3 | Crops tolerate 1 extra dry day before wilting / dying |
| 2 | **Seed Keeper** | 3 (needs Green Thumb) | 15% chance planting doesn't use the seed |

### Stats
Final stat = (class base + flat bonuses) × (1 + % bonuses), from Discipline levels, equipment (active weapon + armor), passive skills and timed buffs.

| Stat | Meaning |
|---|---|
| Health, Poise, Stamina, Stamina Regen | Survivability |
| Attack (100 = normal) | Damage dealt × Attack / 100 |
| Poise Damage (100 = normal) | Stagger power |
| Defense | Damage taken × 100 / (100 + Defense) — 100 Defense = half damage |
| Stamina on Parry | Restored on a perfect parry |
| Extra Harvest Chance · Drought Tolerance · Seed Saving Chance | Farming |

### Open
- ⬜ Exploration Discipline (with climbing & real levels) · ⬜ Trade / Crafting Disciplines · ⬜ Respec · ⬜ Skills beyond tier 3 · ⬜ Homestead upgrade tree

---

## 12. Time, Day/Night & Pressure
**Status:** ✅ design · 🟡 partly built

| Rule | Status |
|---|---|
| **One in-game day = 20 real minutes**; game starts at 08:00 | ✅ Built |
| Time pauses in menus, pause and loading | ✅ Built |
| Sleeping skips to 06:00; every skipped day still ticks crops and shops | ✅ Built |
| Crops need daily watering; neglect → wilt (2 days) → death (3 days) | ✅ Built |
| Shops restock and markets recover each morning | ✅ Built |
| **Fatigue:** sleep restores; staying up gives stat penalties (not death) | 🟡 Built (Milestone 15) |
| **Nights:** tougher enemies, better loot | 🟡 Built (Milestone 15) |
| **Homestead raids:** telegraphed a day ahead (rumours, smoke); defend or lose some stored goods — a setback, never a game over | ⬜ |
| Day/night lighting, weather (rain waters crops) | 🟡 Built (Milestone 14, awaiting test) |
| Optional "harsh world" difficulty that raises pressure | ⬜ |

### Day, Night & Weather (Milestone 14)
| Part | How it works |
|---|---|
| **Sun** | Rises at 05:30, arcs east to west (up to 55° high), sets at 20:00; dawn and dusk take 1.5 h and tint the light orange. Midday on a clear day looks exactly like the original environment art |
| **Night** | The sun becomes a dim blue **moon** (still casting soft shadows); ambient light, fog and the sky darken to deep blue. Dark but readable |
| **Lights** | Lanterns beside every house door and at the contracts board, and a **campfire at the bandit camp** (visible from afar at night), fade in at dusk with a gentle flicker |
| **Weather** | Each day is **Clear**, **Cloudy** (≈25%) or **Rain** (≈20%), rolled from a fixed seed (same days, same weather; day 1 always clear). Clouds dim and soften the sun; rain also greys the sky, pulls the fog in to 70 m and falls as streaks around the camera |
| **Rain waters crops** | Every tilled tile counts as watered on a rainy day (also soil worked during rain) — "It's raining: the field is watered today." |
| **HUD** | The clock shows a sun, moon, cloud or rain icon |
| Saved | The current weather |
| Debug | F4 cycles the weather |

### Night Danger & Fatigue (Milestone 15)
| Part | How it works |
|---|---|
| **Bolder at night** | While it's night (roughly 19:15–06:15), enemies hit **+30%** harder, spot you from **+40%** further away and move **15%** faster. Their name plates turn red with a moon |
| **Better loot at night** | A night kill rolls the loot table **twice** and drops **+50% gold** |
| **Warnings** | "Night falls. Bandits are bolder now, and carry more." / "Dawn breaks." |
| **Fatigue** | Hours awake count up with the clock (a new game starts 2 h after waking). **Tired** at 18 h (≈ midnight if you rose at 06:00): −15% stamina, −20% stamina regen. **Exhausted** at 22 h: −30% stamina, −40% regen, −15% attack. A TIRED / EXHAUSTED badge sits above the health bar; the character screen shows the lowered stats. Sleeping clears it. Never kills you |
| Tuning | Night settings per enemy type on EnemyData; fatigue thresholds and penalties on the player's Player Fatigue |
| Saved | Hours awake |

The loop this creates: night is when fights pay best, but they're riskier and every hour awake wears you down, so you plan when to go out and when to head home to bed.

---

## 13. Traversal
**Status:** 🟡

| Ability | Status |
|---|---|
| Walk / run (camera-relative), sprint (hold), jump | ✅ Built |
| Dodge (tap) with i-frames | ✅ Built |
| **Climbing & ledge grab** — grab and hang on ledges (shimmy, pull up, drop), climb walls / marked surfaces with stamina drain | 🟡 Built (Milestone 11, awaiting test) |

### Climbing & Ledges (Milestone 11)
| Move | How | Stamina |
|---|---|---|
| **Vault** | Jump toward an edge 0.9–2.4 m above your feet that you could stand on from the ground → pulled straight up (quick) | — |
| **Ledge grab** | Jump (or fall) toward a taller wall while moving at it: your hands catch its top edge if they can reach it (walls up to ~3.6 m from flat ground) | — |
| Hang | Hands on the edge | 3 / s |
| Shimmy | A / D (stick sideways) — stops where the edge ends or something is in the way | 6 / s |
| Pull up | Space — only if there's room to stand on top ("No room to climb up" otherwise) | — |
| Leap off | Space while holding away from the wall | — |
| Let go | Shift (B / Circle) | — |
| **Wall climb** | Walls marked **climbable** (ivy texture): Space at the wall, or run into it mid-air. W/S up and down, A/D sideways; reach the top to pull up, touch the floor to step off | 2 / s still · 9 / s moving |

- **Out of stamina → you fall** ("Too tired to hold on"). **Taking a hit knocks you off.**
- **Climb down (Milestone 24):** stand still at a top edge, facing the drop → "[Space] Climb down" → Space lowers you into a hang (or onto the wall, if it's ivy). Edges too low to hang from (your feet would touch the ground) show no prompt: just walk off.
- **Fall damage (Milestone 24):** drops up to **6 m** are harmless; above that you lose health in proportion, up to all of it at **18 m** (10 m ≈ a third). Measured from the top of the fall, so a jump's arc counts; grabbing a ledge or wall resets it. Blocking, dodging and Defense don't reduce it. A hard landing stuns you briefly (0.35–1 s), shakes the camera and thuds. Teleports, respawns and loading a save never count as falls.
- While climbing you can't attack, block, dodge, use items or interact; lock-on is released.
- **On-screen help:** the prompt spot shows the controls while hanging or climbing, and "[Space] Climb" when facing an ivy wall.
- **Level-design rules:** any solid, flat-topped edge is grabbable — mark things that shouldn't be with the **Not Climbable** component (the greybox houses are, because their roofs have no collider). Climbable walls get a **Climbable Surface** component *and* must look different (ivy). Tops need ~0.7 m of standing room. Edges are found automatically (no hand-placed ledge markers).
- **Animation:** two new clips, **Hang** (2 frames, sways) and **Climb** (4 frames, alternating reach; frozen while holding still). The placeholder generator draws them; real sheets need the same clips.
- **Practice course** (west of the village): a 1.4 m and a 2.3 m block to vault, a 3.2 m × 9 m ledge wall to hang from and shimmy along, a 6 m ivy cliff behind it, and (Milestone 24) a 10 m ivy tower south of the cliff for testing fall damage.
| Fast travel · Mounts | ⬜ Open |

---

## 14. Controls
**Status:** 🟡 (prototype bindings; full rebinding later)

| Action | Keyboard / Mouse | Gamepad |
|---|---|---|
| Move | WASD | Left stick |
| Camera | Mouse | Right stick |
| Jump | Space | A / Cross |
| Grab a ledge / climb ivy | Jump toward it (Space at an ivy wall) · Space: climb up · A/D: shimmy · Shift: let go | A · stick · B |
| Light attack | LMB | X / Square |
| Heavy (charged) attack | **Hold** LMB | Y / Triangle |
| Active skill 1 / 2 | **E / Q** | RB / RT |
| Block (tap = parry) | RMB | LB |
| Dodge (tap) / Sprint (hold) | Shift | B / Circle |
| Lock-on | Middle mouse | R3 |
| Swap weapon (= combat style) | X | D-pad → |
| Interact (hold = repeat) | **F** | D-pad ↑ |
| Quick-use food / potion | R | D-pad ↓ |
| Cycle seeds | **V** | D-pad ← |
| Inventory (pauses) | Tab / I | Select (View) |
| Character screen (pauses) | **C** | Select, then LB / RB |
| Journal (pauses) | **J** | Select, then LB / RB |
| Switch tracked quest | **T** | L3 |
| Large map (toggle) | **M** | Select, then LB / RB |
| Dialogue: continue / pick choice | Space / Enter / F / click · 1–9 · ↑↓ | A · D-pad |
| Pause | Esc | Start |
| Close menus | Tab / I / C / J / Esc | Select / Start / B |
| **Menus (Milestone 35)** | Mouse · or arrow keys + Enter | D-pad / left stick to move the focus · A to press · left / right to change a slider or stepper · B to go back · LB / RB to switch tabs |

**Controller navigation (Milestone 35):** every menu works with a pad (and the arrow keys): an ink-gold frame shows the focused control; the stick or D-pad moves it to the nearest control in that direction; A presses it; left / right change sliders and ‹ › steppers in place; lists scroll to keep the focus in view; B backs out (as Esc). In the bag, the focused slot shows its details and A uses or equips it. The first press only shows where the focus is; moving the mouse hides it. While a pad is in use, key hints and key caps show pad buttons (A, B, RB, RT, ↑ ↓ →, View, Start, LB / RB). The name field can't be typed on a pad: **Random** suggests a name. Dialogue already worked with a pad.

Genshin-style layout (skills on E / Q, interact on F) adopted in Milestone 4d.

**Debug keys (development builds):** F1 +1 day · F2 +1 hour · F3 hide overlay · F5 save · F9 load · **F4 cycle weather · F6 +100 Combat XP · F7 +100 Farming XP · F8 +50 standing**.

---

# Part III — World & Narrative

## 15. World & Story
**Status:** 🟡

### Premise
Two kingdoms fought a long war with increasingly desperate magic. The final battle unleashed a **war-sorcery that went wrong** — it ended the war and poisoned the land. This is **the Blight**: soil turns grey, animals twist into monsters, rivers sour.

The frontier is now lawless. The old crown is broken, deserters turn bandit, and people starve because almost nothing grows. Magic is everywhere — but feared and fought over.

### Why the Homestead Matters
The player's farm sits on one of the few patches of **soil the Blight couldn't touch** — and nobody knows why. Growing food there is power, hope, and a target. This drives raids, economic stakes and the central mystery.

### Magic
High fantasy — magic is common and flashy, but tied to the war and the Blight: mistrusted, weaponised, and scarce in safe forms.

### Region (v1)
| Location | Status |
|---|---|
| The player's homestead | ✅ Designed (greybox field, bed, merchant nearby) |
| Hub town | ⬜ Name TBD |
| Blighted wilds (2–3 sub-areas) | ⬜ |
| Dungeons (2–3) | ⬜ |

### Prologue (30–45 min, doubles as the tutorial)
1. **Cold open — the final battle.** Class combat tutorial; ends in the war-sorcery cutscene *(the biggest cutscene in the game)*.
2. **Aftermath.** Wake in a blighted wasteland; first twisted creatures (teaches dodging and reading tells).
3. **The road.** A survivor NPC guides you through bandit country (dialogue, looting, the harsh economy).
4. **The farm.** Find a green homestead held by bandits → boss fight to reclaim it.
5. **Hook.** That night, something in the soil reacts to *you*. Farming unlocks. Prologue ends.

### Main Story Arc
⬜ Acts, major beats and ending(s) — see §25.

### Key NPCs
| NPC | Role | Status |
|---|---|---|
| **Oswin** | Merchant, "Oswin's Provisions" (seeds, food, potions; buys your goods). Warm, tired, practical; first source of lore | 🟡 In game (placeholder, with dialogue) |
| **Brenna** | Blacksmith, "Brenna's Forge" (weapons & armor; buys scrap and gear). Blunt, guarded; first to notice the ash fears your Healroot | 🟡 In game (placeholder, with dialogue) |
| Survivor guide | Prologue companion | ⬜ |
| Rival | ⬜ | ⬜ |
| Antagonist | The Ashen leader? | ⬜ |

### Daily Routines 🟡 *(Milestone 31)*
NPCs keep a day: they walk the village (along the navigation mesh, at a stroll) between places at set hours, face the way each place faces, stop walking while you stand next to them (so you can talk), and go home to sleep — hidden indoors, with no talking, trading or markers until morning. After sleeping, loading or skipping time they're simply where they should be. Not saved (it follows from the clock).

| Time | Oswin | Brenna |
|---|---|---|
| 06:00 | At the stall | At the forge |
| 12:00 | Lunch at the well | — |
| 13:00 | At the stall | Reading the contract board |
| 14:00 | — | At the forge |
| 19:00 / 19:30 | Evening by the well | Evening by the well |
| 21:30 / 22:30 | Asleep at home | Asleep at home |

- Shops follow the people: you can trade with Oswin wherever he is while he's up; at night the shops are shut.
- **Level design:** the places are empty objects under `[NPC Places]` (move them to change where people go); each NPC's **Npc Schedule** lists start hour, place, activity and whether it's indoors (the place is then the door).
- ⬜ Shelter from rain · ⬜ "Closed" signs at night · ⬜ More townsfolk · ⬜ Schedules that change with quests and standing

---

## 16. Protagonist
**Status:** 🟡

**Semi-fixed protagonist.**
- **Fixed:** backstory — a soldier who fought in the final battle and **survived the blast when no one around them did**, marked by it somehow.
- **Player chooses:** name, appearance, class.
- 🟡 **Character creator (Milestone 19):** New Game → pick a slot → create your hero: **hair** (short, long, ponytail, bun, shaved), **hair colour** (8), **skin** tone (6) and **outfit** (5), with a live, turnable preview of the in-game sprite (also with the greatsword) and a Randomise button. The look is saved in the slot.
- 🟡 **Name (Milestone 28):** a Name field at the top of the creator (letters, spaces, apostrophes and hyphens; up to 16). Left empty, you're "Wanderer". The name shows above the health bar, under your figure on the Character screen, first on the save slot card, and to Ink as `player_name()`.
- 🟡 **The mirror (Milestone 28):** a standing mirror at the homestead: **E → Change your look** opens the same controls (name, hair, colours, outfit) on your current look, previewed with the weapon you hold. **Keep this look** applies it ("You are now …" when renamed); Cancel or Esc changes nothing. Free. ⬜ A barber in town (paid, more styles).
- **Class = the unit they served in**, so cutscenes share one script with small class-specific lines.

Open questions: see §25.

---

## 17. Factions & Reputation
**Status:** 🟡

### Factions (names are working titles)
| Faction | Who | Wants | Relationship to player |
|---|---|---|---|
| **The Remnant Crown** | Surviving nobles & soldiers | Restore order, by force if needed | Buys your food; may try to seize the farm |
| **The Free Hollows** | Villagers & refugees | Survival, fair trade | Main market — **v1 reputation target** |
| **The Ashen** | Blight-worshipping cult / rogue mages | Spread the Blight as "purification" | Antagonists; drive raids |

### Reputation (v1 — light) 🟡 *(built — Milestone 10, awaiting test)*
One **standing value** (0–500) with the Free Hollows. Tuned in `Data/Reputation/ReputationConfig`.

| Tier | Standing | Prices | Also |
|---|---|---|---|
| Stranger | 0 | Standard | — |
| Known | 50 | 5% better | — |
| Trusted | 150 | 10% better | **Town Patrol** contract unlocks · Oswin thanks you (bread + a draught) |
| Friend | 300 | 15% better | Brenna gives you an **Iron Helm** |
| Hero of the Hollows | 500 | 20% better | The refugees tell stories about you |

| Source | Amount | Limit |
|---|---|---|
| Finishing quests | Story 40 · side 20–25 · contracts 8–10 · Town Patrol 20 (`StandingReward` on each quest) | — |
| Protecting the region | +2 per Bandit (`StandingReward` on each enemy type) | 20 per in-game day |
| Trading with town shops | +1 per 25 gold bought or sold | 15 per in-game day |
| Abandoning town work | −5 (only quests that would have raised standing) | Can't go below 0 |
| Dialogue | `change_standing(n)` from Ink | — |

- **Quest gating:** any quest can require a tier (`RequiredTier`). Locked contracts show **LOCKED · Requires Trusted standing** on the board.
- **Where you see it:** a standing card in the journal (tier, progress to the next tier, what your tier gives) · the shop header ("Standing: Trusted (10% better prices)") · the character screen header · a **+N Standing** note in the feed · a **STANDING RAISED / LOWERED** banner when your tier changes · the journal warns how much standing abandoning costs.
- **Dialogue:** Ink can read `standing()` and `standing_tier()`. Ask Oswin *"How does the town see me?"* for an in-world explanation of what raises standing.
- **Why daily caps:** bandits respawn in the test world and selling is unlimited; caps keep standing a reflection of time spent helping, not grinding.
- Full multi-faction reputation (rivalries, hostility, land rights) → post-launch.

---

## 17b. Dialogue & Quests
**Status:** ✅ decisions · 🟡 prototype (Milestone 4e)

### Dialogue
| Decision | Choice |
|---|---|
| Writing tool | **Ink** (inkle's narrative scripting language; official Ink Unity package) |
| Structure | One master story `Dialogue/Main.ink` that INCLUDEs one file per NPC; each conversation is a **knot** (`=== oswin ===`). Shared variables and visit counts, so NPCs remember past conversations |
| Speaker lines | `Oswin: Hello.` (name matched to a Speaker asset for name + portrait) or a `# speaker: Oswin` tag; lines without a name are narration |
| Presentation | **Bottom dialogue box + portrait**, typewriter text, choices (mouse, ↑/↓ + confirm, or number keys). The game pauses during conversations; Esc leaves |
| Ink → game | `quest_state`, `start_quest`, `turn_in_quest`, `complete_objective`, `has_item`, `take_item`, `give_item`, `give_gold`, `gold`, `discipline_level`, `open_shop` (see `Externals.ink`) |
| Shops | Merchants are talked to first; "I'd like to trade" opens their shop |
| Saving | Ink story state (variables, visit counts) is saved with the game |
| Compiling | **Beast → Dialogue → Compile Ink Story** (also done by the Milestone 8 setup); the Ink package also auto-compiles on save |

### Quests
| Decision | Choice |
|---|---|
| Types | **Story** (main arc, multi-step via dialogue) · **Side** (from NPCs) · **Contract** (repeatable jobs from a board, once per in-game day) |
| Objectives | **Kill** (enemy type or any) · **Collect** (items held; taken on turn-in) · **Harvest** (crop type or any) · **Custom** (ticked from dialogue) |
| Rewards | Gold, items (dropped at your feet if the bag is full), Combat / Farming XP, **standing** with the Free Hollows |
| Flow | Inactive → Active → Ready (all objectives done) → Completed (on turn-in). Quests can require earlier quests and a standing tier. Optional auto-complete |
| **Offers** | **Every quest offer gives an Accept and a Decline choice.** Declining keeps the offer available (ask again from the NPC's menu; the "!" marker stays). Quests never start without asking — story quests included |
| UI | **Tracked quest** panel under the minimap (full objectives + where to go + distance; other active quests listed dimmed) · **on-screen waypoint ◆** with distance (pinned to the screen edge when off-screen) · **banners**: QUEST ACCEPTED / OBJECTIVES COMPLETE / QUEST COMPLETE · **"!" / "?" markers** over NPCs · contracts board with status badges + progress bars |
| Tracking (focus) | **One tracked quest** drives the tracker panel, waypoint and minimap marker. A newly accepted quest is tracked if nothing else is. Change it with **Track** (journal / board) or **T** (cycles story → side → contracts). Saved with the game |
| Waypoint targets | Worked out live: ready → the NPC / board that takes it · Kill → nearest matching enemy · Harvest or crop to collect → the field · loot to collect → nearest enemy that drops it · talk objectives → the quest's NPC |
| Journal (J) | Filter tabs **All / Story / Side / Contracts / Completed** with counts · quest list (◆ = tracked, status, progress) · details pane (summary, objectives, rewards, where to go + distance, progress bar) · **Track** · **Abandon** (side quests and contracts only, confirmation required; story quests can't be abandoned) |

### Current Quests
| Quest | Type | Giver → Turn in | Objectives | Rewards |
|---|---|---|---|---|
| **Bandit Trouble** | Story | Oswin → Oswin | Defeat 3 Bandits | 50g, 60 Combat XP, +40 standing |
| **Roots of the Blight** | Story (after Bandit Trouble) | Oswin → Brenna | Hear Brenna out · Bring 1 Healroot | 30g, 30 Combat XP, 40 Farming XP, +40 standing |
| **A Taste of Home** | Side | Oswin → Oswin | Bring 5 Turnips | 40g, 3 Healroot Seeds, 30 Farming XP, +25 standing |
| **Scrap Run** | Side | Brenna → Brenna | Bring 4 Iron Scrap | 60g, 40 Combat XP, +20 standing |
| **Bounty: Road Bandits** | Contract (daily) | Board | Defeat 3 Bandits | 45g, 40 Combat XP, +10 standing |
| **Supply Run: Turnips** | Contract (daily) | Board | Deliver 6 Turnips | 50g, 20 Farming XP, +10 standing |
| **Cloth for Bandages** | Contract (daily) | Board | Deliver 5 Bandit Cloth | 25g, 10 Combat XP, +8 standing |
| **Town Patrol** | Contract (daily) · needs **Trusted** | Board | Defeat 5 Bandits | 90g, 70 Combat XP, +20 standing |

**Story beat introduced:** Brenna's quench-barrel ash *crawls toward growing things* — and **recoils from Healroot grown on your field**. The homestead's soil isn't just untouched by the Blight; it fights it.

### Open
- ⬜ Voice/barks (speech bubbles) · ⬜ Portrait art & expressions (`# portrait:` tag) · ⬜ Quest failure / timed quests · ⬜ Branching quest outcomes · ⬜ Contract pool rotation (random daily selection) · ⬜ Prologue quest chain (needs real levels)

---

# Part IV — Presentation

## 18. Visual Style & Sprite Technology
**Status:** ✅ tech · ⬜ final art look *(art production on hold)*

### Direction
| Decision | Choice |
|---|---|
| Characters & enemies | 2D **8-directional sprites** (5 drawn, 3 mirrored) |
| World | 3D |
| Camera | Free third-person orbit with collision (custom; can swap to Cinemachine later) |
| Art look | ⬜ Pixel art vs painted/HD — decide after the real-art style test |
| Recommended pipeline | **3D → 2D pre-render**: model & animate in Blender, script-render the 5 angles, pixelate or cel-shade. Alternative: hand-drawn pixel art in Aseprite (~32–64 px tall) |
| Post-processing | Bloom, depth of field, colour grading — key to the HD-2D look |
| Cutscenes | In-engine dialogue scenes for most; 2–3 fully staged Timeline cutscenes |

### Sprite Technology (built — Milestone 3)
| Feature | How it works |
|---|---|
| Sheet format | Grid: **rows = directions** (front, front-right, right, back-right, back), **columns = frames**. Matches Blender render scripts and Aseprite exports |
| Billboarding | Sprites stay upright facing the camera (optional tilt with camera pitch) |
| Direction choice | Picked from the character's facing relative to the camera; left views mirror right views |
| Shadows | A hidden **sun-facing shadow quad** per character, so shadows never go paper-thin |
| Shading | **Per-pixel shadows** (Milestone 12): the **Beast/Sprite Lit** shader receives shadows from buildings, trees and other characters — a wall's shadow line crosses a character at the right height, a sword's shadow shows on whoever it falls on — but never the sprite's own shadow quad. It samples the shadow map from a point moved toward the sun (`Self-Shadow Offset`: characters 1.2 m, oaks 3 m, pines 2.2 m, dead trees 2.6 m, bushes 1.2 m, grass 0.3 m), so anything closer than that along the sun ray is ignored. Trees, bushes and grass use it too. Sprites on plain URP/Lit fall back to the old 3-ray darkening |
| Lighting | Lighting normals bent upward so sprites stay evenly lit from any angle |
| Animation sync | Attack clips stretch their wind-up / strike / recovery frames to the attack's real timings |
| Feedback | Hit / parry / stagger / telegraph flashes via emission glow |
| Crops | Upright billboards, one strip per crop: growth stages, ripe, wilted, dead |
| Placeholder art | Auto-generated pixel figures (Knight, Bandit, Dummy, Merchant) and crops, in the final layout |
| Player look (Milestone 19) | The player's sheet is drawn **at runtime** from the creator's look and the **weapon in use**: sword &amp; shield (one-handed blade, shield on the off arm) or **greatsword** (long, broad two-handed blade, both hands on the grip, no shield). Swapping weapons swaps the look at once. Each combat style says which look it uses (`MovesetData.Look`). A real-art sheet (any other size) is left alone, so real art replaces this without code changes |

### Environment Art (placeholder — built in the polish pass)
Generated by **Beast → Setup → Run Milestone 9 Setup (Environment Art)**. It matches the characters' chunky pixel-art look so the greybox reads as a place.
| Element | How it's made |
|---|---|
| Textures | Seamless 64 px pixel-art tiles drawn by code with dithered palettes: grass, dirt, tilled soil (furrows), cobbles, stone wall, timber-framed plaster, planks, roof tiles, rock; plus door, window, notice. Written once as PNGs (`Art/Environment/Textures`) — edit or replace them freely; the setup keeps your versions |
| Buildings | Greybox boxes keep their colliders but get world-scaled UVs (textures never stretch), gable roofs with overhang, a chimney, a door facing the village square, windows and a doorstep; the tower gets battlements |
| Vegetation | Billboard sprites like the characters (oak, pine, **blighted dead tree**, bush, grass tuft, flowers) that face the camera, with a separate sun-facing shadow quad. Trees have trunk colliders and a flat canopy that only the minimap draws |
| Layout | Cobbled village square that bends around the field; dirt roads to every door, the field gate, the ramp and the steps; a wide road out past the bandits; a field fence with a gate; well, crates, barrels and an anvil; a forest ring closing the map edge; rocks and ground cover. Gameplay spaces (NPCs, doors, field, roads, fights) are kept clear automatically |
| Story in the art | Dead, blighted trees become more common toward the bandit side — the war-poisoned land creeping in |
| Atmosphere | Trilight ambient light, warm sun, linear distance fog (the minimap camera stays inside the fog-free range) |

### Next Art Step
- ⬜ **Real-art style test:** one real character (idle + walk, 5 directions) → measure time per animation → estimate the full art budget.

---

## 19. Audio
**Status:** 🟡 placeholder sound (Milestone 20) and music (Milestone 33)
- ⬜ Music direction (orchestral? folk? dark ambient?). The placeholders lean **medieval folk**: modal tunes on lute, harp, bowed drone, flute and frame drum
- 🟡 **Placeholder music** (Milestone 33), synthesised into `Audio/Placeholder/Music_*.wav` and listed in the SoundLibrary (swap in real tracks there):

| Track | When | Feel |
|---|---|---|
| Title | Main menu | D Dorian, slow; drone, harp arpeggios, a flute line (60 s loop) |
| Day | Playing, 06:00–20:00 | G major; lute bass and strums, flute and harp tunes (60 s loop) |
| Night | Playing, 20:00–06:00 | A minor; a low pad and sparse harp (51 s loop) |
| Combat | A (non-dummy) enemy chasing or attacking within 20 m; holds 5 s after | D minor, fast; frame drum, bowed ostinato, plucked stabs (29 s loop) |

  - Tracks crossfade (combat faster); the music dips to 60% while a menu or the pause screen is open, and fades out while loading
- ⬜ Voice: none / grunts / partial / full (grunts recommended for solo scope)
- 🟡 **Placeholder sounds**, synthesised by the Milestone 20 setup into `Audio/Placeholder` (WAV) and listed in `Resources/SoundLibrary` — swap in real clips there, no code changes:
  - **Combat:** swing (heavier for the greatsword), hit, block clang, guard break, parry ring, death, dodge
  - **Movement:** footsteps on the run cycle's contact frames (player and enemies, 3D), climbing grabs
  - **Items & farming:** pickup, coins (gold in or out), eat / drink, till, plant, water, harvest
  - **Interface:** button clicks; quest accepted / ready / complete, level up, sleep chimes
  - **Ambience:** day (wind + birds) and night (wind + crickets) loops crossfaded at dusk (19–21) and dawn (5–7); rain over them; a quiet wind on the title screen
- Sounds play from events and sprite frames (`GameAudio`), so new characters and actions get sound for free
- Volumes (Settings → Audio): **Master** (listener), **Music**, **Sound effects**, **Ambience**, **Interface sounds**

---

## 20. UI / UX

> **Restyle done (Milestone 21):** the UI is moving to a Ghost of Tsushima–inspired ink-and-parchment look.
> - Spec: `Docs/superpowers/specs/2026-10-07-tsushima-ui-design.md`. Plan: `Docs/superpowers/plans/2026-10-07-tsushima-ui.md`.
> - Step 1 is done: palette, brush textures, the Cormorant Garamond and Alegreya Sans fonts (OFL, in `Resources/UI/Fonts`), and the title screen.
> - Step 2 is done: the **game menu**, a parchment screen with an ink tab bar (Map · Journal · Bag · Character · Options).
>   - M / J / Tab or I / C open their tab. Esc opens Options (the pause). Q / E switch tabs. A tab's own key closes the menu.
>   - Options has Controls / Display / Audio / Interface tiles (settings shown inline) and the Resume / Save / Load / Quit buttons.
>   - Settings are restyled on parchment, on the title screen too.
> - Step 3 is done: the tabs' own content.
>   - **Bag:** category cards, paper slots, an ink details panel.
>   - **Character:** Combat / Farming / Gear cards. Skills show as a tree of diamond nodes, one row per tier. Gear shows your figure among the six equipment slots.
>   - **Journal:** filter cards with a town-standing card, the quest list, an ink details panel with Track / Abandon.
> - Step 4 is done:
>   - **Shop and contract board:** parchment windows.
>   - **Dialogue:** an ink band across the bottom, the speaker's name in vermilion capitals, choices as paper rows.
>   - **HUD:** see-through ink panels, key caps, a thin vermilion health bar, the quest banner in spaced capitals, and a thin ink frame for the minimap.
> - Later: a UI Toolkit port with controller navigation, and painted art for the cards and tiles.
**Status:** 🟡 *(prototype layer on a shared theme; final UI tech and art not started)*

### UX Principles (applied in the polish pass)
| Principle | How the prototype follows it |
|---|---|
| **One visual language** | A single theme (`UITheme`): dark leather panels with aged-gold borders, parchment text, one type scale, and one set of buttons, tabs, badges, bars and icons for every screen |
| **Resolution independence** | Everything is laid out on a virtual screen 1080 units tall and scaled, so 720p, 1080p and 4K look the same |
| **Readable over the world** | HUD text has drop shadows; muted text is still high-contrast; menus dim the game behind them |
| **Clear states** | Hover highlights, faded disabled buttons, a gold frame on selected rows and slots, status badges (AVAILABLE / IN PROGRESS / READY / DONE) |
| **Feedback for every action** | Trades, item use and equips say what happened (green) or why not (red); pickups, gold and XP slide into a feed; quest banners; sleeping fades to the new day; death shows DEFEATED with a countdown |
| **Safe destructive actions** | Abandoning a quest and quitting ask to confirm; only Esc leaves a conversation (menu hotkeys don't) |
| **Discoverability** | Key chips everywhere ([F] prompts, HUD key hints, footer hints) and a full Controls list in the pause menu |
| **Consistent exits** | Every menu closes with its ✕ button, Esc, or the key that opened it |

### HUD Layout
| Area | Content |
|---|---|
| Top right | Minimap (M = large map with legend) → day, time and gold → tracked quest (T switches) |
| Bottom centre | Health (with number; pulses red when low), stamina, poise; class · style |
| Bottom right | Action slots **E / Q** (skills; cooldown shade + seconds) · **R** (food, count) · **X** (weapon in hand) |
| Bottom left | Menu key hints (Esc, Tab, C, J, M) |
| Left | Notification feed |
| Centre | Interaction prompt ([F] Action), waypoint, NPC "!" / "?", enemy name plates, damage numbers |
| Full screen | Quest banners, DEFEATED overlay, sleep fade |

### Screens
| Screen | Status |
|---|---|
| **Main menu** (title screen): Continue (most recent save, with its slot, day and play time), New Game (choose a slot; an occupied slot asks before it's overwritten), Load Game (slot cards with day, time, gold, levels, standing, play time and save date; Delete with confirm), Quit. Dusk backdrop drawn in code | 🟡 Mouse |
| Pause menu (Esc): Resume, Save, Load last save (with its time), Controls, Quit to main menu and Quit to desktop (both confirmed); the title shows the slot | 🟡 |
| Inventory (Tab / I): slot grid with count badges, details panel, Use / Equip, right-click shortcut, tooltips | 🟡 Mouse |
| Trade: Buy / ×5 / Sell / All, red prices when unaffordable, market %, result line | 🟡 Mouse |
| Character (C): Discipline cards with XP, skill list (Learn / Set E·Q), equipment (two-line rows), gear in bag, stats grid | 🟡 Mouse |
| Journal (J): tabs with counts, list, details (objectives, reward, where to go), Track, Abandon (confirm) | 🟡 Mouse |
| Contracts board: badges, progress bars, Accept / Turn in / Track | 🟡 Mouse |
| Dialogue: name plate, framed portrait, typewriter text, numbered choice rows, blinking continue arrow | 🟡 |
| Dev line (state, time, debug keys) | 🟡 Dev builds only |

### To Design
- ⬜ Final UI tech (UI Toolkit) & art · ✅ Controller navigation (Milestone 35, in the IMGUI screens) · ✅ Item icons (placeholder) · ⬜ Main menu art · ⬜ Key rebinding · ⬜ Pad button glyphs (icons instead of letters)
- ⬜ Accessibility: rebinding, subtitles, colour-blind options (interface size ✅ in Settings)

### Settings (Milestone 17)
Opened from the main menu and the pause menu. Every change applies at once and is saved automatically to `settings.json` (shared by all save slots, not part of a save). **Reset to defaults** asks first.

| Tab | Options |
|---|---|
| Controls | Mouse sensitivity (0.25–3×) · Controller look speed (0.25–3×) · Invert vertical look |
| Display | Window mode (Borderless / Fullscreen / Windowed) · Resolution · VSync · Frame rate limit (30 / 60 / 120 / 144 / none; off while VSync is on) · Render scale (50–100%) · Shadows (Off / Low / Medium / High) · Anti-aliasing (Off / 2× / 4× / 8×) |
| Audio | Master · Music · Sound effects (the game has no sound yet; master already sets the listener volume) |
| Interface | Interface size (80–120%) · Damage numbers on/off (PARRY! and GUARD BREAK always show) · Camera shake (0–100%) |

- Window mode and resolution changes show "Keep these display settings? Reverting in 12 s" (Keep / Revert) and undo themselves if not kept. Unity remembers the window itself, so these aren't in settings.json.
- Graphics options change a **runtime copy** of the URP asset, so playing in the Editor never alters the project's asset; the original is restored when Play stops.
- Shadows High = the project's own URP settings (50 m, 2048, 4 cascades); Medium 40 m / 2048 / 2; Low 30 m / 1024 / 1; Off = no shadows (sprites also stop darkening in shade).
- In the pause menu, Esc closes Settings first, then a second Esc resumes.

---

# Part V — Technical

## 21. Architecture
**Status:** ✅ built (Milestones 1–4c)

### Principles
- **Bootstrapped services:** core systems are created once before any scene loads, so Play works from any scene.
- **Service locator** instead of singletons; **event bus** so systems never reference each other directly.
- **Data-driven:** all content (attacks, classes, enemies, items, crops, shops, sprite sheets) is ScriptableObject data with stable IDs.
- **Save by ID:** saves store IDs and plain values, never object references; versioned for future migrations.
- **Fast iteration:** code split into assemblies (Core / Gameplay / Editor); Enter Play Mode without domain reload (statics reset automatically).

### Core Services (created at boot)
| Service | Responsibility |
|---|---|
| InputService | Action maps per game state (Player / UI / Cutscene); cursor lock |
| GameStateService | Boot · MainMenu · Loading · Playing · Paused · Cutscene · **InGameMenu**; time scale |
| HitStop | Brief time freeze on impact; safe with pausing |
| SceneLoader | Async scene loading (Loading → Playing, or → MainMenu); knows the first world scene and the main menu |
| SettingsService | Player options (settings.json): loads at boot, applies graphics/VSync/frame cap/volume, saves shortly after each change, display-change confirm |
| SaveService | **3 save slots**, one playthrough each (the active slot is what sleeping, the pause menu and F5/F9 use); slot summaries for the menu; play time; New Game; delete; atomic writes + backup, version number (now 2, old saves still load), restore-on-register |
| WorldClock | Game time, hour and day events, skip-to-hour |
| GameDatabase | ID → content lookup for every data asset |
| DebugOverlay | Dev-only HUD and debug keys |

### Events (publish / subscribe)
| Event | Raised by | Used by (now / planned) |
|---|---|---|
| GameStateChanged | Game state | Input, HitStop, menus |
| HourChanged / **DayPassed** | World clock | Farming, shops (✅) · raids (planned). NPC schedules read the clock directly |
| DamageDealt | Combatants | Damage numbers, quest kills, Combat XP, standing from kills (✅) · stats (planned) |
| **CombatantDied** | Combatants | Debug probe (✅) |
| ItemsAdded / GoldChanged / ItemUsed | Inventory | Notifications (✅) · quests (planned) |
| **ItemTraded** | Shops | Standing (✅) · quests, stats (planned) |
| **CropHarvested** | Farm | Farming XP (✅) · quests (planned) |
| XpGained / **LevelUp** | Progression | Notifications (✅) · achievements, UI (planned) |
| QuestStarted / QuestProgress / **QuestCompleted** / QuestAbandoned | Quest log | UI, standing (✅) |
| **ReputationChanged** | Reputation | Feed note, tier banner (✅) |
| ContractBoardOpened | Contracts board | Board screen |
| ShopOpened, LockOnChanged, CameraShake, HudMessage | Various | UI, camera |
| GameSaved / GameLoaded / SceneLoaded | Save, scene loader | UI, systems |
| **NewGame** | Save service | Persistent services reset (the clock goes back to day 1, 08:00) |

### Saved State
Player position · world time · inventory & gold · equipment (both weapons, active weapon, armor) · Discipline levels, XP & points · unlocked skills & E/Q slots · quests (active progress, completed, contract days) · Ink story state · every farm tile · each shop's stock and market saturation · standing (and today's capped gains) · weather · hours awake.

### Save Slots & Main Menu (Milestone 16)
| Part | How it works |
|---|---|
| Boot | Bootstrap → **MainMenu** → the world. Pressing Play in World_Test still skips the menu (plays in slot 1) |
| Slots | 3 slots, each its own playthrough. The **active slot** is the one sleeping, **Save game** in the pause menu and F5/F9 write to and load from |
| New Game | Pick a slot (an occupied one asks first). The clock resets, the world loads fresh, and the slot is saved immediately so it's claimed |
| Continue | Loads the most recently saved slot (damaged saves are skipped) |
| Load Game | Each card: Day and time · gold, Combat/Farming levels, standing tier · play time and when it was saved. Delete asks first |
| Summary | Saved inside the file, so the menu reads it without loading the game. Any saveable can add a line (ISaveSummarySource) |
| Play time | Counted while in the world (menus included, the title screen and loading not); saved per slot |
| Safety | Damaged files or saves from a newer version show as "Damaged" and can't be loaded (the menu stays put); version 1 saves load fine and show "Day ?" until saved again |
| Quit to main menu | From the pause menu, with a "unsaved progress will be lost" confirm |

### Gameplay Systems
| Area | Main components |
|---|---|
| Player | PlayerMotor, PlayerCombat, LockOnController, PlayerInteractor, PlayerFarmer, QuickItemUser, Inventory, PlayerEquipment, PlayerStats, PlayerProgression, PlayerSkills |
| Progression | ProgressionConfig (XP curve, per-level growth), SkillData, StatModifier / IStatSource (anything can contribute stats) |
| Combat | Combatant, Stamina, AttackExecutor, TargetFinder, EnemyController, CombatantFlash |
| Items | ItemData / ConsumableData / EquipmentData / WeaponData, Inventory, LootTable, LootDropper, ItemPickup |
| Farming | CropData, CropCatalog, FarmPlot, SleepSpot |
| Economy | ShopData, Shopkeeper, NpcController |
| Visuals | DirectionalSpriteSheet, DirectionalSpriteRenderer, SpriteQuad |
| Interaction | Interactable (base), PlayerInteractor — shared by farm, bed, shops, NPC dialogue, contracts board; ready for doors, chests |
| Dialogue | DialogueRunner (Ink story + game functions), DialogueSpeaker (talkable NPC + quest markers), SpeakerData |
| Quests | QuestData, QuestLog (progress, turn-in, rewards, daily contracts), ContractBoard |

---

## 22. Project Structure & Setup Tools
**Status:** ✅

### Folders (`Assets/_Project/`)
```
Art/        Characters/Placeholder · Crops/Placeholder · Materials
Data/       Combat (Attacks, Movesets, Classes, Enemies) · Items (+ Loot, Equipment) · Farming · Economy · Progression (+ Skills) · Quests · Dialogue/Speakers · Visuals · GameDatabase
Dialogue/   Main.ink (master) · Externals.ink (game functions) · one .ink per NPC · Main.json (compiled)
Resources/  GameConfig (the only Resources asset)
Scenes/     Bootstrap · World_Test
Settings/   GameControls.inputactions
Scripts/    Core · Gameplay · Editor   (one assembly each)
```

### Setup Menu (Unity: **Beast → Setup**)
Each step builds its milestone's assets and scene objects. All steps are safe to re-run and never overwrite tuned values.

| Menu item | Creates |
|---|---|
| Run Milestone 1 Setup | Folders, GameConfig, GameDatabase, Bootstrap + World_Test scenes, build settings, fast Play Mode |
| Run Milestone 2 Setup (Combat) | Player/Enemy layers, Knight (2 styles), Bandit & Dummy data, enemies in the scene, combat HUD |
| Run Milestone 3 Setup (Sprites) | Placeholder sprite sheets, sprite material, sprite visuals on all characters |
| Run Milestone 4 Setup (Items) | Starter items, loot tables, inventory, quick-use, inventory screen |
| Run Milestone 5 Setup (Farming) | Crops, crop art, 6×6 field, bed, starter seeds, interaction |
| Run Milestone 6 Setup (Economy) | General store, merchant NPC (NPC layer), trade screen |
| Run Milestone 7 Setup (Progression) | Progression config, Knight & Farming skill trees, skill attacks, weapons & armor, Brenna's Forge + blacksmith NPC, XP rewards, player stats/progression/equipment/skills, character screen |
| Run Milestone 8 Setup (Dialogue & Quests) | Compiles Ink, speakers, 2 story + 2 side quests + 3 contracts, dialogue runner & box, quest UI, NPC conversations, contracts board, quest log on the player |
| Run Milestone 9 Setup (Environment Art) | EnvBillboard / MinimapOnly layers, generated environment textures, materials and meshes, dressed buildings, `Environment_Dressing` (square, roads, fence, props, trees, rocks, ground cover), lighting & fog. HUD, pause menu, quest tracker and minimap need no setup: they're added automatically to any scene with a Player |
| Run Milestone 33 Setup (Music) | Synthesises the four placeholder music loops into `Audio/Placeholder` and adds them to the SoundLibrary (run Milestone 20 first). Keeps tracks you've swapped in |
| Run Milestone 31 Setup (NPC Schedules) | Daily routines for Oswin and Brenna (Npc Schedule components) and their places under `[NPC Places]` (stall, forge, the well, the contract board, each one's house door). Kept on re-run |
| Run Milestone 28 Setup (Names & Mirror) | The standing `Mirror` near the bed (kept on re-run) with a `Mirror_Glass` material, and a recompile of the Ink story (adds `player_name()`). Name entry needs no setup |
| Run Milestone 27 Setup (Storage Chest) | The 60-slot `Storage_Chest` beside the bed (kept on re-run), linked to the workbench so crafting can use its contents |
| Run Milestone 26 Setup (Crafting) | Three new foods (Stamina Tonic, Roast Turnips, Turnip Stew) with icons, eight recipes in `Data/Crafting/Recipes`, the `RecipeBook`, the `Workbench` beside the bed, and Player Buffs on the player. Re-running keeps your edits and adds missing recipes to the book |
| Run Milestone 25 Setup (Archers & Jump Links) | Bandit Archer data (`Enemy_BanditArcher`, kept if it exists), placeholder sprites with a bow (`Sheet_BanditArcher`), and `Bandit_Archer` at the bandit camp. Jump links need no setup |
| Run Milestone 23 Setup (Cleanup) | Deletes Unity's unused template scene (`Assets/Scenes/SampleScene`) and keeps it out of Build Settings |
| **Beast → Build → Windows (Development / Release)** | Builds the ticked scenes into `Builds/Windows` (or `Builds/Windows (Dev)`, with the dev overlay and debug keys) and opens the folder. `Builds/` is ignored by git. About 110 MB |
| **Beast → Show Character Sprites in Edit Mode** | On by default: characters show as their idle sprite in the Scene and Game views outside Play Mode (drawn by the editor, nothing saved into the scene); the cyan box only shows for the selected character |
| Run Milestone 24 Setup (Fall Damage & Climbing Down) | Adds Player Fall Damage to the player and the 10 m `Ivy_Tower` to the climbing course (rebuilt on re-run; re-running Milestone 11 removes it, so run 24 again after it). Climbing down needs no setup |
| Run Milestone 20 Setup (Sound) | Synthesises the placeholder sounds into `Audio/Placeholder` and fills `Resources/SoundLibrary` (keeps clips you've swapped in). The sound player needs no scene changes |
| Run Milestone 19 Setup (Character Look) | Adds Player Appearance to the player and marks each combat style with the weapon it shows (greatsword / sword &amp; shield). The creator itself needs no setup |
| Run Milestone 18 Setup (Item Icons) | Draws a placeholder icon for every item without one (`Art/UI/Icons`), the gold icon and the dropped-item card material (`Resources`) |
| *(Milestone 17, Settings)* | No setup: the settings service is created at boot and the Settings buttons are already in the main and pause menus |
| Run Milestone 16 Setup (Main Menu & Save Slots) | Creates the **MainMenu** scene (camera + title screen), puts it in Build Settings after Bootstrap and points GameConfig at it. Save slots need no setup |
| Run Milestone 15 Setup (Night Danger & Fatigue) | Adds Player Fatigue to the player (enemy night settings need no setup) |
| Run Milestone 14 Setup (Day, Night & Weather) | Rain texture + Beast/Rain material, an animated procedural sky, the `[Sky]` object (Day Night Cycle + Weather System), lanterns at every house door and the board, a campfire at the bandit camp (`[Night Lights]`, rebuilt on re-run) |
| Run Milestone 13 Setup (Smarter Enemies) | Adds a third Bandit (Bandit_C) to the camp. Pathfinding needs no setup: the navigation mesh is built when the scene loads |
| Run Milestone 12 Setup (Sprite Shadows) | Moves the character sprite material and the tree / bush / grass / flower materials onto Beast/Sprite Lit and lets those sprites receive shadows |
| Run Milestone 11 Setup (Climbing) | Hang/Climb frames on the Knight's placeholder sheet, ivy wall texture + `Env_Ivy`, PlayerClimber on the player, Not Climbable on the greybox houses, the `Climbing_Course` practice area (removes dressing that overlaps it) |
| Run Milestone 10 Setup (Reputation) | Reputation Config, standing rewards on quests and Bandits (only ones still at 0), the Town Patrol contract on the board, recompiles Ink, Reputation component on the player |
| **Beast → Data → Rebuild Game Database** | Re-indexes all content assets and fills missing IDs |
| **Beast → Dialogue → Compile Ink Story** | Recompiles `Dialogue/Main.ink` → `Main.json` and reports Ink errors with file and line |

### Packages
Ink Unity integration (`com.inkle.ink-unity-integration`, pinned to a specific commit in `Packages/manifest.json`; needs Git installed so Unity can download it).

### Conventions
- New content = new data asset (right-click → **Create → Beast → …**), then *Rebuild Game Database*.
- Setup scripts must reload assets by path after opening a scene (opening a scene unloads assets).
- URP/Lit decides shadow receiving **per material**, not per renderer. Billboard sprites use **Beast/Sprite Lit** (`Art/Shaders/SpriteLit.shader`), which supports Forward+, soft shadows, SSAO, fog and point lights.

### Version Control
Git + Git LFS (art, audio, models) — `.gitignore` and `.gitattributes` in place. ⚠️ Project lives inside OneDrive; move it out or exclude it from syncing.

---

## 23. Content & Tuning Reference
**Status:** 🟡 prototype values — tune through play

### Combat Tuning
| Setting | Value |
|---|---|
| Input buffer | 0.25 s |
| Tap/hold boundary (dodge vs sprint) | 0.2 s |
| Soft-aim range / angle | 4.5 m / ±75° |
| Lock-on acquire / break range | 15 m / 20 m |
| Player walk / sprint speed | 4.5 / 7.5 m/s |
| Player respawn delay | 2 s |

### Attacks
| Attack | Dmg | Poise | Startup / Active / Recovery (s) | Lunge |
|---|---|---|---|---|
| Knight S&S Light 1 / 2 | 12 | 10 | 0.10 / 0.08 / 0.22–0.24 | 0.6 m |
| Knight S&S Light 3 | 18 | 20 | 0.14 / 0.10 / 0.30 | 1.0 m |
| Knight S&S Light 4 | 26 | 35 | 0.20 / 0.12 / 0.40 | 1.4 m |
| Knight Shield Bash (heavy) | 20 | 45 | 0.30 / 0.12 / 0.40 | 1.5 m |
| Knight GS Light 1 / 2 | 22 / 24 | 20 / 22 | 0.18 / 0.12 / 0.32–0.34 | 0.8 m |
| Knight GS Light 3 | 40 | 50 | 0.30 / 0.16 / 0.50 | 1.6 m |
| Knight GS Overhead (heavy) | 45 | 70 | 0.45 / 0.16 / 0.55 | 1.0 m |
| Bandit Slash | 12 | 25 | 0.45 / 0.12 / 0.45 | 0.8 m |
| Bandit Overhead | 20 | 40 | 0.75 / 0.12 / 0.60 | 1.2 m |
| Skill: War Cry (360°) | 5 | 30 | 0.15 / 0.15 / 0.30 | — |
| Skill: Shield Charge | 18 | 55 | 0.12 / 0.35 / 0.35 | 5.0 m |
| Skill: Whirlwind (360°) | 38 | 45 | 0.25 / 0.35 / 0.45 | 0.5 m |

Damage values are before the attacker's Attack stat and the target's Defense.

### Loot Tables
| Table | Drops | Gold |
|---|---|---|
| Bandit | Bandit Cloth 80% (1–2) · Iron Scrap 35% · Turnip Seeds 30% (1–3) · Bread 25% | 3–12 |
| Training Dummy | Bread 50% · Turnip Seeds 30% (1–2) | 1–3 |

### Player Start (test scene)
3 Bread · 1 Healing Draught · 6 Turnip Seeds · 3 Healroot Seeds · 10 gold · Rusted Sword & Shield + Rusted Greatsword + Gambeson equipped · Combat 1 / Farming 1.

---

# Part VI — Production

## 24. Roadmap & Milestones

| # | Milestone | Status | Result |
|---|---|---|---|
| 0 | Pre-production | 🟡 | This GDD; ⬜ mood board |
| 1 | **Backbone** | ✅ Verified | Bootstrapper, services, events, save/load, input, game states, world clock, data layer |
| 2 | **Combat prototype** | ✅ Verified — *fun* | Knight (2 styles), Bandit, Dummy; dodge/block/parry, lock-on, hit-stop |
| 3 | **Sprite tech** | ✅ Verified | 8-dir billboards, shadow quads, attack-synced animation, placeholder sheets |
| 4 | **Systems** | 🟡 In progress | 4a Items/inventory/loot ✅ · 4b Farming ✅ · 4c Economy & shops ✅ · 4d Stats & progression ✅ · 4e Dialogue & quests 🟡 (Ink, quests, contracts — awaiting test) · Light reputation 🟡 (awaiting test) · Climbing & ledges 🟡 (awaiting test) · Sprite shadows 🟡 (awaiting test) · Smarter enemies 🟡 (awaiting test) · Day/night & weather 🟡 (awaiting test) · Night danger & fatigue 🟡 (awaiting test) · Main menu & save slots 🟡 (awaiting test) · Settings 🟡 (awaiting test) · Item icons 🟡 · Character creator & weapon looks 🟡 · Placeholder sound 🟡 (awaiting test) · Fall damage & climbing down 🟡 · Archers & jump links 🟡 · Crafting 🟡 · Storage chest 🟡 · Names & mirror 🟡 · NPC schedules 🟡 · Music 🟡 · Controller navigation 🟡 · Cleanups & first Windows build ✅ |
| 5 | Vertical slice | ⬜ | Prologue + homestead + 1 wild zone, polished; playtest with strangers |
| 6 | Steam page + demo | ⬜ | Wishlists, devlogs |
| 7 | Content & polish | ⬜ | Full v1 region |
| 8 | Release / Early Access | ⬜ | — |

### Candidate Next Steps
| Option | Why |
|---|---|
| Real UI | Theme, layout and pad navigation are proven in the prototype; port to UI Toolkit with the real art |
| Real-art style test | Sets the art budget |

---

## 25. Open Questions
All unresolved design questions in one place.

### World & Story
- [ ] Names: world, region, kingdoms, hub town, in-world currency.
- [ ] What exactly was the war-sorcery? Who cast it?
- [ ] Why is the homestead's soil untouched? Why does it react to the protagonist?
- [ ] Main story arc: acts, major beats.
- [ ] Single ending or multiple?
- [ ] Key NPCs: survivor guide, rival, antagonist.

### Protagonist
- [ ] How is the protagonist "marked"? (visual, ability, story-only?)
- [ ] Voiced or text-only? (Recommend text + grunts for solo scope.)
- [ ] Appearance customisation depth (8-dir sprites make this expensive — presets + colour palettes?)

### Combat & Classes
- [ ] Ranger and Battlemage kits, resources, styles and skill trees.
- [ ] Solo only, or companions?
- [ ] Can players switch class later / multiclass?
- [ ] Weapon class restrictions (can a Knight use a bow?).

### Progression
- [ ] Exploration Discipline: XP sources and perks (with climbing and real levels).
- [ ] Trade / Crafting Disciplines — and how Trade relates to reputation.
- [ ] Respec (refund skill points)?
- [ ] Gear tiers beyond Iron; upgrading.
- [ ] Homestead upgrade tree.

### Farming & Economy
- [ ] Seasons? Tools and water capacity? Rain? Soil blight? Livestock? Crop raids?
- [x] Crafting scope: alchemy, cooking and smithing at one homestead workbench (Milestone 26). Open: a Crafting discipline?
- [ ] Storage limits / homestead chest?
- [ ] Merchant gold limits, buyback, haggling, regional prices, scarcity events?

### Presentation & Production
- [ ] Final art look: pixel vs painted/HD.
- [ ] Music direction.
- [ ] Target audience definition.
- [ ] Early Access or full release?

---

## 26. Backlog & Known Compromises
Requested features and accepted compromises, to revisit before the vertical slice.

| Item | Type | Current behaviour | Planned |
|---|---|---|---|
| Sprite shadow offset | Limitation | A sprite ignores shadows from anything within its Self-Shadow Offset toward the sun (e.g. a character pressed right against another one) | Fine for now; a per-character shadow ID in the shadow map would remove it |
| Prototype UI (IMGUI) | Tech debt | Every screen shares one theme, scales with resolution and works with mouse, pad and arrow keys (Milestone 35) | Port to UI Toolkit when real art arrives (§20) |
| **Full regression test pass** | QA | Not yet run. `Test_Checklist.md` (MegaGame101 folder) covers Milestones 1 → 4d, ~150 checks | Run the whole checklist with fresh saves and Error Pause on; fix anything found. Do this before the vertical slice (or sooner if bugs appear). Extend the checklist with each new milestone |
| Generated environment layout | Tooling | The Milestone 9 setup rebuilds `Environment_Dressing` from scratch on every run | Hand-built levels replace it at the vertical slice |
| Enemy name plates through walls | UI | An enemy's name and health bar show even when a house is between you and it (seen at night in town) | Hide plates without a line of sight |

---

## 27. Risks

| Risk | Impact | Mitigation |
|---|---|---|
| 8-dir sprite art volume (solo) | High | 3D→2D pre-render pipeline; small sprites; limit animations; placeholder-first |
| Scope creep (open world + farming + economy) | High | Pillars + v1 scope table; vertical slice first |
| Combat not fun | High | ✅ Mitigated — greybox prototype verified fun before art |
| Cutscene cost | Medium | In-engine dialogue scenes; 2–3 staged cutscenes max |
| Economy breaks (too much money) | Medium | Supply & demand built in; define sinks early; playtest balance |
| Part-time burnout | Medium | Small milestones; share devlogs for motivation |
| OneDrive syncing the Unity project | Medium | Move project out of OneDrive or exclude it; Git + LFS |

---

## 28. Change Log

| Date | Version | Change |
|---|---|---|
| 2026-09-29 | 0.1 | Initial draft: constraints, pillars, core loop, world premise, v1 scope, technical backbone plan. |
| 2026-09-29 | 0.2 | Milestone 1 backbone implemented in Unity project "Beast". |
| 2026-09-29 | 0.3 | Milestone 1 verified. Combat decisions locked (fast & flashy, dodge/block/parry, soft-aim + lock-on, multi-style classes). Pillar 1 reworded. Milestone 2 combat prototype (Knight 2 styles, Bandit, Dummy). |
| 2026-09-29 | 0.4 | Milestone 2 verified; dodge/sprint split. Milestone 3 sprite tech with placeholder art. |
| 2026-09-29 | 0.5 | Milestone 3 verified; sprite shadow shader deferred. Milestone 4a: items, inventory, loot, consumables, gold, inventory screen. |
| 2026-09-29 | 0.6 | 4a verified. Climbing & ledge grab added to backlog. Milestone 4b farming: interaction system, field, Turnip & Healroot, watering/wilt/death, regrowth, bed. |
| 2026-09-30 | 0.7 | 4b verified. Milestone 4c economy: shop pricing, supply & demand, restock, merchant Oswin, trade screen, trade events, reputation price hook. |
| 2026-10-08 | 1.25 | **Milestone 23 — cleanups.** Characters are drawn as sprites in the Scene view outside Play Mode (editor-only; toggle under Beast). The unused SampleScene is removed (it was already out of the build list). New Beast → Build → Windows (Development / Release). First release build: 107 MB, starts cleanly and passes the main-menu test (new game into the world). |
| 2026-10-08 | 1.24 | **Milestone 35 — controller support.** Kept the IMGUI screens (the UI Toolkit port waits for real art) and added navigation to every themed control: focus frame, D-pad / stick / arrow keys to move, A / Enter to press, left / right on sliders and steppers, scroll-to-focus, B to back out (now also bound to Resume), pad labels in key hints and key caps, a Random name button for pad players. Bag slots are navigable and show details on focus. |
| 2026-10-08 | 1.23 | **Milestone 33 — placeholder music.** Four synthesised folk-style loops (title, day, night, combat) that crossfade by game state, clock and danger: combat music while enemies chase you nearby, holding a few seconds after. Quieter under menus. Settings → Audio → Music now does something. |
| 2026-10-08 | 1.22 | **Milestone 31 — NPC schedules.** Oswin and Brenna keep daily routines: work from 06:00, lunch and evenings at the well, Brenna checking the contract board, and sleeping at home at night (hidden; no trading or markers). They walk the navigation mesh, stop for you, and snap into place after time skips. NPCs no longer cut holes in the navigation mesh. |
| 2026-10-08 | 1.21 | **Milestone 28 — names & the mirror.** The creator gains a Name field (default "Wanderer"), shown on the HUD, the Character screen and the save slot, and available to Ink as `player_name()`. A mirror at the homestead reopens the creator's controls to change your name, hair, colours and outfit mid-game. The creator and the mirror share one look editor. |
| 2026-10-08 | 1.20 | **Milestone 27 — storage chest.** A 60-slot chest beside the homestead bed with a parchment Bag / Chest window (Store, Take, All, and "Store crops, seeds & materials"). The workbench now uses ingredients from the chest too. Saved with the game. |
| 2026-10-08 | 1.19 | **Milestone 26 — crafting, part one.** A workbench beside the homestead bed opens a parchment Workbench window with Alchemy, Cooking and Smithing recipes (eight to start): Healing Draught and a new Stamina Tonic from Healroot; Roast Turnips and Turnip Stew; Iron weapons and the Iron Helm forged from their lesser versions plus scrap and cloth (upgrading gear you're wearing in place), and a Padded Cap from cloth. Food can now give timed buffs, shown under the health bar. Crafting sounds. |
| 2026-10-08 | 1.18 | **Milestone 25 — archers & jump links.** (Pathfinding itself already existed from Milestone 13; the stale backlog row is gone.) Enemies vault up edges up to 2.4 m and jump down drops up to 4.5 m along automatic jump links, so low blocks are no longer safe. New **Bandit Archer**: keeps its distance, draws (telegraphed) and looses arcing arrows whenever it can see you, including while you climb; walks round to get a view; arrows can be blocked, parried and dodged. Placeholder sprites gained a bow. One archer at the bandit camp. |
| 2026-10-08 | 1.17 | **Milestone 24 — fall damage & climbing down.** Drops over 6 m hurt in proportion to height (all your health at 18 m), measured from the top of the fall; a hard landing stuns briefly, shakes the camera and thuds. Stand still at a top edge facing the drop and press Space to lower yourself into a hang, or onto an ivy wall. New 10 m ivy tower on the climbing course. Removed both from the backlog. |
| 2026-10-08 | 1.16 | **UI restyle, step 4: pop-ups and HUD.**<br>• Shop and contract board are parchment windows: paper rows and cards, brush buttons, and the tracked contract framed in vermilion.<br>• Dialogue is an ink band across the bottom: the portrait on a paper mat, the speaker's name in spaced vermilion capitals, choices as paper rows (selected one red) with key caps.<br>• HUD: see-through ink panels and key caps; a thin vermilion health bar with thin stamina and poise bars and the numbers above them; Tired / Exhausted in spaced capitals; gold coin and sun in ink gold; quest banners on an ink band with the title in spaced capitals; a thin ink frame on the minimap. "Defeated" and the sleep fade use spaced capitals.<br>• This completes the restyle (Milestone 21). |
| 2026-10-07 | 1.15 | **UI restyle, step 3: tab contents.** Bag: category cards (All, Food, Materials, Seeds & crops, Gear), paper slots with a vermilion frame on the selected one, an ink details panel with Use / Equip. Character: Combat / Farming / Gear cards. Each Discipline's skills form a tree of diamond nodes, one row per tier: gold rim when learned, ink when learnable, grey when locked, linked to prerequisites. Gear shows the character's own sprite among the six equipment slots; the ink panel has the selected slot, spare gear that fits it, and all stats. Journal: filter cards plus a town-standing card, the quest list (gold diamond = tracked), and an ink details panel with Track / Abandon (Esc cancels the abandon question). |
| 2026-10-07 | 1.14 | **UI restyle, step 2: the game menu.** Map, Journal, Bag, Character and Options are now tabs of one parchment screen with an ink top bar (Q / E switch; M / J / Tab / I / C jump to a tab or close it; Esc opens Options, which is the pause). The large map is now the Map tab (time frozen) instead of an overlay while playing. Options: four tiles that show their settings inline, and Resume / Save / Load last save / Quit to title / Quit to desktop as brush buttons. Settings restyled on parchment (paper sliders, toggles, option cards) on the title screen and in Options. |
| 2026-10-07 | 1.13 | **UI restyle, step 1** (Ghost of Tsushima reference). Ink-and-parchment theme: parchment, ink, paper, one vermilion for selection, gold accents; brush strips, red brush blocks and a white swash drawn in code. New fonts: Cormorant Garamond (titles, spaced capitals) and Alegreya Sans (all body text, HUD included). Title screen: stormy sky with drifting clouds and falling ash, a plain list on the left with a brush swash (mouse or arrow keys), parchment slot picker and character creator. |
| 2026-10-07 | 1.12 | **Milestones 18–20.** Item icons: a pixel-art placeholder icon for every item; dropped items show it on a camera-facing card. Character creator on New Game (hair, hair colour, skin tone, outfit; live preview; saved per slot). The player's sprite is drawn at runtime from that look and the weapon in use: sword &amp; shield or a two-handed greatsword. Placeholder sound: combat, footsteps, items, farming, interface chimes and day / night / rain ambience; Settings → Audio gains Ambience and Interface sound sliders. |
| 2026-10-06 | 1.11 | **QA pass fixes** (see Docs/QA). Invisible walls at the edge of the ground plus a safety net (fall below the world → back on the last solid ground; enemies sent home). Roofs got colliders (the camera could slip inside a roof). Holding F repeats only the action it started with (hold to harvest no longer replants and waters). Fading HUD notices fade their box too. Skill lock reasons sit under the Learn button. Quest banners over menus are a slim strip at the top. Consumables only list what they restore. Brenna completes Roots of the Blight in one talk if you already carry Healroot. The main menu works from its first frame. Milestone 16 setup also removes Unity's template SampleScene from Build Settings. New repair menu: Beast → Setup → Repair → Add Roof Colliders. Project moved into the GitHub repo. |
| 2026-10-06 | 1.10 | **Milestone 17 — settings.** Settings screen from the main and pause menus: mouse / controller sensitivity, invert Y; window mode, resolution (with keep-or-revert), VSync, frame cap, render scale, shadow quality, anti-aliasing; master / music / effects volume; interface size, damage numbers, camera shake. Saved to settings.json for all slots; reset to defaults. |
| 2026-10-06 | 1.9 | **Milestone 16 — main menu & save slots.** Title screen (Continue / New Game / Load Game / Quit) with a dusk backdrop; 3 save slots, one playthrough each, with summaries (day, time, gold, levels, standing, play time, save date), overwrite and delete confirms; sleeping, the pause menu and F5/F9 use the active slot; Quit to main menu; save format v2 (old saves still load). |
| 2026-10-02 | 1.8 | **Milestone 15 — night danger & fatigue.** Enemies are bolder at night (+30% damage, +40% aggro range, +15% speed, red name plates) and drop double loot rolls and +50% gold; nightfall / dawn messages. Fatigue: Tired at 18 h awake, Exhausted at 22 h (stamina, regen and attack penalties), HUD badge, cleared by sleeping, saved. |
| 2026-10-02 | 1.7 | **Milestone 14 — day, night & weather.** Sun arc with dawn/dusk colour, moonlit nights, animated sky, ambient light and fog; daily Clear / Cloudy / Rain weather from a seed, rain particles, rain waters the field; lanterns and a bandit campfire that light up at dusk; sky icon on the HUD clock; F4 cycles weather. |
| 2026-10-02 | 1.6 | **Milestone 13 — smarter enemies.** Runtime navigation mesh (built from colliders at scene load); enemies path around obstacles, take turns (max 2 attacking), hold a ring while waiting, keep apart, give up and walk home when the player climbs out of reach, leash to 22 m from home and heal on returning. Third Bandit at the camp. |
| 2026-10-02 | 1.5 | **Milestone 12 — per-pixel sprite shadows.** New Beast/Sprite Lit shader: characters, trees, bushes and grass receive real shadows (shadow lines cross sprites at the right height, thin shadows like swords show) without self-shadowing from their sun-facing shadow quads. Replaces the 3-ray banded darkening (kept as a fallback). Also: letting go of a ledge with Shift no longer fires a dodge on release; the HUD prompt row (interaction and climbing controls) moved up so it never covers the player's head. |
| 2026-10-02 | 1.4 | **Milestone 11 — climbing & ledges.** Vault low edges, grab and hang from taller ones (shimmy, pull up, leap off, let go), climb ivy-marked walls; stamina drain, falling when exhausted or hit; climbing suspends combat. HUD control hints while climbing, climbing in the pause-menu controls. Hang / Climb sprite clips (placeholder sheet upgraded in place), ivy wall texture, Climbable Surface / Not Climbable markers, practice course west of the village. Backlog: fall damage, climbing down from an edge. |
| 2026-10-02 | 1.3 | **Milestone 10 — light reputation.** One standing value with the Free Hollows, 5 tiers (Stranger → Hero of the Hollows) with 0–20% better prices both ways; earned from quests, Bandit kills (+2, 20/day) and trade (+1 per 25g, 15/day); −5 for abandoning town work. Quests can require a tier (new **Town Patrol** contract needs Trusted). Standing card in the journal, shop and character headers, feed notes, tier banners (banners now queue instead of overwriting each other). Ink: `standing()`, `standing_tier()`, `change_standing()`; Oswin explains standing and thanks you at Trusted; Brenna gives an Iron Helm at Friend. Fix: Ink `give_item` now drops what doesn't fit instead of losing it. |
| 2026-10-01 | 1.2 | **Polish pass.** UI/UX: a shared theme for every screen and the HUD (resolution-independent); new HUD layout (vitals, E/Q/R/X action slots, clock & gold, notification feed, prompt pill); pause menu (save, load, controls, quit); defeat overlay; sleep fade; inventory, shop, character, journal, board and dialogue rebuilt for clarity and feedback. Environment art: generated pixel-art textures and billboard vegetation, dressed buildings, village square, roads, fence, props, a forest ring with blight. Fixes: holding F at the bed slept through several nights; harvest overflow vanished when the bag was nearly full; loading was possible during menus and dialogue; menu hotkeys abandoned conversations and Tab unpaused the game; the minimap was washed out by fog; the Milestone 9 setup crashed on a re-run (found in testing). |
| 2026-10-01 | 1.1 | Quest UX: ✕ close button on every menu (board had no visible exit), quest banners, board status badges + progress bars, journal rebuilt (filter tabs, list, details, Track, Abandon), **tracked quest** (T / Track) driving HUD panel + on-screen waypoint + minimap marker, **minimap** (M for large map). |
| 2026-10-01 | 1.0.1 | Fix: dialogue text going blank when clicking during the typewriter effect. Rule added: every quest offer has Accept / Decline (Scrap Run and Roots of the Blight now ask; declined offers can be re-asked and keep their "!" marker). |
| 2026-10-01 | 1.0 | Full regression test pass deferred (backlog). Milestone 4e dialogue & quests: Ink integration (master story, game functions, saved story state), dialogue box with portraits, quest system (story/side/contract; kill/collect/harvest/custom objectives), quest tracker, NPC markers, journal (J), contracts board, first story beat (the ash fears your Healroot). Merchants now talked to before trading. |
| 2026-09-30 | 0.9 | Milestone 4d progression: Disciplines (Combat, Farming; Renown display-only), stat system, equipment (2 weapons = styles, 4 armor slots), Knight skill tree (3 actives on E/Q + 3 passives), farming perks, Brenna's Forge, character screen, charged heavy attack, Genshin-style control remap. |
| 2026-09-30 | 0.8 | **Full reorganisation** into six parts (Vision, Gameplay, World, Presentation, Technical, Production). Added: scope & constraints, enemies, traversal, full controls, architecture as built, project structure & setup tools, content & tuning reference, consolidated open questions, expanded backlog. Resolved stale "to define" lists (combat, camera, sprite shading, day length). |
