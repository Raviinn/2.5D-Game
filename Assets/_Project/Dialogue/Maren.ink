// ============================================================================
//  MAREN: healer, "Maren's Remedies". Old, kind, sharp-tongued. Tobin's aunt.
//  Gives: Herbs for Maren (side). Tends your wounds for free.
// ============================================================================

=== maren ===
{ quest_state("quest_herbsformaren") == "ready": -> herbs_done }
{
- maren == 1:
    Maren: Hold still. Let me look at you. Hm. Thin, scratched, still breathing. You'll do.
    Maren: I'm Maren. I mend what the road breaks, and I brew what the field gives. And your name, dear?
    Maren: {player_name()}. A good name. Try to keep it off a gravestone.
- hurt():
    Maren: {player_name()}, you're bleeding on my floor. Sit. Now.
- weather() == "Rain":
    Maren: In out of the wet, {player_name()}. Damp gets into the lungs.
- else:
    Maren: {~Sit, sit. You look like you walked through a hedge.|There's tea if you want it. It's mostly nettle.|Back again, {player_name()}? Show me those hands.}
}
-> menu

= menu
+ [Could you see to my wounds?]
    ~ heal_player()
    Maren: {~There. Clean, bound, and you'll scar handsomely.|Bite your sleeve. Done. Next time, duck.|Nothing a little Healroot salve can't settle.}
    -> menu
+ [What do you have for sale?]
    Maren: Remedies, mostly. Don't drink them all at once.
    ~ open_shop()
    -> END
+ {quest_state("quest_herbsformaren") == "inactive"} [Do you need help with anything?]
    -> herbs_offer
+ {quest_state("quest_herbsformaren") == "active"} [About the Healroot...]
    Maren: Three sprigs. Your field grows it, I'm told. Nobody else's does any more.
    -> menu
+ [Goodbye.]
    Maren: Keep your feet dry.
    -> END

= herbs_offer
Maren: My shelves are nearly bare. The Blight took every Healroot patch from here to the river.
Maren: Three sprigs would keep me brewing for a week. I'll pay in draughts, which is better than coin out there.
+ [I'll bring you some.]
    ~ start_quest("quest_herbsformaren")
    Maren: Bless you. Not too ripe, not too green.
+ [Not right now.]
    Maren: Then I'll make do. Ask me again if your field is generous.
- -> menu

= herbs_done
Maren: Oh, look at them. Green to the root.
~ turn_in_quest("quest_herbsformaren")
Maren: Take these. And come to me before you go anywhere stupid, not after.
-> menu
