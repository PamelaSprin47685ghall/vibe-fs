module Wanxiangshu.Runtime.SubsessionPendingEvidence

open System.Collections.Generic
open Wanxiangshu.Kernel.Subsession.Types

/// Simple in-memory buffer for evidence that arrives before an actor has an
/// active turn.  Keyed by session id (string).
module SubsessionPendingEvidence =
    type Key = string

    type Pending =
        { Evidence: CurrentTurnEvidence list
          IdleSeen: bool }

    let mutable private buffer = Dictionary<Key, Pending>()

    /// Append one piece of evidence to the per-key buffer.
    let Buffer (key: Key) (evidence: CurrentTurnEvidence) : unit =
        match buffer.TryGetValue key with
        | true, existing ->
            buffer.[key] <-
                { existing with
                    Evidence = List.append existing.Evidence [ evidence ] }
        | false, _ ->
            buffer.[key] <-
                { Evidence = [ evidence ]
                  IdleSeen = false }

    /// Record that an idle was seen before the actor had an active turn.
    let MarkIdle (key: Key) : unit =
        match buffer.TryGetValue key with
        | true, existing -> buffer.[key] <- { existing with IdleSeen = true }
        | false, _ -> buffer.[key] <- { Evidence = []; IdleSeen = true }

    /// Take and remove ALL buffered state for the given key.
    /// Returns the list of evidence (in insertion order) and whether idle was seen.
    let TakeAll (key: Key) : CurrentTurnEvidence list * bool =
        match buffer.TryGetValue key with
        | true, pending ->
            buffer.Remove key |> ignore
            pending.Evidence, pending.IdleSeen
        | false, _ -> [], false
