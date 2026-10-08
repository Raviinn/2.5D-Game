// ============================================================================
//  MASTER STORY — every conversation in the game lives under this file.
//  Each NPC gets its own file, included below. Each conversation is a knot
//  (=== name ===) that an NPC's DialogueSpeaker points to.
//
//  Writing tips:
//    Oswin: Hello there.        -> spoken line (name must match a Speaker asset)
//    The wind howls.            -> narration (no name)
//    + [Choice text]            -> a choice that can be picked again
//    * [Choice text]            -> a choice that disappears once picked
//    ~ start_quest("quest_x")   -> call into the game (see Externals.ink)
//    -> END                     -> ends the conversation
//
//  RULE — quest offers: always give the player an Accept and a Decline choice,
//  and keep a way to ask again later (e.g. a menu choice shown while the
//  quest is still "inactive"). Never start a quest without asking.
//
//  Ink reference: https://github.com/inkle/ink/blob/master/Documentation/WritingWithInk.md
// ============================================================================

INCLUDE Externals.ink
INCLUDE Oswin.ink
INCLUDE Brenna.ink
INCLUDE Maren.ink
INCLUDE Tobin.ink
INCLUDE Hale.ink

-> END
