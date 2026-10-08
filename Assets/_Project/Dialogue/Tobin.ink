// ============================================================================
//  TOBIN: farmer whose fields died in the Blight. Young, restless, honest.
//  Maren's nephew. Gives: Wolves at the Fold (side). Shares farming tips.
// ============================================================================

=== tobin ===
{ quest_state("quest_wolvesatthefold") == "ready": -> wolves_done }
{
- tobin == 1:
    Tobin: You're the one with the field that actually grows? I've been watching it over the fence. Sorry. It's just...
    Tobin: Mine went grey two summers back. I'm Tobin. I help where I can, which is mostly carrying things.
- weather() == "Rain":
    Tobin: {~Rain! Your field's drinking well today, {player_name()}.|Don't mind me, I just like standing in it.}
- quest_state("quest_wolvesatthefold") == "done":
    Tobin: {~{player_name()}! Haven't heard a howl since you went up there.|The goats are gone, but at least nobody else will be.}
- else:
    Tobin: {~Morning, {player_name()}! Or whatever it is.|Your turnips look better than anything I ever grew.|Heard the wolves again last night.}
}
-> menu

= menu
+ [Any advice for a farmer?]
    -> tips
+ {quest_state("quest_wolvesatthefold") == "inactive"} [You look worried.]
    -> wolves_offer
+ {quest_state("quest_wolvesatthefold") == "active"} [About the wolves...]
    Tobin: The pack dens in the north-west woods, past the houses. Three of them. They come at you from behind, so keep turning.
    -> menu
+ [Goodbye.]
    Tobin: Mind the fence, it's the only one we've got.
    -> END

= tips
{ season() == "Winter":
    Tobin: Only frost kale takes the cold. Everything else dies at the first frost, so harvest before winter comes.
    -> menu
}
{ season() == "Autumn":
    Tobin: Pumpkins are an autumn crop. Plant them early in the season: they take their time.
    -> menu
}
Tobin: {~Rain does your watering for you. Don't waste a morning carrying buckets when the sky's doing it.|Healroot keeps giving after you pick it. Turnips don't. Plant accordingly.|Two days dry and a crop wilts. Three and it's gone. Ask me how I know.|Sleep in your own bed and the field grows while you do. Best trade in the Hollows.}
-> menu

= wolves_offer
Tobin: Wolves. A pack moved into the north-west woods when the Blight drove the deer out.
Tobin: They took the last of Maren's goats. Next it'll be someone walking home late.
+ [I'll deal with them.]
    ~ start_quest("quest_wolvesatthefold")
    Tobin: Three of them. Watch your back. I mean that literally.
+ [Not now.]
    Tobin: No, that's fair. They're not your goats.
- -> menu

= wolves_done
Tobin: All three? Maren will cry. Happily, I mean.
~ turn_in_quest("quest_wolvesatthefold")
Tobin: Take these seeds. I was saving them for a field I don't have any more.
-> menu
