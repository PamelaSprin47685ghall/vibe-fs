namespace Wanxiangshu.Execution.Delegation.Advisor

open System.Collections.Generic

/// Process-live delivered-anchor cursor for `js.advisor`: the anchors one
/// caller session's advisor seat has already been handed, so a follow-up
/// consult delivers only the view delta behind the new intent. Module state by
/// construction — a restart forgets every cursor, matching the process-live
/// delegation seam and the anchor ledger.
module Cursor =

    // DSL-MUTABLE: resource — callerSessionId → delivered anchor numbers
    let private cursors = Dictionary<string, HashSet<string>>()

    let private cursorOf (callerSessionId: string) : HashSet<string> =
        match cursors.TryGetValue callerSessionId with
        | true, existing -> existing
        | false, _ ->
            let fresh = HashSet<string>()
            cursors.[callerSessionId] <- fresh
            fresh

    /// Every anchor number already delivered to this caller session's advisor.
    let delivered (callerSessionId: string) : Set<string> =
        match cursors.TryGetValue callerSessionId with
        | true, existing -> existing |> Set.ofSeq
        | false, _ -> Set.empty

    /// Record the anchors one consult delivered. The cursor only grows: an
    /// anchor already delivered stays delivered.
    let record (callerSessionId: string) (anchors: string list) : unit =
        if not (List.isEmpty anchors) then
            let cursor = cursorOf callerSessionId

            anchors |> List.iter (fun anchor -> cursor.Add anchor |> ignore)

    let clear (callerSessionId: string) : unit =
        cursors.Remove callerSessionId |> ignore

    let clearAll () : unit = cursors.Clear()
