namespace Wanxiangshu.Execution.Delegation.Advisor

/// advisor-[002]/[003]: JS-native owner boundary for the advisor context view and the
/// process-live delivered-anchor cursor.
///
/// A request crosses as a plain JS object — `{ messages: [{ anchor, text }], declaration:
/// { prefixUntil, thenRemove: [[anchor, summary]] } }` — and a plan comes back as
/// `{ viewPrompt, deliveredAnchors, hasNewMaterial }`. `AdvisorView`'s records and the
/// cursor's F# collections stay on this side of the boundary.
[<RequireQualifiedAccess>]
module Surface =

    /// advisor-[002]/[003]: render the view delta of one caller session as the advisor seat's
    /// next prompt; `delivered` carries the anchors that session's seat already holds.
    val plan: language: string -> delivered: int array -> request: obj -> obj

    /// Every anchor number already delivered to this caller session's advisor, ascending.
    val delivered: callerSessionId: string -> int array

    /// Record the anchors one consult delivered; the cursor only grows.
    val record: callerSessionId: string -> anchors: int array -> unit

    /// advisor-[003]: forget one caller session's cursor, or every cursor.
    val clear: callerSessionId: string -> unit
    val clearAll: unit -> unit
