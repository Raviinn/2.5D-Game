# Full Test Checklist — Milestones 1 → 53 (+ QA fixes, cleanups)

**Build under test:** Unity project `Beast`, everything through Milestone 4e (Dialogue & Quests), plus the polish pass (new UI theme, HUD, pause menu, environment art — setup menu "Milestone 9") light reputation (setup menu "Milestone 10") climbing & ledges (setup menu "Milestone 11") per-pixel sprite shadows (setup menu "Milestone 12") smarter enemies (setup menu "Milestone 13") day/night & weather (setup menu "Milestone 14") night danger & fatigue (setup menu "Milestone 15") and the main menu & save slots (setup menu "Milestone 16").
**Format:** each line is **Action → Expected result**. Tick it only if the expected result happens **and the Console shows no new red errors or yellow warnings**.
If something fails, note it with the bug template at the bottom.

---

## 0. Preparation (do once before testing)

- [ ] **Console setup:** open the Console window and turn on **Error Pause**, so Play pauses on the first error. Clear it before each section.
- [ ] **Game view:** turn on **Gizmos** (top-right of the Game view) to see hitboxes.
- [ ] **Fresh saves:** close Play Mode and delete the folder `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Beast\saves` (paste the path into Explorer). This rules out old saves causing false bugs.
- [ ] **Ink package:** after Unity recompiles, **Window → Package Manager → In Project** lists **Ink** (2.0.0). If it failed to install, make sure Git is installed and restart Unity.
- [ ] **Setup menus:** run all of these once, in order: **Beast → Setup → Milestone 1 … Milestone 33** (24–33 skip the numbers that have no setup). Each ends with a "… setup complete" line → no errors in the Console. (Milestone 9 adds the environment art; it takes a few seconds.)
- [ ] **Re-run safety:** run **Milestone 7** to **33 Setup** a second time each → they say things were "kept as-is", repaired or rebuilt. There are no duplicate NPCs, enemies, boards, UI objects, roofs or trees in the Hierarchy, and no errors.
- [ ] **Beast → Dialogue → Compile Ink Story** → "Compiled Assets/_Project/Dialogue/Main.json", with no [Ink] errors.
- [ ] **Database:** run **Beast → Data → Rebuild Game Database** → "Rebuilt with N entries", with **no "Duplicate Id" errors**.
- [ ] **Inspector check:** select **Player**. Player Combat → Class Data = `Class_Knight`; Player Progression → Config = `ProgressionConfig`; Player Equipment → both starting weapons and the Gambeson are set.
- [ ] Select each enemy (TrainingDummy, Bandit_A, Bandit_B) → Enemy Controller → Data is set; Loot Dropper is present.
- [ ] Select **Merchant** and **Blacksmith** → Shopkeeper → Shop is set.

---

## 1. Boot & Core Systems (Milestone 1)

- [ ] Open **World_Test**, press Play → the game starts. The small grey **DEV** line (bottom-left, above the key hints) shows Playing · Day 1 08:00; the HUD clock under the minimap shows **Day 1 08:00**.
- [ ] Open **Bootstrap**, press Play → a brief black screen, then the **main menu** (after the Milestone 16 setup; before it, World_Test loads straight away).
- [ ] **Press Play, stop, press Play again** (twice more) → it works identically each time, with no "already registered" warnings and no doubled events. This checks the fast Play Mode setting.
- [ ] The clock advances in real time (about 1 in-game minute per real second) → the HUD clock and the DEV line both count up.
- [ ] **F2** → +1 hour. **F1** → +1 day; the Console logs `[EventProbe] Day 2 has begun.`
- [ ] **F3** → the DEV line hides; F3 again → it shows.
- [ ] **Esc** → the **Paused** menu appears (Resume, Save game, Load last save, Controls, Settings, Quit to main menu, Quit to desktop), the clock stops, the cursor shows, and the player can't move. **Esc** again (or **Resume**, or **✕**) → resumes and the cursor locks.
- [ ] While paused, press gameplay keys (LMB, E, Q, F, Shift) → nothing happens.

---

## 2. Movement & Camera

- [ ] WASD moves relative to the camera → the player turns to face the movement direction.
- [ ] Hold **Shift** while moving → sprint (faster). **Tap** Shift → a dodge only, no sprint.
- [ ] **Space** → jump; the player lands properly on the ground, the ramp and the steps.
- [ ] Walk up the **ramp** and the **steps** → no getting stuck or sliding off.
- [ ] Mouse moves the camera around the player; pitch is limited, so you can't flip under the ground.
- [ ] Move the camera behind a house or the tower → the camera moves in, doesn't clip through the wall, and doesn't jitter.
- [ ] Camera near the **Merchant / Blacksmith / enemies** → the camera doesn't bump into them.
- [ ] **Controller** (if you have one): left stick moves, right stick moves the camera, A jumps, B tap dodges and B hold sprints.

---

## 3. Combat — Player (Milestone 2 + 4d)

Use the **Training Dummy** first.

- [ ] **Tap LMB** repeatedly → the 4-hit Sword & Shield combo. Each hit lunges, gives a brief freeze (hit-stop), shakes the screen, flashes the dummy white and shows damage numbers.
- [ ] Stop between hits for about 1 s, then attack → the combo restarts from hit 1.
- [ ] **Hold LMB** → a light attack, then the **charged heavy** (Shield Bash) follows automatically.
- [ ] Gamepad **Y** → heavy attack directly.
- [ ] Hit the dummy until its purple **poise** bar empties → it staggers (purple tint).
- [ ] Attack with no enemy nearby → you attack toward your movement direction.
- [ ] Attack with an enemy in front, slightly off to the side → you **snap to face it** (soft-aim), and the lunge stops short of the enemy.
- [ ] **Tap Shift during an attack** → the dodge cancels the attack.
- [ ] **Hold RMB** → block. You move slower and face the camera direction.
- [ ] Hold RMB during an attack's recovery → you switch to blocking.
- [ ] **X** → the weapon swaps to the **Rusted Greatsword**; a notice shows "Weapon: …". The combo is now 3 slow, big hits. **X** again → back to Sword & Shield.
- [ ] Press X during an attack → nothing (you can only swap while free).
- [ ] **Dodge through** an attack (see §4) → no damage during the dodge; the player looks pale briefly (invulnerability frames).
- [ ] Dodge with no movement input → you step backwards.

## 4. Combat — Enemies (Milestone 2)

Go to the Bandits (northeast).

- [ ] Approach within about 12 m → the Bandits chase you.
- [ ] A Bandit **glows orange** during its wind-up, then swings → you take damage and see red damage numbers; your HP drops.
- [ ] Sidestep during the wind-up → the Bandit turns to track you. Sidestep after the wind-up → the swing misses.
- [ ] **Block** a swing (facing it) → a blue flash, reduced damage, stamina drops.
- [ ] Block until stamina runs out → **"GUARD BREAK"**, and you're staggered.
- [ ] **Tap RMB just before** a hit → **"PARRY!"**, a yellow flash and a strong freeze; the Bandit is staggered (purple) for about 1.5 s.
- [ ] Block a hit from **behind** → it's not blocked; you take full damage.
- [ ] Run more than about 18 m away → the Bandits give up and go idle.
- [ ] Kill a Bandit → it disappears, loot pops out, and it **respawns about 6 s later** at its spawn point.
- [ ] Let the Bandits kill you → you respawn at the start after 2 s with full HP and stamina. The Bandits go idle while you're dead.
- [ ] Two Bandits at once → both can hit you; no errors.

## 5. Lock-on

- [ ] **Middle mouse** while looking at an enemy → "[ LOCK ]" appears above it; the camera swings to keep it framed; you circle-strafe around it.
- [ ] Middle mouse again → the lock releases.
- [ ] Kill the locked enemy → the lock releases automatically.
- [ ] Run more than 20 m from the locked enemy → the lock releases.
- [ ] Attack while locked → you always attack the locked target.
- [ ] Middle mouse with no enemy in view → nothing happens.

## 6. Sprites & Visuals (Milestone 3)

- [ ] The player, Bandits, Dummy, Merchant and Blacksmith all appear as **pixel sprites**, not capsules.
- [ ] Orbit the camera around a standing character → its view steps through **front, 3/4, side, back** (eyes and chest emblem visible from the front, gone from the back). Left-side views are mirrored.
- [ ] Walk in circles → the run animation plays and the direction updates smoothly.
- [ ] Attack → the weapon is raised during the wind-up; the **swing frame with the white trail matches the red hitbox gizmo exactly**. Check both weapons.
- [ ] Bandit wind-up → weapon raised plus the orange glow.
- [ ] Dodge, block, stagger and death each show a distinct pose.
- [ ] Characters cast shadows on the ground from every camera angle, with **no dark diagonal band across their own body**.
- [ ] Walk into a building's shadow → your sprite darkens smoothly; step out → it brightens.
- [ ] Stand between the sun and the Dummy → the Dummy darkens (full shading or in bands).
- [ ] In the Scene view (not playing), characters show as their idle sprites (since Milestone 23; a cyan box marks the selected one).

---

## 7. Items, Inventory & Loot (Milestone 4a)

- [ ] The bottom-right HUD shows "Gold: 10" and the quick-use item (Bread).
- [ ] Kill Bandits → colored cubes and gold spheres pop out, hover, then **fly to you** when you're close; notices show ("+2 Bandit Cloth", "+8 gold").
- [ ] Stand still far from the loot → it stays hovering. After about 3 min it despawns.
- [ ] **Tab** → the inventory opens and the game pauses (enemies and the clock stop). Hover items for details.
- [ ] Click **Bread** → "Used Bread", HP goes up, and the count goes down.
- [ ] Click the **Healing Draught** → HP and stamina restored.
- [ ] Close with **Tab**, then **I**, then **Esc** (try each) → closes and the game resumes.
- [ ] Press **R** at low HP → eats the first food. Pressing R rapidly → about 0.8 s cooldown between uses.
- [ ] Press R mid-attack or mid-dodge → nothing happens.
- [ ] Run out of food, press R → nothing happens; the HUD says "(no food)".
- [ ] Items stack correctly (e.g. several Bandit Cloth in one slot).
- [ ] **Bag full** (optional: buy lots of seeds to fill 30 slots) → new loot stays on the ground; it's picked up once you make space.

## 8. Interaction & Farming (Milestone 4b)

Go to the dark 6×6 field southeast of the spawn.

- [ ] Face a tile → a glowing outline plus the prompt **"[F] Till soil"**.
- [ ] **F** → the tile becomes brown soil; the prompt changes to "Plant Turnip Seeds (×6)".
- [ ] **F** → planted (sprout visible); the prompt says "Water Turnip".
- [ ] **F** → watered (soil darker); the prompt says "watered, growing". Pressing F again does nothing.
- [ ] **Hold F while walking along a row** → it repeats the action you started with: hold on grass to till a row, hold on tilled soil to plant a row, hold to water, hold to harvest. Holding to harvest never replants or waters.
- [ ] **V** → switches seed type (notice "Seeds: Healroot Seeds"); the prompt mentions "V: switch seeds".
- [ ] **F1** (next day) → watered crops grow one stage; all soil dries (lighter).
- [ ] Water daily with F1 in between → the Turnip is **ripe after 4 watered days** (white bulb). "[F] Harvest Turnip" gives 1–2 Turnips.
- [ ] After harvesting a Turnip → the tile is empty tilled soil again.
- [ ] Healroot: ripe after 6 watered days (red berries). After harvesting, the plant **stays** and is ripe again 3 watered days later.
- [ ] Skip 2 days without watering → the crop **wilts** (brown, drooping); the prompt says "Water wilting …!". Water it, then F1 → it recovers and grows.
- [ ] Skip 3 days without watering → the crop **dies**; "[F] Clear dead Turnip" returns the tile to tilled soil.
- [ ] A ripe crop left for many days → still ripe (never spoils).
- [ ] With 0 seeds on a tilled tile → "Tilled soil (no seeds)"; F does nothing.
- [ ] Eat a harvested Turnip with R → heals 15.
- [ ] Try to interact mid-attack or while staggered → no prompt shows.

## 9. Sleep (Milestone 4b)

- [ ] Stand next to the red **bed** → "[F] Sleep until 06:00".
- [ ] Sleep → the screen goes **black with "Day N · Morning, 06:00 · You feel rested · Game saved"**, then fades back in. The clock is at 06:00 of the next day; HP and stamina are full; watered crops grow.
- [ ] **Regression (hold F):** keep **F held** at the bed for 3 seconds → you sleep **once** (only one day passes).
- [ ] Sleep at 03:00 (use F2 to get there) → you wake the **same** day at 06:00.
- [ ] Both the bed and the field are in range → the closest one's prompt wins.

## 10. Economy & Shops (Milestone 4c)

- [ ] Walk to **Oswin** (green, north of the field) → he turns to face you within about 5 m; "[F] Talk to Oswin". Talk → choose **"I'd like to trade."** → the dialogue closes and his shop opens.
- [ ] **F** → the trade screen opens and the game pauses. Stock is on the left, your sellables on the right; the greeting shows.
- [ ] **Only** the trade screen shows (the inventory panel does not overlap it).
- [ ] Buy Turnip Seeds (3g) → gold goes down, seeds are added, stock goes down.
- [ ] **×5** → buys 5 (or as many as you can afford / are in stock); the footer says "Bought N× … for Ng."
- [ ] Not enough gold → the Buy button is greyed out.
- [ ] Buy out a stock item → the button is greyed out ("stock 0").
- [ ] Sell 1 Turnip (about 6g) → gold goes up.
- [ ] **Sell all** of an item with a large stack → a **"market NN%"** tag appears, and the price per unit falls as you sell more.
- [ ] **F1** → Oswin's stock refills; prices recover partway.
- [ ] Close with Tab or Esc → the game resumes.
- [ ] Walk to **Brenna** (the blacksmith, left of Oswin) → "[F] Talk to Brenna" → **"Show me your wares."** → her shop opens; she sells weapons and armor and buys materials and equipment.
- [ ] Near a merchant there is only **one** prompt ("Talk to …"), never a separate "Trade with …" prompt.
- [ ] Brenna's sell list shows **only** materials and equipment (no food or seeds). Oswin's shows no equipment.
- [ ] Sell Iron Scrap and Bandit Cloth to Brenna → gold goes up.

---

## 11. Disciplines & XP (Milestone 4d)

- [ ] **C** → the character screen opens and pauses: "Renown 2", Combat level 1, Farming level 1, XP bars, 0 points.
- [ ] Close with **C** or **Esc** → resumes.
- [ ] Kill a Bandit → "+25 Combat XP". Kill the Dummy → "+2 Combat XP".
- [ ] **Parry** a Bandit → "+3 Combat XP".
- [ ] Harvest a Turnip → "+6 Farming XP" per Turnip (Healroot +8).
- [ ] Reach 100 Combat XP (4 Bandits or F6) → **"Combat level 2! (+1 skill point)"**; max HP goes up (the HP bar number increases); Renown becomes 3.
- [ ] **F6** repeatedly → multiple level-ups in a row work; leftover XP carries over to the next level.
- [ ] **F7** → Farming levels up; the character screen shows Farming points.
- [ ] Level up while damaged → your HP **fill percentage** stays the same (you don't heal to full).
- [ ] Die and respawn → levels and XP are kept.

## 12. Equipment & Stats (Milestone 4d)

- [ ] Character screen → Equipment shows Weapon 1: Rusted Sword & Shield **(active)**, Weapon 2: Rusted Greatsword, Body: Gambeson; other slots are empty.
- [ ] The stats panel shows sensible values (Health 150+, Attack 100, Defense 10…).
- [ ] Buy an **Iron Sword & Shield** from Brenna → click it in the inventory → it **replaces the active weapon**; the Rusted one goes back to the bag. Attack goes up to 115 and Defense rises.
- [ ] Buy a **Padded Cap**, click it → equipped in Head; Defense goes up.
- [ ] Buy an **Iron Helm**, equip it → it replaces the Padded Cap; the cap returns to the bag.
- [ ] Equip the **Soldier's Token** → max stamina goes up (the bar number increases).
- [ ] **Unequip** a slot on the character screen → the item goes back to the bag and the stats drop.
- [ ] Unequip with a **full bag** → "Bag is full"; the item stays equipped.
- [ ] Unequip Weapon 1 while it's active → the active weapon switches to Weapon 2 and the style changes.
- [ ] Unequip **both** weapons → you still fight with the class's default style; no errors.
- [ ] Hit the Dummy with Rusted vs Iron weapons → Iron shows bigger damage numbers.
- [ ] Wear the Chain Hauberk and take a Bandit hit → the red damage number is smaller than without armor.
- [ ] Only the **active** weapon's stats count: press X to swap and watch Attack change on the character screen.

## 13. Skills (Milestone 4d)

- [ ] Character screen, Combat at level 1 → **Veteran's Vigor** shows "Available" once you have a point; War Cry shows "Requires Combat level 2".
- [ ] Unlock **Veteran's Vigor** → max HP +10%; the point is spent; the skill shows "Unlocked".
- [ ] Reach level 2 (F6) → unlock **War Cry** → it's automatically placed on **E**. The action bar (bottom-right) shows "WC" in the **E** slot with "War Cry" under it.
- [ ] Press **E** near enemies → a shout knocks them back and deals poise damage. Attack shows +20% on the character screen for 10 s, then returns to normal. A cooldown counts down on the HUD (18 s).
- [ ] Press E again during the cooldown → nothing happens.
- [ ] Shield Charge shows "Requires War Cry" until War Cry is learned; the Unlock button is disabled.
- [ ] Unlock **Shield Charge** (level 3) → placed on **Q**. With the Sword & Shield: Q → a 5 m charge into the enemy with a big knockback.
- [ ] Swap to the Greatsword (X), press Q → notice "Shield Charge needs Sword & Shield"; no cooldown is used.
- [ ] Unlock **Whirlwind** (level 4) → "Set E" / "Set Q" buttons appear. Assign it to Q → Shield Charge is removed from Q. With the Greatsword: Q → a 360° spin hits all enemies around you.
- [ ] Assign a skill that's on E to Q → the two slots swap.
- [ ] Use a skill mid-combo, after a swing's strike → the skill cancels the recovery. Mid-dodge or while staggered → it doesn't fire.
- [ ] **Second Wind** → a parry now restores stamina.
- [ ] **Iron Will** → max poise goes up (you're harder to stagger).
- [ ] Farming (F7 for points): **Green Thumb** → sometimes +1 extra produce. **Deep Roots** → crops survive 1 extra dry day before wilting and dying. **Seed Keeper** → sometimes "Seed saved!" when planting.
- [ ] Knight skills show as available for the Knight class (no "Knight only" lock).

---

## 13b. Dialogue & Quests (Milestone 4e)

### Dialogue box
- [ ] Talk to **Oswin** (F) → the game pauses; a bottom box shows a **green portrait with "O"**, the name "Oswin" and text typing out.
- [ ] Press Space / F / click **while the text types** → the whole line appears at once; press again → next line.
- [ ] **Regression (blank text bug):** spam-click rapidly through a whole conversation with Brenna and Oswin (including during typing and on choices) → the text **never goes blank**, and nothing gets stuck.
- [ ] The **first** conversation has his introduction; later conversations show a random short greeting instead.
- [ ] Choices appear (numbered). Pick one by **clicking** it, by **↑/↓ then Space/Enter**, and by **number keys** (try each method).
- [ ] Clicking empty space while choices show → nothing happens (no accidental pick).
- [ ] **"Goodbye."** → the box closes and the game resumes.
- [ ] **Esc mid-conversation** → the conversation closes and the game resumes; talk again → it starts cleanly from the beginning.
- [ ] Press **Tab, I, C or J** mid-conversation → nothing happens (the conversation stays open). Only Esc leaves.
- [ ] Finish a conversation by pressing **F** on the last line while standing still → it does **not** immediately reopen.
- [ ] Narration lines (Brenna's quest) show in *italics* with a dark "…" portrait and no name.
- [ ] No interact prompt or quest tracker shows while a conversation is open.

### Story quest chain
- [ ] Oswin has a **"!"** marker above his head at the start.
- [ ] Oswin → "What happened to this land?" → lore lines → "I'll deal with them." → notice **"New quest: Bandit Trouble"**; the tracker (top-right) shows "☐ Defeat Bandit (0/3)".
- [ ] Kill Bandits → the tracker counts 1/3, 2/3, 3/3 → notice "Bandit Trouble: return to Oswin"; Oswin shows **"?"**.
- [ ] Killing the **Dummy** does not count toward the Bandit objective.
- [ ] Talk to Oswin → he thanks you, the quest completes (+50 gold, +60 Combat XP), then **offers** Roots of the Blight with **"I'll go and see her." / "Not now."**
- [ ] Choose **"Not now."** → no quest starts; Oswin keeps a **"!"** marker; his menu now has **"About Brenna's ash..."** → it repeats the offer → accept → **Roots of the Blight** starts and that menu option disappears.
- [ ] Talk to **Brenna** → the ash scene plays; objective 1 is ticked. Talk again → "Still waiting on that Healroot."
- [ ] With **1 Healroot** in the bag → the tracker shows the objective done and "→ Return to Brenna"; Brenna shows "?".
- [ ] Talk to Brenna → the Healroot is **taken**, the quest completes (30g, 30 Combat XP, 40 Farming XP) and the reveal lines play.
- [ ] Oswin's "What happened to this land?" choice no longer appears once Bandit Trouble is done.

### Side quests
- [ ] Oswin → "Need anything?" → "I'll grow them." → **A Taste of Home** starts.
- [ ] Holding 5+ Turnips → the objective shows done; **sell some** to drop below 5 → it un-ticks again.
- [ ] Hand in with 5 Turnips → 5 Turnips removed, +40g, +3 Healroot Seeds, +30 Farming XP.
- [ ] Brenna → "Any work?" → she explains and offers **"I'll find some." / "Not right now."** Decline → no quest, "Any work?" is still there. Ask again → accept → **Scrap Run** starts. Bring 4 Iron Scrap → hand in → 4 removed, +60g, +40 Combat XP.
- [ ] **Every** quest offer in the game shows an accept and a decline choice (Bandit Trouble, Roots of the Blight, A Taste of Home, Scrap Run).
- [ ] Choose "Maybe later." / "Not my problem." → no quest starts; the choice to ask again is still there next time.
- [ ] Start a Collect quest **while already holding enough items** → it's immediately ready ("return to …").

### Contracts board
- [ ] The brown board near the spawn → "[F] Read the Contracts Board" → the board screen opens (game paused).
- [ ] **Accept** "Bounty: Road Bandits" → it shows "In progress" and appears in the tracker (blue title).
- [ ] Kill 3 Bandits → the board shows **Turn in** → rewards granted → "Done today".
- [ ] Bandit kills count for **both** Bandit Trouble and the Bounty if both are active.
- [ ] **F1** (next day) → the contract is available to accept again.
- [ ] Supply Run (6 Turnips) and Cloth for Bandages (5 Bandit Cloth) take the items on turn-in.

### Journal & rewards
- [ ] **J** → the journal opens (paused): active quests with summary, objectives, rewards and who to return to; completed quests are listed. Close with J or Esc.
- [ ] Complete a quest with a **full bag** (item reward) → the reward item drops on the ground next to you instead of vanishing.
- [ ] More than 4 active quests → the tracker shows the tracked quest in full, up to 3 others dimmed, then "+N more"; story quests are listed first.

## 13c. Quest UX, Tracking & Minimap

**Setup:** run **Beast → Setup → Run Milestone 8 Setup** once more (it adds the tracker and minimap to the scene) → "setup complete", no errors. The **QuestUI** object now has Quest Journal, Contract Board Screen, Quest Tracker and Minimap.

### Closing menus
- [ ] Every menu has a **✕** button top-right: inventory (Tab), character (C), journal (J), shops, contracts board. Clicking it closes the menu and resumes the game.
- [ ] **Regression:** open the board → **Accept** a contract → click **✕** → the board closes. Repeat, closing with **Esc**, then with **Tab** → each works.

### Showing progress
- [ ] Accept any quest → a **"QUEST ACCEPTED"** banner with the quest's name appears across the top for about 3 s (also visible while the board is open).
- [ ] Finish all objectives → **"OBJECTIVES COMPLETE — return to …"** banner. Hand in → **"QUEST COMPLETE"** banner.
- [ ] Board rows show a coloured badge (AVAILABLE / IN PROGRESS / READY / DONE TODAY); active contracts show a **progress bar** that fills as you progress.

### Tracking
- [ ] Accept the first quest → it becomes **tracked** automatically: the panel under the minimap shows **◆ Title**, objectives, and "◆ Bandit · 34 m".
- [ ] A **◆ waypoint** appears in the world over the target with the distance; turn away → it pins to the screen edge pointing the right way; walk within about 3 m → it hides.
- [ ] Tracking Bandit Trouble → the waypoint points at the **nearest living Bandit**; kill it → it moves to the next one; when ready → it points at **Oswin**.
- [ ] Tracking A Taste of Home → points at the **field** (Turnips are grown there); when you hold 5 → points at Oswin.
- [ ] Tracking Scrap Run → points at the nearest **Bandit** (they drop Iron Scrap), labelled "Iron Scrap (loot)".
- [ ] Tracking Roots of the Blight → points at **Brenna**.
- [ ] Tracking a contract that's ready → points at the **Contracts Board**.
- [ ] With 2+ quests active, press **T** → "Tracking: …" notice; the panel, waypoint and minimap ◆ all switch. Other active quests show dimmed under the tracked one.
- [ ] Hand in / abandon the tracked quest → tracking moves to another active quest automatically (story first). No quests left → the panel and waypoint disappear.

### Journal
- [ ] **J** opens the journal on the tracked quest. The tabs **All / Story / Side / Contracts / Completed** show counts and filter the list.
- [ ] The list shows ◆ on the tracked quest plus status and progress; clicking a row shows its details (summary, objectives, reward, **where to go + distance**, progress bar).
- [ ] **Track this quest** on another quest → it becomes tracked (button changes to "◆ Tracking").
- [ ] **Abandon** on a side quest or contract → asks "Abandon? Progress will be lost." → **Cancel** keeps it; **Yes, abandon** removes it. You can accept it again from the NPC or board.
- [ ] A **story** quest has no Abandon button ("Story quests can't be abandoned.").
- [ ] The Completed tab lists finished quests with all objectives ticked.

### Minimap
- [ ] A square minimap shows top-right with the world from above, rotating as you turn the camera. Your arrow in the centre points the way your character faces.
- [ ] Markers: red dots for Bandits (not the dummy), yellow dots or **!**/**?** for Oswin and Brenna, a brown square for the board, a blue square for the bed, **N** on the edge for north. Trees show as round canopies, dead trees as grey-purple ones.
- [ ] The tracked quest's **◆** shows on the map, pinned to the edge when it's beyond the map.
- [ ] Characters themselves don't render on the map (only markers); houses and the field do.
- [ ] **M** → a large map in the centre with a legend underneath (game keeps running); **M** again → back to the minimap.
- [ ] The minimap hides while menus or dialogue are open, and returns afterwards.
- [ ] **F5 / F9** with a non-default tracked quest → the same quest is tracked after loading.

## 13d. Polish Pass — UI/UX & Environment Art (Milestone 9)

**Setup:** **Beast → Setup → Run Milestone 9 Setup (Environment Art)** → "environment art complete: N trees…", no errors. A new **Environment_Dressing** object holds roads, fence, props, trees, rocks and grass. Layers **EnvBillboard** and **MinimapOnly** now exist.

### Look of every menu (same theme everywhere)
- [ ] Inventory, Character, Journal, Contracts Board, every Shop and the Pause menu all share the same look: a dark framed window over a **dimmed** game, a gold title, a ✕ top-right, and a grey key-hint line along the bottom.
- [ ] Buttons light up (gold border) when the mouse is over them; disabled buttons are clearly faded and do nothing.
- [ ] Change the **Game view size** (e.g. Full HD, then 1280×720, then Free Aspect very small) → every window and the HUD **scale together**; nothing overlaps or runs off-screen.

### HUD layout (nothing overlaps)
- [ ] Bottom centre: **health** (with number), **stamina** and a thin **poise** bar, with "Knight · style" above them.
- [ ] Bottom right: four action slots **E, Q, R, X** with key chips. E/Q show the skill (dark shade drains while on cooldown, with seconds left); R shows your food and how many; X shows the weapon in hand. Captions never overlap.
- [ ] Bottom left: key hints **Esc Menu · Tab Bag · C Character · J Journal · M Map**.
- [ ] Top right: minimap → **Day / time and gold** strip under it → tracked quest under that.
- [ ] Left side: pickups, gold, XP and messages slide in as small pills and fade after about 4 s.
- [ ] Drop below 30% health → the health bar **pulses red**.
- [ ] The HUD hides while a menu, conversation or the pause menu is open; notifications and quest banners still show.
- [ ] A quest banner that appears while a menu is open sits at the very top and never covers the window's title.

### Interaction, death and sleep
- [ ] Walk up to anything interactive → a pill in the lower middle shows **[F] Action**; information-only prompts (e.g. "watered, growing") show without the key chip, greyed.
- [ ] Die → the screen tints red with **DEFEATED** and a respawn countdown; you respawn after it.
- [ ] Sleep → black screen with the new day, then a fade back in (see §9).

### Inventory, shop, character, journal details
- [ ] Inventory: **click** selects (gold frame) and shows details on the right; **right-click** uses or equips directly; hovering an unselected item shows a small tooltip. Stack counts are readable in a dark corner badge.
- [ ] Inventory: select food → **Use**; select gear → **Equip**; a green line confirms ("Used Bread.") or a red one explains a failure.
- [ ] Shop: unaffordable prices show **red**; sold-out shows "sold out"; every buy/sell prints what happened in the footer (e.g. "Sold 3× Turnip for 15g.").
- [ ] Character: equipment rows show the item name with its bonuses on a second line; long names end in "…" instead of running under **Unequip**.
- [ ] Journal: tabs show counts "(2)"; the tracked quest has a gold diamond; Abandon asks to confirm ("Yes, abandon" / "Keep it").

### Pause menu
- [ ] **Save game** → "Game saved." in green; **Load last save** shows when you last saved (greyed out with a hint if there's no save yet).
- [ ] **Controls** → a list of every key appears on the right; **Hide controls** removes it.
- [ ] **Quit to desktop** → asks to confirm; **Cancel** keeps playing; **Quit** stops Play Mode (in a build, closes the game).
- [ ] **Tab / I / C / J** do **not** close the pause menu (only Esc, Resume or ✕).

### Environment art
- [ ] Houses are timber-framed with tiled roofs, a chimney, a door facing the village square and windows; the tower is stone with battlements; steps are stone; the ramp, bed and notice board are wood (the board has posts and pinned notices).
- [ ] A cobbled **village square** sits between Oswin, Brenna and the board; **dirt roads** lead to each door, the field gate, the ramp, the steps, and a wider road leads out past the bandits to the forest edge.
- [ ] The field has a wooden **fence** with a gate gap facing the square; the cobbles never cover the field; tilled tiles show furrows and darken when watered.
- [ ] A **well**, crates and barrels by Oswin, and an anvil by Brenna.
- [ ] A **forest ring** closes off the map edge; trees, bushes, rocks, grass tufts and flowers are scattered around, with more **dead, blighted trees toward the bandit side**. Nothing blocks a door, NPC, the field, a road or the bandit fighting area.
- [ ] Trees, bushes and grass always face the camera as you orbit, and **their shadows keep their shape** (they don't go paper-thin).
- [ ] Walk into a tree trunk or a large rock → it blocks you. Bushes, grass and flowers don't.
- [ ] Stand in a building's or tree's shadow → your sprite darkens (shade check), just like near houses before.
- [ ] Distance fog softens the far forest; the **minimap is not foggy** and shows round canopies for trees.
- [ ] Select a tree in the **Scene view** (outside Play Mode) → it's visible there too and faces the scene camera.

## 13e. Light Reputation (Milestone 10)

**Setup:** **Beast → Setup → Run Milestone 10 Setup (Reputation)** → "reputation setup complete", no errors. Select **Player** → a **Reputation** component with Config = `ReputationConfig`. The contracts board now lists **4** contracts. **F8** gives +50 standing for quick testing.

### Seeing your standing
- [ ] Press **J** → under the quest list, a card shows **Free Hollows · Stranger**, an empty bar, "Standing 0 / 50 · next: Known" and "Standard prices".
- [ ] Talk to Oswin → **I'd like to trade** → the header shows "Standing: **Stranger** (standard prices)". Note a price, e.g. Healing Draught.
- [ ] Press **C** → the header shows "Renown N · Free Hollows: **Stranger**".

### Earning standing
- [ ] Kill a Bandit → the left feed shows **+2 Standing** (next to the XP note).
- [ ] Keep killing Bandits → after 10 kills (+20) in one day, kills stop giving standing. Press **F1** (+1 day) → the next kill gives +2 again.
- [ ] Sell or buy about 25 gold worth of goods → **+1 Standing**. Trading gives at most +15 per day.
- [ ] Turn in any quest → the QUEST COMPLETE banner lists "+N standing" in its rewards, and the feed shows **+N Standing**.
- [ ] Reach 50 standing (F8 is fine) → a green **STANDING RAISED** banner: "Free Hollows: Known · 5% better prices". It plays **after** any quest banner instead of replacing it.

### What standing does
- [ ] As **Known**, open Oswin's shop again → the header says "(5% better prices)". Pricier items cost less than before and sell for a little more. Very cheap items (1–3g) may not change: they round to the same gold.
- [ ] Before **Trusted**, the board shows **Town Patrol** with a red **LOCKED** badge and "Requires Trusted standing" instead of Accept.
- [ ] Reach **Trusted** (150) → Town Patrol shows **AVAILABLE** with Accept. Accept it, kill 5 Bandits, turn it in on the board → 90 gold, 70 Combat XP, +20 standing.
- [ ] As **Trusted**, talk to Oswin → he thanks you **once** and gives **2 Bread and a Healing Draught**. Talk again → no second gift.
- [ ] Ask Oswin **"How does the town see me?"** → his answer matches your tier (stranger / known / trusted / friend / hero).
- [ ] Reach **Friend** (300) and talk to Brenna → she gives you an **Iron Helm**, once. With a full bag, the helm drops at your feet instead of vanishing.

### Losing standing
- [ ] Accept a contract, then **J → Abandon** → the warning reads "Abandon? Progress is lost and you lose 5 standing." Confirm → the feed shows **−5 Standing**.
- [ ] Abandoning just below a tier threshold → a red **STANDING LOWERED** banner, and gated contracts lock again.
- [ ] Standing never drops below 0.

### Save / load
- [ ] Note your standing, press **F5**, gain more (F8), press **F9** → standing is back to the saved value and the journal card matches.

---

## 13f. Climbing & Ledges (Milestone 11)

**Setup:** **Beast → Setup → Run Milestone 11 Setup (Climbing)** → "climbing setup complete", no errors. Select **Player** → it has a **Player Climber**. A **Climbing_Course** object sits west of the village (press **M**: grey blocks). Run Milestone 9 again afterwards if you like; trees will keep clear of the course.

### Vaulting (the two short blocks)
- [ ] Run at the **1.4 m block** and press **Space** → instead of bumping into it you're pulled up onto it in a quick motion (Climb frames, arms up).
- [ ] Same with the **2.3 m block** → also vaulted, a little slower.

### Hanging (the long 3.2 m wall)
- [ ] Run at the long wall and press **Space** → your hands catch the top edge and you hang (back view, arms up, slow sway). The prompt spot shows **[Space] Climb up · [A / D] Shimmy · [Shift] Let go**.
- [ ] The **stamina bar drains slowly** while hanging, faster while shimmying.
- [ ] **A / D** → you shimmy along the wall and **stop at its end** (you don't float past it).
- [ ] **Space** → you pull yourself up onto the wall.
- [ ] Hang again, hold **S** (away) and press **Space** → you leap backwards off the wall.
- [ ] Hang again, press **Shift** → you drop.
- [ ] Hang until stamina runs out → "Too tired to hold on" and you fall.
- [ ] While hanging, **LMB, RMB, R, E/Q and F do nothing**.

### Ivy cliff (behind the wall)
- [ ] Stand on top of the long wall facing the ivy → **[Space] Climb** shows. Press it → you grab the ivy.
- [ ] **W / S / A / D** move up, down and sideways; holding still freezes the animation and drains stamina slowly.
- [ ] Climb to the top → you pull up onto the cliff. Climb down from the ground side → you step off when your feet touch the floor.
- [ ] Get hit by a Bandit while climbing (lure one over) → "Knocked off!" and you fall.

### Where climbing must NOT happen
- [ ] Jump at a **house** wall → no grab (houses are marked Not Climbable).
- [ ] Jump in the open, next to fences, trees and the board → normal jumps, no strange grabs.
- [ ] **F5** while standing, then grab a ledge and **F9** → you're back where you saved, not stuck hanging.
- [ ] **Esc → Controls** lists "Space at a ledge" and "Space at ivy".

---

## 13g. Sprite Shadows (Milestone 12)

**Setup:** **Beast → Setup → Run Milestone 12 Setup (Sprite Shadows)** → "sprite shadow setup complete", no errors. `Art/Materials/Sprite_Lit` now uses the **Beast/Sprite Lit** shader.

- [ ] Stand in open sun → your sprite is evenly lit: **no dark half or stripe** from its own shadow.
- [ ] Walk slowly into a building's or the ivy cliff's shadow → the shadow line **moves up your body** (legs dark first, then head), instead of the whole sprite dimming at once.
- [ ] Stand where a Bandit's shadow (or its sword's) falls on you → that shadow shows on your sprite.
- [ ] Trees, bushes and grass in a building's shadow are darker than those in sun; trees aren't darkened by their own shadow.
- [ ] Hit flashes (white/red glow) still work, and fog still fades distant sprites.
- [ ] Rotate the camera all the way round a character in sun → no flicker or dark patches at any angle.

---

## 13h. Smarter Enemies (Milestone 13)

**Setup:** **Beast → Setup → Run Milestone 13 Setup (Smarter Enemies)** → "smarter-enemies setup complete", no errors. A **Bandit_C** now stands at the camp. In Play Mode a **[Navigation]** object appears in the Hierarchy.

- [ ] Stand on the far side of a house from a Bandit (within ~12 m) → it walks **around** the house to you instead of pushing into the wall.
- [ ] Fight all three Bandits at once → **no more than two** come at you at a time; the third circles about 3.5 m away and steps in when one backs off.
- [ ] The Bandits don't stack on top of each other.
- [ ] Climb onto the long wall of the climbing course while a Bandit chases you → it waits at the foot of the wall, can't hit you, then after ~5 s **walks home**.
- [ ] Hit a Bandit a few times, then run far away → it walks home and its health bar refills.
- [ ] Optional: select a Bandit in the Scene view during Play → its path shows as a yellow line (red while it's attacking).

---

## 13i. Day, Night & Weather (Milestone 14)

**Setup:** **Beast → Setup → Run Milestone 14 Setup (Day, Night & Weather)** → "day/night & weather setup complete: N lantern(s), campfire at the bandit camp", no errors. A **[Sky]** and a **[Night Lights]** object appear in the Hierarchy. Use **F2** (+1 hour) to move through the day quickly.

- [ ] Morning (start) → the world looks like before; the HUD clock shows a **sun** icon.
- [ ] Skip to ~19:00 → the light turns orange and shadows grow long; lanterns start to glow.
- [ ] Skip to ~22:00 → night: dark blue, a dim moon still casts soft shadows, the clock shows a **moon**, lanterns and the campfire glow warm. You can still see where you're going.
- [ ] Skip to ~06:00 → dawn: the moon fades, the sun rises orange, lanterns go out.
- [ ] Press **F4** → Cloudy (grey, soft shadows, cloud icon) → **F4** → Rain (streaks of rain, fog closes in, rain icon) → **F4** → Clear again.
- [ ] Till and plant a tile, then press **F4** until it rains → the soil turns dark (watered) without using the watering action.
- [ ] Skip several days (F1) → the weather changes now and then (roughly 1 day in 5 is rainy, day 1 is always clear). On a rainy morning: "It's raining: the field is watered today."
- [ ] **F5** while raining, **F4** to clear, **F9** → it's raining again.
- [ ] Sprites (you, Bandits, trees) still receive shadows correctly at every time of day.

---

## 13j. Night Danger & Fatigue (Milestone 15)

**Setup:** **Beast → Setup → Run Milestone 15 Setup (Night Danger & Fatigue)** → "night danger & fatigue setup complete", no errors. Select **Player** → it has a **Player Fatigue**.

- [ ] Skip to ~21:00 (F2) → "Night falls. Bandits are bolder now, and carry more." Bandit name plates turn **red with a moon**.
- [ ] At night, Bandits notice you from further away (about 17 m instead of 12 m), move a bit faster and hit harder.
- [ ] Kill Bandits at night → noticeably more loot and gold than by day.
- [ ] Skip to morning → "Dawn breaks." and the name plates are normal again.
- [ ] Don't sleep: around midnight → "You're getting tired…" and a gold **TIRED** badge above the health bar; the stamina bar is shorter (C: the Stamina stat is lower).
- [ ] Keep going to ~04:00 → "You're exhausted…" and a red **EXHAUSTED** badge; attack is lower too.
- [ ] Sleep → the badge disappears and stamina is back to full size.
- [ ] **F5** while exhausted, sleep, **F9** → exhausted again.

---

## 13k. Main Menu & Save Slots (Milestone 16)

**Setup:** **Beast → Setup → Run Milestone 16 Setup (Main Menu & Save Slots)** → "main menu & save slots setup complete", no errors. **File → Build Profiles** (or Build Settings) lists Bootstrap, **MainMenu**, World_Test in that order.

- [ ] Open **Bootstrap** (or **MainMenu**), press Play → the title screen: **BEAST** over a dusk sky with stars, a moon and slowly drifting hills. The cursor is visible.
- [ ] If you've saved before, **Continue** is at the top with "Slot 1 · Day … · played …" under it. (Saves from before this milestone show "Day ?" and "0 min" until saved again.)
- [ ] **New Game** → three slot cards. Click **Start here** on an empty slot → the world loads on **Day 1, 08:00**. **Esc** → the pause title shows **Slot N**.
- [ ] Play a bit (earn some gold, **F1** a day), **Esc → Save game** → "Game saved."
- [ ] **Esc → Quit to main menu** → it asks "Unsaved progress will be lost" → **Quit** → back on the title screen.
- [ ] **Load Game** → that slot's card shows Day / time, your gold, Combat and Farming levels, standing, play time and the save date.
- [ ] **New Game → Start over here** on a used slot → it asks first ("Start over? Slot N's save will be lost."). **Cancel** leaves it alone.
- [ ] Start a new game in a **different** slot → day 1, starting gold. Quit to the menu → the first slot is unchanged; **Continue** now loads the newer one.
- [ ] **Load** the first slot → everything is as you saved it. **Sleep** in the bed → only this slot is updated (check the cards' save times).
- [ ] **F5 / F9** save and load the slot you're playing (the DEV line shows "slot N").
- [ ] **Load Game → Delete** → it asks first; **Delete** → the card says Empty.
- [ ] Press Play directly in **World_Test** → the menu is skipped, as before (you play in slot 1).

---

## 13l. Settings (Milestone 17)

**Setup:** none. Press Play in **Bootstrap** (or MainMenu).

- [ ] Main menu → **Settings** → a window with four tabs: Controls, Display, Audio, Interface. **Esc** or **Back** returns to the menu.
- [ ] **Controls:** drag **Mouse sensitivity** to about 2× → in the world the camera turns noticeably faster. **Invert vertical look** On → moving the mouse up looks down.
- [ ] **Display:** **Shadows Off** → shadows disappear (and characters no longer darken in shade); **High** brings them back. **Render scale 50%** → the world gets blurry, the UI stays sharp. Turn **VSync** off → **Frame rate limit** becomes clickable.
- [ ] **Interface:** **Interface size 120%** → every menu and the HUD get bigger; 80% smaller. **Damage numbers Off** → hitting the dummy shows no numbers (a parry still shows PARRY!). **Camera shake** to Off → heavy hits no longer shake the camera.
- [ ] **Audio:** the sliders move and save (what they do: see 13p).
- [ ] **Esc** in the world → **Settings** → **Esc** goes back to the pause menu (not straight into the game); **Esc** again resumes.
- [ ] Stop Play, press Play again → all your settings are still as you left them. They're the same in every save slot.
- [ ] **Reset to defaults** → asks first → **Reset** puts everything back (window mode and resolution stay).
- [ ] *(In a build only)* change **Resolution** or **Window mode** → "Keep these display settings? Reverting in 12 s". Don't click anything → it goes back by itself. Change it again and click **Keep** → it stays.
- [ ] After stopping Play, the project's **PC_RPAsset** (Assets/Settings) still has Render Scale 1 and its original shadow settings (the game only changes a runtime copy).

---

## 13m. QA Fixes (2026-10-06)

**Setup:** run **Beast → Setup → Repair → Add Roof Colliders** once (and Milestones 12, 13, 15, 16 if you haven't).

- [ ] Walk to any edge of the world (past the trees) → an invisible wall stops you; you never fall off.
- [ ] Stand in the field with the house just behind you and swing the camera over its roof → the camera stops at the roof and moves closer instead of going inside it.
- [ ] Hold F on a ripe crop → it's harvested, and the tile stays empty (no seed used, no watering) until you press F again.
- [ ] A HUD notice (e.g. "+3 Turnip") fades out completely, box included.
- [ ] Character screen: each skill's "Requires Combat level N" / "Needs 1 Combat point(s)" shows in full under its Learn button.
- [ ] Accept a contract on the board → the banner is a slim line at the very top while the board is open.
- [ ] Bread's details say "Restores 30 health." (no "0 stamina").
- [ ] With a Healroot already in your bag, talk to Brenna about the ash → she completes Roots of the Blight in the same conversation.
- [ ] Build Settings (File → Build Profiles) lists Bootstrap, MainMenu, World_Test only.

---

## 13n. Item Icons (Milestone 18)

**Setup:** run **Beast → Setup → Run Milestone 18 Setup (Item Icons)**.

- [ ] Open the bag (**I**) → every item has a little picture instead of a coloured square with a letter (bread loaf, red flask, turnip, seed pouches, swords, helmet...).
- [ ] The shop, the character screen's gear slots and the quick-item slot on the HUD show the same pictures.
- [ ] Kill a bandit or hit the dummy → the dropped loot shows its picture on a small card that always faces you (gold is a coin stack), bobbing; it still flies to you when you're close.
- [ ] Give an item your own sprite in its **Icon** field and re-run the setup → your sprite is kept.

## 13o. Character Creator & Weapon Looks (Milestone 19)

**Setup:** run **Beast → Setup → Run Milestone 19 Setup (Character Look)**. Press Play in **Bootstrap**.

- [ ] **New Game** → pick a slot → the **Create your hero** screen: a preview of your character idling, with **Hair**, **Hair colour**, **Skin**, **Outfit**.
- [ ] Change each option → the preview changes at once. **<** / **>** turn the preview; **Preview with: Greatsword** shows the two-handed sword and no shield.
- [ ] **Randomise** → a random look. **Back** / **Esc** → back to the slot list.
- [ ] **Begin your journey** → in the world your character has the look you picked.
- [ ] Open the character screen and equip a **greatsword** → your character now holds a long two-handed blade and has no shield. Equip a **sword & shield** (or swap weapons with the weapon-swap key) → the shield is back.
- [ ] Attack with each → the strike animation shows the right weapon.
- [ ] Save, quit to the menu, **Continue** → same look. Start a new game in another slot with a different look → each slot keeps its own.

## 13p. Sound (Milestone 20)

**Setup:** run **Beast → Setup → Run Milestone 20 Setup (Sound)**. Turn your speakers on.

- [ ] Title screen: a quiet wind. Clicking buttons makes a soft tick.
- [ ] In the world by day: wind and birds. Wait until night (or sleep past 21:00) → crickets instead. Rainy day → rain on top.
- [ ] Run → footsteps. Attack → a swoosh (deeper with the greatsword). Hit the dummy → a thud; block a bandit → a clang; parry → a ring. Dodge → a whoosh. A bandit dies → a heavier thump.
- [ ] Pick up loot → a blip; gold → coins. Eat bread → crunching; drink a draught → gulps.
- [ ] Farm: till, plant, water and harvest each have their own sound.
- [ ] Accept a quest, finish one, level up, sleep → a short chime for each.
- [ ] **Settings → Audio:** **Sound effects** to 0 → swings and hits go silent; **Ambience** to 0 → wind, birds and rain fade out; **Interface sounds** to 0 → no clicks or chimes; **Master** lowers everything.
- [ ] Bandits far away are quiet or silent; close ones are louder and come from their side.

## 13q. UI Restyle (Milestone 21)

**Step 1 — theme, fonts, title screen.** No setup. Press Play in **Bootstrap**.

- [ ] Title screen: a dark stormy sky with slowly drifting clouds and falling ash, the hills in front, and **B E A S T** in wide capitals at the upper right.
- [ ] The menu is a plain list on the left. Moving the mouse over an item puts a white brush swash behind it (dark text on the swash). **Up / Down** (or W / S) move it too; **Enter** chooses.
- [ ] With no saves, **Load Game** is greyed out and can't be chosen; with a save, **Continue** is first and the line under the list names the slot, day and play time.
- [ ] **New Game** → a parchment window with the three slots as paper cards and black brush buttons. Hovering a button turns it red.
- [ ] **Start here** → the character creator on parchment: the preview on a black stage; swatches with a red frame on the chosen one; Sword & Shield / Greatsword as cards, the chosen one red.
- [ ] Bottom right shows **[Esc] Back** on these pages; Esc goes back.
- [ ] Everywhere (menus and HUD) the text is in the new fonts and nothing is cut off.

**Step 2 — the game menu.**

- [ ] **Esc** in the world → a parchment screen with a black bar along the top: **Q · MAP · JOURNAL · BAG · CHARACTER · OPTIONS · E**, with Renown and gold on the right. **Options** is highlighted (parchment block, dark text). The world behind is hidden and paused.
- [ ] Options shows four tiles (Controls, Display, Audio, Interface; hovering one turns it red) and black brush buttons: Resume, Save game, Load last save (with the last save time under it), Quit to title, Quit to desktop.
- [ ] Click **Audio** → the volume sliders appear in place, with a ‹ button and the title AUDIO. Drag a slider (red fill, black handle). **Esc** goes back to the tiles (the menu stays open); **Esc** again closes the menu.
- [ ] **Controls** tile → sensitivity rows on the left, every key listed on a paper card on the right.
- [ ] **Quit to title** asks first (red text, Quit / Cancel); **Esc** cancels the question without closing the menu.
- [ ] In the world press **C** → the menu opens on Character. **E** moves one tab right (Options), **E** again wraps to Map, **Q** goes back. Clicking a tab name switches too.
- [ ] Inside the menu, **J** jumps to Journal; **J** again closes the menu. Same for **Tab / I** (Bag) and **M** (Map).
- [ ] **M** in the world → the Map tab: the map, framed, with the legend under it; time is frozen. The corner minimap is unchanged while playing.

**Step 3 — the tabs' content.**

- [ ] **Bag:**
  - Category cards on the left (All, Food, Materials, Seeds & crops, Gear) show their counts. Picking one shows only those items; All shows every slot, empty ones too.
  - Hovering a slot shows a dark tooltip. Clicking it frames it in red and fills the black panel on the right: big icon, name, details, **Use** or **Equip**.
  - Right-click still uses or equips straight away.
- [ ] **Character → Combat:**
  - Skills are diamonds in rows by tier, joined by lines. A gold rim means learned, black means learnable now, grey means locked.
  - Clicking one turns its rim red and shows it in the black panel.
  - **Learn** works when you have a point. A learned active skill has **Set E / Set Q**, and its diamond then shows E or Q.
- [ ] **Character → Farming:** the same for the farming skills.
- [ ] **Character → Gear:**
  - Your own character (your chosen look) stands in the middle, idling.
  - Weapons and the accessory are on the left; head, body and legs are on the right. The weapon in hand says "in hand".
  - Clicking a slot shows it in the black panel with **Unequip**, the spare gear in your bag that fits it (each with **Equip**), and all your stats.
- [ ] **Journal:**
  - Filter cards with counts, and your standing with the town at the bottom left.
  - Quests are paper rows; the tracked one has a gold diamond, and the selected one is red.
  - The black panel shows the summary, objectives (done ones in green), reward and where to go, with **Track this quest** and **Abandon** (side quests and contracts only).
  - Abandon asks first; **Esc** cancels the question without closing the menu.

**Step 4 — pop-ups and HUD.**

- [ ] **Shop** (talk to the merchant):
  - A parchment window with the merchant's name in spaced capitals, your gold and your standing.
  - Buy and Sell columns of paper rows, with **Buy / ×5** and **Sell / All** brush buttons. Prices you can't afford are red.
  - A trade message shows at the bottom. **Esc** leaves.
- [ ] **Contract board:**
  - Contracts are paper cards with a small status line (Available, In progress, Ready, Done today, Locked), the summary, objectives and reward.
  - **Accept / Turn in / Track** buttons. The tracked contract has a red frame.
- [ ] **Dialogue:**
  - A dark band across the bottom of the screen, with the portrait on a paper mat and the speaker's name in red capitals.
  - Answer choices are paper rows with number keys; the selected one is red.
  - **[Space] Continue** shows bottom right.
- [ ] **HUD:**
  - Thin red health bar with the numbers above it, and thin stamina and poise bars under it. KNIGHT and your fighting style show above the bar.
  - Action slots and key caps are dark and see-through.
  - The clock and gold sit on a dark panel under the minimap, which has a thin dark frame.
  - Pickup notices show on dark pills.
- [ ] Accept a quest → the banner is a dark band with the quest title in spaced capitals and a coloured line under it.
- [ ] Stay up past midnight (Tired) → "T I R E D" shows under the bars.
- [ ] Get defeated → "D E F E A T E D" in red. Sleep → "D A Y  N" fades in and out.
- [ ] A quest banner while the menu is open shows as a slim strip just under the black bar.
- [ ] Title screen → **Settings** → a parchment window with the four categories as cards along the top, Reset to defaults and Back as brush buttons.

## 13r. Fall Damage & Climbing Down (Milestone 24)

Run **Beast → Setup → Run Milestone 24 Setup (Fall Damage & Climbing Down)** first → "Milestone 24 setup complete". A 10 m ivy tower now stands on the climbing course, just south of the ivy cliff.

- [ ] Climb onto the 3.2 m ledge wall, walk to its east edge (the side with the wooden lip) and stop, facing the drop → **[Space] Climb down** shows.
- [ ] Press **Space** → you step over the edge, turn and hang from it. Shimmy, pull up and let go all work as usual.
- [ ] On top of the ivy cliff, stop at its edge facing the ledge wall below → **Space** puts you on the ivy. Hold **S** → you climb down and step off onto the wall top. No damage.
- [ ] On the 1.4 m block, stop at the edge → no Climb down prompt (too low to hang from).
- [ ] Holding a direction at an edge and pressing Space still jumps as usual.
- [ ] Walk off the 3.2 m wall → no damage. Jump off the 6 m cliff → a little damage at most (the jump's arc adds height).
- [ ] Climb the 10 m tower, then walk off → you lose about a third of your health, the hit number shows, the camera shakes, a heavy thud plays and you're stunned for a moment.
- [ ] Climb down the tower instead (ivy all the way) → no damage.
- [ ] Jump off the tower and grab the ledge wall or the cliff on the way down → the fall restarts from the grab, so landing from there is harmless.
- [ ] Get defeated by a fall (e.g. walk off the tower with low health) → normal defeat and respawn, with no extra damage after respawning.
- [ ] Save on top of the tower, walk off, and load the save (F9) while still falling → you are back on the tower with no damage.

## 13s. Archers & Jump Links (Milestone 25)

Run **Beast → Setup → Run Milestone 25 Setup (Archers & Jump Links)** first → "Milestone 25 setup complete". A green-clad **Bandit Archer** now stands at the bandit camp.

- [ ] Pull a Bandit to the climbing course and stand on the **2.3 m block** → it vaults up after you and attacks.
- [ ] Stand on the **1.4 m block** → same.
- [ ] Stand on the **3.2 m wall** → melee Bandits wait below and give up after a few seconds (too high to vault).
- [ ] Drop off the wall while a Bandit is on top of a block → it jumps down after you.
- [ ] Approach the camp → the archer keeps ~9 m away; walk at it and it backs off.
- [ ] The archer flashes red while drawing, then an arrow flies in a slight arc. Sidestep during the draw → the arrow misses.
- [ ] Block facing the archer → the arrow is blocked (reduced damage, stamina used). Dodge through it → no damage.
- [ ] Hang or climb within sight of the archer → arrows hit you and knock you off ("Knocked off!").
- [ ] Hide behind a house → the archer walks round until it can see you again.
- [ ] Kill the archer → it drops loot like a Bandit and gives Combat XP. It respawns like the others.
- [ ] At night the archer's arrows hit harder (like other Bandits).

## 13t. Crafting (Milestone 26)

Run **Beast → Setup → Run Milestone 26 Setup (Crafting)** first → "Milestone 26 crafting setup complete". A workbench (a wooden table with a little anvil and pot) now stands beside your bed.

- [ ] Walk up to the workbench → **[F] Use workbench**. Press F → the **Workbench** window opens on parchment and the game pauses.
- [ ] Three cards on the left: **Alchemy, Cooking, Smithing**. The number on each is how many recipes you can make right now (out of how many).
- [ ] Each recipe row shows the result's icon and name, what it needs (short items in red), and what it does.
- [ ] Harvest Healroot, then **Craft** a Healing Draught → 2 Healroot gone, +1 draught, a pouring sound and "Made Healing Draught." With too little Healroot the button is greyed.
- [ ] **×5** makes as many as you can, up to 5, and reports how many.
- [ ] Cook a **Turnip Stew** and eat it → healed, and "+15 Defense 3:00" counts down under the health bar. The Character screen shows the higher Defense. It disappears when the time runs out.
- [ ] Drink a **Stamina Tonic** → stamina refills and a "+30% Stamina Regen" countdown shows.
- [ ] With the Rusted Sword & Shield **equipped**, 6 Iron Scrap and 2 Bandit Cloth → **Smithing → Iron Sword & Shield**: the ingredient line says "(equipped)". Craft → a clang, and you're now holding the Iron Sword & Shield (same slot, still equipped). The rusted one is gone.
- [ ] Padded Cap from 4 cloth goes into the bag; Iron Helm from a Padded Cap you're wearing upgrades it in place.
- [ ] **Esc** closes the window and the game resumes.
- [ ] Save, quit, load → crafted items are still there. Active food buffs end on load (by design).

## 13u. Storage Chest (Milestone 27)

Run **Beast → Setup → Run Milestone 27 Setup (Storage Chest)** first (after Milestone 26) → "Milestone 27 storage setup complete". A wooden chest with iron bands now stands beside your bed.

- [ ] Walk up to the chest → **[F] Open storage chest** → the chest window opens and the game pauses. The top right shows how full the bag and chest are.
- [ ] **Store** moves one, **All** moves the whole stack; **Take / All** on the chest side bring things back. No "+N item" notices pop up while moving.
- [ ] **Store crops, seeds & materials** → all crops, seeds and materials go into the chest; food, potions and gear stay in the bag.
- [ ] With Healroot only in the chest, open the workbench → the Healing Draught row says "(chest)" and can be crafted; the Healroot comes out of the chest.
- [ ] Fill your bag, then Take → "Your bag is full."
- [ ] Save, quit to the menu, load → the chest still holds the same things. A new game starts with an empty chest.

## 13v. Names & the Mirror (Milestone 28)

Run **Beast → Setup → Run Milestone 28 Setup (Names & Mirror)** first → "Milestone 28 names & mirror setup complete … Ink story recompiled". A standing mirror now stands near your bed.

- [ ] New Game → the creator has a **Name** field at the top showing "Wanderer (type a name)". Click it and type a name → it appears; digits and symbols are ignored; it stops at 16 letters.
- [ ] Randomise changes the look but keeps the name.
- [ ] Begin your journey → the name shows in spaced capitals above the health bar (e.g. "E D D A   Knight · Sword & Shield").
- [ ] Character screen → Gear view: the name under your figure.
- [ ] Save, quit to title → Load Game: the slot card's details start with the name.
- [ ] A new game with the name left empty → you're "Wanderer".
- [ ] Walk to the mirror → **[F] Change your look** → "The mirror" window with your current look and name, holding your current weapon.
- [ ] Type a name containing I, C or J → the window stays open (those keys don't close it).
- [ ] Change the hair and name → **Keep this look** → your sprite changes, "You are now …" shows, the HUD name updates.
- [ ] Open it again, change things, then **Cancel** (or Esc) → nothing changes.
- [ ] Save and load → the new look and name are kept.

## 13w. NPC Schedules (Milestone 31)

Run **Beast → Setup → Run Milestone 31 Setup (NPC Schedules)** first → "Milestone 31 NPC schedules setup complete". An empty `[NPC Places]` object holds their spots.

- [ ] In the morning Oswin is at his stall and Brenna at her forge, as before.
- [ ] Around 12:00 (F2 skips an hour) Oswin walks to the well — a slow walking animation along the paths, around houses, not through them. He goes back to the stall at 13:00.
- [ ] At 13:00 Brenna walks to the contract board and reads it; at 14:00 she's back at the forge.
- [ ] Stand right next to someone who is walking → they stop and wait; talk to them as usual (Oswin's shop still opens). Step away → they carry on.
- [ ] Evening: both stand by the well.
- [ ] After 22:30 both have gone home: no sprites, no "!" / "?" markers, no minimap dots, no Talk prompt at their doors.
- [ ] Sleep in your bed → at 06:00 they're back at work straight away.
- [ ] Save at night, load → they're still at home; save at noon, load → Oswin is at the well.

## 13x. Music (Milestone 33)

Run **Beast → Setup → Run Milestone 33 Setup (Music)** first → "Milestone 33 music complete: 4 track(s) written".

- [ ] Title screen → a slow, calm tune (drone, harp, later a flute).
- [ ] Start or continue a game → it crossfades to the day tune (lute and flute) over a couple of seconds.
- [ ] Past 20:00 (F2 skips hours) → the night tune (quiet, sparse).
- [ ] Get a Bandit to chase you → within a second or so the combat music (drum, fast strings) takes over. Kill it or run → about 5 s later the day or night tune comes back.
- [ ] Hitting the Training Dummy doesn't start combat music.
- [ ] Open the menu (Tab) or pause (Esc) → the music gets quieter; close it → back to full.
- [ ] Settings → Audio → **Music** at 0% → silent; back up → it returns. Master volume still affects it.
- [ ] Loops: listen across a loop's end (60 s) — no click or gap.

## 13y. Controller Navigation (Milestone 35)

No setup needed. Plug in a gamepad (Xbox-style layout assumed for the labels).

- [ ] Title screen: press the D-pad → nothing moves, but the swash marks the focused item. Press again → it moves; **A** chooses; **B** goes back from New Game / Load Game / Settings.
- [ ] Creator: the D-pad moves an ink-gold frame between controls; left / right on Hair or Outfit steps through them; on a colour swatch row, left / right move between colours and A picks one; **Random** gives a name.
- [ ] Settings (title and Options tab): sliders move with left / right; toggles flip with A; lists scroll to keep the focus visible.
- [ ] In game: **View** opens the bag; LB / RB switch tabs; B closes. In the bag the focused slot's details show; A uses or equips.
- [ ] Character screen: skills and gear slots can be reached and chosen with the D-pad and A.
- [ ] Shop, contract board, workbench, chest and mirror: everything reachable with the D-pad; A buys / crafts / stores / accepts; B leaves.
- [ ] While using the pad, hints show pad buttons ([A] [B] [LB / RB], RB / RT / ↓ / → on the action slots, ↑ for interact). Touch the keyboard or mouse → they switch back.
- [ ] Moving the mouse hides the focus frame; the mouse still works everywhere as before.
- [ ] Keyboard only: arrow keys + Enter navigate the same way (W / S still move the title menu; typing a name isn't interrupted).

## 13z. Cleanups & Windows Build (Milestone 23)

- [ ] **Beast → Setup → Run Milestone 23 Setup (Cleanup)** → "SampleScene deleted"; `Assets/Scenes` is gone; Build Settings lists Bootstrap, MainMenu, World_Test.
- [ ] Open World_Test without pressing Play → the player, enemies and NPCs show as their sprites (front idle frame) in the Scene view, turning to face the scene camera as you orbit. Selecting one shows its cyan box.
- [ ] **Beast → Show Character Sprites in Edit Mode** unticks → back to nothing drawn; tick it again → they return. The scene isn't marked as changed by any of this.
- [ ] **Beast → Build → Windows (Release)** → the build finishes and Explorer opens `Builds/Windows` with Beast.exe. Run it → the title screen, music, a new game, play a minute, save, quit, run again, Continue → all work. No dev overlay or debug keys in this build.
- [ ] **Windows (Development)** → same, into `Builds/Windows (Dev)`, with the dev overlay and F-keys.
- [ ] `git status` doesn't list anything under `Builds/`.

## 13aa. Batch 2 setups (Milestones 37–53)

Run, in order: **Beast → Setup → Run Milestone 37, 40, 43, 44, 45, 46, 47, 48, 49, 51, 53 Setup**. Each ends with "… setup complete" and no errors.

## 13ab. Conversation camera (Milestone 36)

- [ ] Talk to Oswin → you and he turn to face each other; the camera glides (about ¾ s) to a shot from behind your right shoulder: your back in the left foreground, Oswin in the middle.
- [ ] His name is centred in gold over a thin gold line; the line sits centred under it and types out without shifting. No portrait box.
- [ ] Choices appear on the right as dark pills with numbered discs; the selected one has a gold edge.
- [ ] Esc → the camera glides back to behind you, facing Oswin.

## 13ac. Chest and minimap trail (Milestones 37, 38)

- [ ] The storage chest stands beside the bed, on the floor, not on the farm field. F opens it.
- [ ] Accept Bandit Trouble → a trail of gold dots on the minimap leads to the nearest bandit, round houses, drifting toward it; the large map (M) shows it too. Stand at the target → it shrinks away.

## 13ad. New enemies (Milestone 40)

- [ ] North-west woods: three Grey Wolves. They chase together, circle behind you, bite, then dart away. Drops: Wolf Pelt.
- [ ] Bandit camp: the Shieldbearer raises his shield; hits from the front are blocked (small numbers) until "GUARD BREAK"; hits from behind land fully.
- [ ] Past the camp, only at night: the Blighted Brute. Its slams can't be interrupted (no stagger mid-swing). Drops Blight Ichor. Gone by day, back the next night.
- [ ] Workbench: Fur Leggings (smithing) and Ichor Draught (alchemy).

## 13ae. Townsfolk (Milestone 43)

- [ ] Maren (by the square, by day): "Could you see to my wounds?" heals you; she sells remedies; Herbs for Maren (3 Healroot).
- [ ] Tobin (by the field): farming tips; Wolves at the Fold (kill 3 wolves).
- [ ] Captain Hale (out at night, asleep in the tower by day): Shield Wall, then The Thing in the Dead Wood.

## 13af. Durability (Milestone 44)

- [ ] Bag / Character → gear shows "Condition N%". Hitting bandits wears your weapon; being hit wears your armour. The dummy doesn't.
- [ ] Below 25%: "badly worn" notice. At 0%: "broke!" and its bonus stops.
- [ ] Brenna: "Can you mend my gear? (N gold)". Workbench → Smithing → Mend with Iron Scrap.

## 13ag. Animals (Milestone 45)

- [ ] A pen near the homestead: three hens and Bess the cow wander inside it.
- [ ] Fill the trough (Animal Feed from Oswin, or 2 turnips). Next morning: "Collect the egg" / "Milk Bess". Pet them daily. Unfed: nothing the next day.
- [ ] Cooking: Fried Eggs, Farmhouse Omelette, Warm Milk.

## 13ah. Seasons (Milestone 46)

- [ ] HUD clock reads "Spr 1 08:00". F1 through the days: Summer on day 15, Autumn 29, Winter 43, with a message each time.
- [ ] Autumn: orange trees and grass. Winter: snow on the ground, bare oaks, no flowers or grass tufts, snowfall instead of rain.
- [ ] Turnips can't be planted in winter (the prompt says why); the first frost kills them. Frost Kale grows only in winter, Pumpkins only in autumn.
- [ ] Spring 8: seeds half price. Autumn 14: crops sell for half as much again. Oswin mentions both.

## 13ai. Foraging and fishing (Milestone 47)

- [ ] Coloured clusters around the woods: "Gather Wild Garlic" (spring), berries, mushrooms, winterberries; back after 3 days.
- [ ] Buy a Fishing Rod from Oswin; at the pond: "Cast a line" → wait → "!" press F → press F when the needle is on the gold, three times. Two misses and it gets away. Night Pike only at night in autumn and winter.

## 13aj. Contracts, upgrades, keys, HUD, names, tutorial (Milestones 48–53)

- [ ] The board shows four contracts, different each day; a taken contract stays until handed in.
- [ ] The plans by the bed: build the Kitchen Garden (a new 4×4 field appears), Larger Chest (100 slots), Copper Still (two servings), Hay Loft (trough lasts 3 days).
- [ ] Settings → Controls → Keys: click a key, press a new one; the HUD and tips use it. Esc cancels. "Reset keys".
- [ ] Bandit plates disappear behind houses; wolves' plates sit low. Lock on → a bar at the top. Getting hit from the side shows a red arrow that way.
- [ ] People use your name; Oswin and Maren notice when you're hurt; Brenna notices broken gear; rain and night change greetings.
- [ ] New game: a "FIRST DAY" card at the top left walks you through moving, farming, talking to Oswin, the dummy and sleeping. Esc → How to play. Settings → Interface → Tutorial hints off hides it.

---

## 14. Full Save / Load Round-Trip (every system)

Set up a varied state, save, change everything, then load:

- [ ] Set up: a partially grown field (some wilted), several items in the bag, some gold spent, a skill unlocked and assigned, Iron gear equipped, the Greatsword active, some Oswin stock bought, market saturation on Turnips, Combat level 3.
- [ ] **F5** → "Slot 0 saved" in the Console (slot numbers in the Console start at 0: slot 0 is "Slot 1" in menus); the notice shows.
- [ ] Change everything: move far away, harvest or plant, buy and sell, equip different gear, swap weapon, gain XP, F1 a few days.
- [ ] **F9** → **everything returns exactly**: position, time and day, bag and gold, every farm tile (growth, watered, wilted, dead), shop stock and "market %", equipment and **active weapon**, levels / XP / points, unlocked skills and E/Q slots.
- [ ] After F9, attack → the combat style matches the restored active weapon.
- [ ] F9 **while the inventory, trade or character screen is open** → nothing loads; the Console shows the warning "[Save] Can't load right now (state: InGameMenu)". Close the screen and F9 → it loads.
- [ ] Sleep in the bed (autosave), move away, F9 → back at the moment you slept.
- [ ] **Stop Play, press Play again, F9** → the save loads into a fresh session correctly.
- [ ] **Start from the Bootstrap scene, then F9** → loads correctly.

- [ ] **Quests & dialogue:** with quests at different stages (one active with partial kills, one ready, one completed, a contract done today) and after talking to Oswin once (intro seen): **F5**, then complete or change things, **F9** → quest progress, ready/completed states, contract "Done today", and Oswin's **short greeting** (intro not replayed) all restore.
- [ ] F9 **during a conversation** → refused with the same warning; the conversation is unaffected.

## 15. Cross-System & Edge Cases

- [ ] Open a menu (Tab, C, trade) during **hit-stop** or right after a hit → the game pauses properly; after closing, speed is normal (no slow-motion stuck).
- [ ] Pause (Esc) mid-attack, resume → the attack continues normally.
- [ ] Open the inventory, then press C → the inventory closes (C closes menus); press C again → the character screen opens.
- [ ] Try to open the inventory while the trade screen is open → nothing (Tab closes the trade screen instead).
- [ ] Die with a War Cry buff active → after respawning, the buff runs out normally; stats are correct.
- [ ] Die while blocking or charging a heavy → you respawn in a normal state.
- [ ] Get killed by a Bandit while loot is flying toward you → no errors; the loot is still collectable.
- [ ] Sleep while a Bandit is chasing you → it works (time skips); no errors.
- [ ] F1 ten times quickly → no errors; crops and shops update each day.
- [ ] Level up and buy gear during the same session, then F5/F9 → stats are recalculated correctly (the HP number matches).
- [ ] Parry, then immediately use a skill → works.
- [ ] Lock onto a Bandit, then open the character screen and close it → the lock is still valid or cleanly released.
- [ ] Hold F on the field while a Bandit attacks you → interacting stops while you're staggered and resumes afterwards.
- [ ] Walk into the Merchant or Blacksmith → you're blocked by their collider; they aren't pushed or broken.
- [ ] Soft-aim and lock-on **never target the Merchant or Blacksmith**.
- [ ] Attack the Merchant or Blacksmith → no damage, no errors.

## 16. Performance Sanity

- [ ] Open **Window → Analysis → Profiler** and play for 1–2 minutes of combat → no steady garbage-collection spikes and no frame-time climbing.
- [ ] The **Stats** overlay in the Game view (top-right "Stats" button) → FPS stays steady during combat, farming and menus.

---

## Known Limitations (not bugs — don't report)

- The inventory, trade and character screens are **mouse-only** placeholder UI (no gamepad navigation yet).
- Thin shadows (e.g. a sword) don't show on other characters; shading happens in whole-sprite bands.
- Enemies walk straight at you and can get stuck behind houses (no pathfinding yet).
- No climbing, day/night lighting, weather or reputation yet.
- The prompt and key hints always show keyboard keys, even on a controller.
- All UI is still the prototype (IMGUI) layer with the shared theme; menus are mouse-driven (no gamepad navigation yet).
- Enemies can't climb or reach you on high ground, so climbing out of reach is always an escape (ranged enemies are planned).
- Falls from any height do no damage yet, and you can't lower yourself from a top edge into a hang (both in the backlog).
- Environment art is generated placeholder pixel art. Re-running Milestone 9 rebuilds **Environment_Dressing** from scratch (same layout every time), so put hand-placed props outside that object.
- Placeholder item icons are coloured squares with a category letter (F food, S seed, C crop, E equipment, M material).
- Merchants have unlimited gold.
- The main menu and Settings are mouse-only, and keys can't be rebound yet. The volume sliders do nothing audible: the game has no sound yet.
- In the Editor, Window mode and Resolution follow the Game view; test them in a build.
- Portraits are coloured placeholder boxes with the speaker's initial.
- Contracts don't rotate yet — the same three are offered every day.
- Journal and contracts board are mouse-only.

---

## Bug Report Template

```
Section / item:   e.g. 12 – "Unequip Weapon 1 while active"
Steps:            1. …  2. …  3. …
Expected:         …
Actual:           …
Console error:    (copy the full red message + first lines of the stack trace)
Happens every time? Yes / Sometimes (x out of y)
```
