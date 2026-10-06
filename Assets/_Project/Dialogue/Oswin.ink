// ============================================================================
//  OSWIN — merchant, "Oswin's Provisions". Warm, tired, practical.
//  Gives: Bandit Trouble (story), A Taste of Home (side).
//  Explains town standing ("How does the town see me?") and thanks you once you're Trusted.
// ============================================================================

=== oswin ===
{ quest_state("quest_bandittrouble") == "ready": -> bandits_done }
{ quest_state("quest_tasteofhome") == "ready": -> turnips_done }
{ standing_tier() >= 2 && not trusted_thanks: -> trusted_thanks }
{ oswin == 1:
    Oswin: A new face! And not a hungry-looking one, for once.
    Oswin: Name's Oswin. I sell what little there is to sell, and buy whatever you drag back from the wilds.
- else:
    Oswin: {~Back again, friend.|Ah, the farmer with the green field.|Still in one piece? Good.|Business is slow. Talk is free.}
}
-> menu

= menu
+ [I'd like to trade.]
    Oswin: Let's see what you've got.
    ~ open_shop()
    -> END
+ {quest_state("quest_bandittrouble") == "inactive"} [What happened to this land?]
    -> lore
+ {quest_state("quest_bandittrouble") == "active"} [About those bandits...]
    Oswin: Three of them, out past the houses to the north-east. Watch for the wind-up before they swing.
    -> menu
+ {quest_state("quest_bandittrouble") == "done" && quest_state("quest_rootsoftheblight") == "inactive"} [About Brenna's ash...]
    -> brenna_offer
+ {quest_state("quest_tasteofhome") == "inactive"} [Need anything?]
    -> turnips_offer
+ {quest_state("quest_tasteofhome") == "active"} [About those turnips...]
    Oswin: Five turnips. Fresh, mind you. I can't sell what doesn't exist.
    -> menu
+ [How does the town see me?]
    -> standing_talk
+ [Goodbye.]
    Oswin: Mind the road.
    -> END

= lore
Oswin: The war ended in a single night. The sky went white, and when it faded, the soil had turned to ash.
Oswin: Nothing grows out here anymore. Nothing... except on your field. Nobody knows why.
Oswin: And now deserters have taken to the hills. They raid anyone who still has food.
+ [I'll deal with them.]
    ~ start_quest("quest_bandittrouble")
    Oswin: You'd do that? Clear out three of them and I'll make it worth your while.
+ [Not my problem.]
    Oswin: It will be, once they smell your crops.
- -> menu

= bandits_done
Oswin: Word travels fast. Three fewer bandits on the road!
~ turn_in_quest("quest_bandittrouble")
-> brenna_offer

= brenna_offer
Oswin: There's something else. Brenna at the forge found ash in her quench barrel that... moves.
Oswin: She won't talk about it with just anyone. But she'll talk to someone who's bled for this town.
+ [I'll go and see her.]
    ~ start_quest("quest_rootsoftheblight")
    Oswin: Good. Mind what she shows you.
+ [Not now.]
    Oswin: When you're ready, then. It isn't going anywhere. Unfortunately.
- -> menu

= standing_talk
~ temp tier = standing_tier()
{ tier:
- 0:
    Oswin: Honestly? You're a stranger with a green field. Folk are curious, and a little scared.
    Oswin: Help people, trade fairly, keep the road safe. They'll warm to you, and so will my prices.
- 1:
    Oswin: People know your face now. Some of them even wave.
    Oswin: Keep at it. Folk who are trusted here get first pick of the work on the board.
- 2:
    Oswin: You're trusted here. The guards even posted a patrol job on the board for people like you.
- 3:
    Oswin: A friend of the Hollows, that's what you are. My best prices are yours.
- else:
    Oswin: The refugees tell stories about you around the fire. The Hero of the Hollows, they call you.
    Oswin: Don't let it go to your head.
}
-> menu

= trusted_thanks
Oswin: There you are! People have been saying your name at the well, and kindly, for once.
Oswin: The refugees, the guards, even Brenna. You've earned this town's trust.
Oswin: Here. Bread from the first proper flour we've had in months, and a draught for the road.
~ give_item("item_bread", 2)
~ give_item("item_healingdraught", 1)
Oswin: And you'll find my prices a little kinder from now on.
-> menu

= turnips_offer
Oswin: Bread's running low and the refugees are hungry. Bring me five turnips and I'll pay above market.
+ [I'll grow them.]
    ~ start_quest("quest_tasteofhome")
    Oswin: Bless you.
+ [Maybe later.]
    Oswin: The offer stands.
- -> menu

= turnips_done
Oswin: Turnips! Real ones, with dirt on them!
~ turn_in_quest("quest_tasteofhome")
Oswin: Take these seeds as well. Healroot. You'll want it out there.
-> menu
