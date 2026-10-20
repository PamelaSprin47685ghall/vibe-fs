namespace Wanxiangshu.Execution.Delegation.Advisor

open System.Collections.Generic

/// Process-live record of material successfully delivered to each advisor seat.
/// Repeated representatives can carry new summaries, so identity includes the
/// representative and the exact rendered material.
module Cursor =

    // DSL-MUTABLE: resource — callerSessionId → delivered material identities
    let private cursors = Dictionary<string, HashSet<string>>()

    let private cursorOf (callerSessionId: string) : HashSet<string> =
        match cursors.TryGetValue callerSessionId with
        | true, existing -> existing
        | false, _ ->
            let fresh = HashSet<string>()
            cursors.[callerSessionId] <- fresh
            fresh

    /// Material identities already delivered to this caller session's advisor.
    let delivered (callerSessionId: string) : Set<string> =
        match cursors.TryGetValue callerSessionId with
        | true, existing -> existing |> Set.ofSeq
        | false, _ -> Set.empty

    /// Record only material confirmed as delivered. The record is monotone.
    let record (callerSessionId: string) (anchors: string list) : unit =
        if not (List.isEmpty anchors) then
            let cursor = cursorOf callerSessionId

            anchors |> List.iter (fun anchor -> cursor.Add anchor |> ignore)

    let clear (callerSessionId: string) : unit =
        cursors.Remove callerSessionId |> ignore

    let clearAll () : unit = cursors.Clear()
