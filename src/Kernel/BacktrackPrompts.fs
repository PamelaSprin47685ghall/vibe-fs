module VibeFs.Kernel.BacktrackPrompts

let preludeText =
    "You can rewrite visible history by calling the backtrack tool. "
    + "Normal tool calls never rewrite history. "
    + "backtrack.anchor must be a currently visible id (the #id_: N prefix on tool results). "
    + "backtrack.note must be a non-empty concise note. "
    + "Ids never reuse. User messages are never removed. "
    + "When rewriting, the chosen anchor is replaced with a concise note "
    + "and later non-user visible history is discarded. "
    + "If you issue multiple backtrack calls, they are applied in message order, "
    + "and each one uses the currently visible history at that point."

let toolDescription =
    "Rewrite visible history from a specific anchor point. "
    + "The anchor must be a currently visible tool result id (the #id_: N number). "
    + "The note replaces the anchor content, and all non-user content after the anchor "
    + "is discarded from visible history. Use this when you need to correct course or "
    + "remove a wrong path of investigation."

let anchorDesc = "Currently visible id to rewrite from."
let noteDesc = "Non-empty concise note to keep at the anchor."
