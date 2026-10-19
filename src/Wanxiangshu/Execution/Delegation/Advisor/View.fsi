namespace Wanxiangshu.Execution.Delegation.Advisor

/// advisor-[002]: one record of the caller's own context.
///
/// `Anchor` is the caller session's own anchor number — never a session id and never a wire key —
/// because the anchor is the only identity the delivery cursor advances over.
type AdvisorViewMessage = { Anchor: int; Text: string }

/// advisor-[003]: how far this request may reach, and which anchors arrive as summaries.
///
/// `PrefixUntil` is exclusive: an anchor is eligible only when it is below it. A `ThenRemove` pair
/// replaces that anchor's text with its summary; it does not re-open an anchor already delivered.
type AdvisorViewDeclaration =
    { PrefixUntil: int
      ThenRemove: (int * string) list }

/// advisor-[002]: the caller's context, in order, plus the declaration it is read under.
type AdvisorViewRequest =
    { Messages: AdvisorViewMessage list
      Declaration: AdvisorViewDeclaration }

/// advisor-[002]/[003]: the one prompt this request hands over, and the cursor it moves.
///
/// `DeliveredAnchors` is the union of the anchors this prompt now hands over with the anchors the
/// seat already held, so the caller can store it back as the next cursor. `HasNewMaterial` is false
/// when no record was emitted — the delta was empty and the prompt carries the header alone.
type AdvisorViewPlan =
    { ViewPrompt: string
      DeliveredAnchors: int list
      HasNewMaterial: bool }

/// advisor-[002]/[003]: the caller's context view, rendered as the advisor seat's first prompt.
///
/// Pure: no Host, no journal, no clock. `language` is the provider language string the Participant
/// owner parses (PROMPT-017); it selects the authored header locale.
[<RequireQualifiedAccess>]
module AdvisorView =
    val plan: language: string -> delivered: Set<int> -> request: AdvisorViewRequest -> AdvisorViewPlan
