// ============================================================================
//  BRENNA — blacksmith, "Brenna's Forge". Blunt, guarded, secretly curious.
//  Gives: Scrap Run (side). Hands in: Roots of the Blight (story, started by Oswin).
//  Gives you an Iron Helm once you're a Friend of the Hollows. Mends worn gear for gold.
// ============================================================================

=== brenna ===
{ quest_state("quest_scraprun") == "ready": -> scrap_done }
{ quest_state("quest_rootsoftheblight") == "active" || quest_state("quest_rootsoftheblight") == "ready": -> roots }
{ standing_tier() >= 3 && not friend_gift: -> friend_gift }
{
- brenna == 1:
    Brenna: You're the one with the green field. Everyone's talking about it.
    Brenna: I'm Brenna. If it cuts or it stops a cut, I make it. And you are?
    Brenna: {player_name()}. Right. I'll remember that if you pay on time.
- broken_gear():
    Brenna: {player_name()}, what did you do to that? It's in pieces. Let me mend it before you get yourself killed.
- wears("weapon_ironswordshield") || wears("weapon_irongreatsword"):
    Brenna: {~That's good iron on your belt. Look after it.|Still swinging that iron, {player_name()}? Keep the edge clean.}
- is_night():
    Brenna: Forge is banked for the night. Talk fast.
- else:
    Brenna: {~Forge is hot. Make it quick.|You again, {player_name()}. Need steel?|Mind the sparks.}
}
-> menu

= menu
+ [Show me your wares.]
    ~ open_shop()
    -> END
+ {repair_cost() > 0} [Can you mend my gear? ({repair_cost()} gold)]
    -> mend
+ {quest_state("quest_scraprun") == "inactive"} [Any work?]
    -> scrap_offer
+ {quest_state("quest_scraprun") == "active"} [About the scrap...]
    Brenna: Four pieces. Bandits carry it around like trophies.
    -> menu
+ [Goodbye.]
    -> END

= roots
{ quest_state("quest_rootsoftheblight") == "ready": -> roots_done }
{ roots > 1:
    Brenna: Still waiting on that Healroot. Just one sprig.
    -> menu
}
Brenna: Oswin sent you? Good. Look at this.
The ash in the barrel shivers, then slides toward the green thread on your sleeve.
Brenna: It crawls toward anything that grows. I've never seen ash do that.
~ complete_objective("quest_rootsoftheblight", 0)
{ quest_state("quest_rootsoftheblight") == "ready":
    Brenna: And you've brought Healroot already? Then let's not wait.
    -> roots_done
}
Brenna: Bring me a sprig of Healroot from your field. I want to see what the ash does with it.
-> menu

= roots_done
Brenna: Let's see...
You drop the Healroot into the barrel. The ash recoils from it, like a hand from a flame.
~ turn_in_quest("quest_rootsoftheblight")
Brenna: Your soil isn't just untouched by the Blight. It's fighting it.
Brenna: Keep that to yourself. There are people who'd burn your field to the ground for a secret like that.
-> menu

= friend_gift
Brenna: Wait. Before you go.
She lifts a helm from the rack. The iron is plain, but the rivets are perfect.
Brenna: Folk keep telling me you're the reason the road's quiet. Least I can do is keep your head on.
~ give_item("armor_ironhelm", 1)
Brenna: Don't get it dented on my account.
-> menu

= scrap_offer
Brenna: Iron's scarce since the war. Bring me four pieces of scrap and I'll pay properly.
+ [I'll find some.]
    ~ start_quest("quest_scraprun")
    Brenna: Good. Bandits carry it around like trophies.
+ [Not right now.]
    Brenna: Suit yourself. The offer stands.
- -> menu

= mend
{ gold() >= repair_cost():
    ~ repair_gear()
    Brenna: {~There. Good as new, near enough.|Held together with good iron and spite. You're welcome.|Stop hitting things with the edge. Or don't. I like the coin.}
- else:
    Brenna: That's {repair_cost()} gold, and you're short. Come back with coin.
}
-> menu

= scrap_done
Brenna: That's good iron. Here.
~ turn_in_quest("quest_scraprun")
-> menu
