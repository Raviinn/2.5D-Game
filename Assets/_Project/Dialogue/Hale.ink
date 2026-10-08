// ============================================================================
//  HALE: captain of what's left of the town watch. Ex-soldier, dry, tired.
//  Works nights. Gives: Shield Wall (side), then The Thing in the Dead Wood (side).
// ============================================================================

=== hale ===
{ quest_state("quest_shieldwall") == "ready": -> shield_done }
{ quest_state("quest_deadwood") == "ready": -> brute_done }
{
- hale == 1:
    Hale: Halt. ...Ah, the farmer. {player_name()}, isn't it? Brenna told me about you.
    Hale: Captain Hale. The watch is me, a spear, and a lot of dark. I keep the night; you keep your field.
- hurt():
    Hale: You're hurt, {player_name()}. Go home. The night will still be here tomorrow.
- weather() == "Rain":
    Hale: Rain keeps the bandits in their tents. Mostly.
- standing_tier() >= 2:
    Hale: {~{player_name()}. The Hollows sleep easier with you about.|Evening, {player_name()}. Anything I should know?}
- else:
    Hale: {~Quiet night so far. Don't jinx it.|Evening. Keep your blade loose.|You're out late, {player_name()}. Good. So am I.}
}
-> menu

= menu
+ {quest_state("quest_shieldwall") == "inactive"} [Need a hand with the bandits?]
    -> shield_offer
+ {quest_state("quest_shieldwall") == "active"} [About the shieldbearer...]
    Hale: Don't hit the shield, hit the man. Get round his side, or hammer the shield till his arm gives.
    -> menu
+ {quest_state("quest_shieldwall") == "done" && quest_state("quest_deadwood") == "inactive"} [What's out in the dead wood?]
    -> brute_offer
+ {quest_state("quest_deadwood") == "active"} [About the thing in the dead wood...]
    Hale: It only walks at night. It doesn't flinch mid-swing, so don't trade blows. Dodge, then punish.
    -> menu
+ [Goodbye.]
    Hale: Stay in the light.
    -> END

= shield_offer
Hale: There's a deserter at the camp with a tower shield. Held a shield wall at the river, by the look of him.
Hale: Arrows bounce off, my spear bounces off. Put him down and the rest lose their nerve.
+ [I'll take him on.]
    ~ start_quest("quest_shieldwall")
    Hale: Side or back. Never the front.
+ [Not yet.]
    Hale: Wise. Come back when you're ready to bleed a little.
- -> menu

= shield_done
Hale: Heard the camp's gone quiet. That was you.
~ turn_in_quest("quest_shieldwall")
Hale: The town owes you. I'll make sure they know it.
-> menu

= brute_offer
Hale: Past the camp, where the trees went black, something walks at night. Big. Wrong. Blight in its veins.
Hale: I've watched it from the road. It hasn't come to town yet. Yet.
+ [I'll hunt it.]
    ~ start_quest("quest_deadwood")
    Hale: Only at night. Bring draughts. And bring yourself back.
+ [That sounds like a bad idea.]
    Hale: It is. That's why I'm asking you and not doing it.
- -> menu

= brute_done
Hale: It's dead? You're sure? ...You smell like it's dead.
~ turn_in_quest("quest_deadwood")
Hale: Take the purse. And sleep. Someone in this town should.
-> menu
