// ============================================================================
//  GAME FUNCTIONS — callable from any conversation.
//  IDs are asset names in lower case, e.g. Quest_BanditTrouble -> "quest_bandittrouble",
//  Item_Turnip -> "item_turnip".
//
//  The functions below the EXTERNAL list are fallbacks so the story can be
//  previewed in the Inky editor without the game running.
// ============================================================================

// Quests ----------------------------------------------------------------------
EXTERNAL quest_state(quest_id)                  // "inactive" | "active" | "ready" | "done"
EXTERNAL start_quest(quest_id)                  // returns true if it started
EXTERNAL turn_in_quest(quest_id)                // returns true if handed in (rewards granted)
EXTERNAL complete_objective(quest_id, index)    // ticks off a Custom objective (index starts at 0)

// Items & gold ------------------------------------------------------------------
EXTERNAL has_item(item_id, count)
EXTERNAL take_item(item_id, count)              // returns true if the player had enough
EXTERNAL give_item(item_id, count)
EXTERNAL give_gold(amount)
EXTERNAL gold()

// Player ------------------------------------------------------------------------
EXTERNAL discipline_level(name)                 // "Combat" | "Farming"
EXTERNAL player_name()                          // the hero's name from the character creator ("Wanderer" if none)
EXTERNAL heal_player()                          // restores the player's health and stamina (healers)
EXTERNAL is_night()                             // true from dusk to dawn
EXTERNAL weather()                              // "Clear" | "Cloudy" | "Rain" (snow in winter counts as Rain)
EXTERNAL hurt()                                 // true below half health
EXTERNAL wears(item_id)                         // true if this item is equipped (weapons: either hand)
EXTERNAL broken_gear()                          // true if anything you have equipped is worn out
EXTERNAL season()                               // "Spring" | "Summer" | "Autumn" | "Winter"
EXTERNAL festival()                             // "Planting Festival" | "Harvest Fair" | "" (none today)
EXTERNAL repair_cost()                          // gold to repair all worn gear (0 when nothing is worn)
EXTERNAL repair_gear()                          // pays and repairs all worn gear; returns true if done

// Standing with the Free Hollows ------------------------------------------------
EXTERNAL standing()                             // points, 0-500
EXTERNAL standing_tier()                        // 0 Stranger, 1 Known, 2 Trusted, 3 Friend, 4 Hero of the Hollows
EXTERNAL change_standing(amount)                // raise (or lower, with a negative amount) standing

// World -------------------------------------------------------------------------
EXTERNAL open_shop()                            // opens this NPC's shop when the conversation ends


=== function quest_state(quest_id) ===
~ return "inactive"

=== function start_quest(quest_id) ===
~ return true

=== function turn_in_quest(quest_id) ===
~ return true

=== function complete_objective(quest_id, index) ===
~ return

=== function has_item(item_id, count) ===
~ return false

=== function take_item(item_id, count) ===
~ return false

=== function give_item(item_id, count) ===
~ return

=== function give_gold(amount) ===
~ return

=== function gold() ===
~ return 0

=== function discipline_level(name) ===
~ return 1

=== function player_name() ===
~ return "Wanderer"

=== function heal_player() ===
~ return

=== function is_night() ===
~ return false

=== function weather() ===
~ return "Clear"

=== function hurt() ===
~ return false

=== function wears(item_id) ===
~ return false

=== function broken_gear() ===
~ return false

=== function season() ===
~ return "Spring"

=== function festival() ===
~ return ""

=== function repair_cost() ===
~ return 0

=== function repair_gear() ===
~ return false

=== function standing() ===
~ return 0

=== function standing_tier() ===
~ return 0

=== function change_standing(amount) ===
~ return

=== function open_shop() ===
~ return
