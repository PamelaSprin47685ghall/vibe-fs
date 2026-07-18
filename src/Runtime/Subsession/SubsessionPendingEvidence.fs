module Wanxiangshu.Runtime.SubsessionPendingEvidence

open System.Collections.Generic
open Wanxiangshu.Kernel.Subsession.Types

/// In-memory buffer for evidence that arrives before an actor has an
/// active turn.
///
/// S-06 fix: legacy callers used a single `Key = string` that mapped
/// 1:1 to a physical session id, which leaked a late `session.idle`
/// from the previous turn into the new turn.  The buffer now exposes
/// epoch-aware overloads.  Old callers continue to work (epoch 0 by
/// default); the new ones pass the turn epoch explicitly.
module SubsessionPendingEvidence =

    /// Composite key: physical session id (string) + turn epoch (int).
    type Key = string

    type Pending =
        { Evidence: CurrentTurnEvidence list
          IdleSeen: bool
          mutable TurnEpoch: int }

    /// Default epoch used by the legacy single-argument API.  Actors
    /// that have not been migrated yet still drain under epoch 0.
    let private defaultEpoch = 0

    let private capacity = 256

    let mutable private buffer = Dictionary<Key, Pending>()

    let private keyFor (physicalSessionId: string) (turnEpoch: int) : Key =
        physicalSessionId + "|" + string turnEpoch

    let private trim (existing: CurrentTurnEvidence list) : CurrentTurnEvidence list =
        if List.length existing < capacity then
            existing
        else
            List.skip (List.length existing - capacity) existing

    /// Legacy single-argument append.  Uses epoch 0.
    let Buffer (key: Key) (evidence: CurrentTurnEvidence) : unit =
        match buffer.TryGetValue key with
        | true, existing ->
            let next = trim (List.append existing.Evidence [ evidence ])

            buffer.[key] <-
                { existing with
                    Evidence = next
                    TurnEpoch = defaultEpoch }
        | false, _ ->
            buffer.[key] <-
                { Evidence = trim [ evidence ]
                  IdleSeen = false
                  TurnEpoch = defaultEpoch }

    /// Epoch-aware append.  Callers that drive their own actor epoch
    /// (e.g. SubsessionService on BeginRun) MUST use this overload so
    /// the buffer key matches the new turn's epoch.
    let BufferEpoch (physicalSessionId: string) (turnEpoch: int) (evidence: CurrentTurnEvidence) : unit =
        let k = keyFor physicalSessionId turnEpoch

        match buffer.TryGetValue k with
        | true, existing ->
            let next = trim (List.append existing.Evidence [ evidence ])

            buffer.[k] <- { existing with Evidence = next }
        | false, _ ->
            buffer.[k] <-
                { Evidence = trim [ evidence ]
                  IdleSeen = false
                  TurnEpoch = turnEpoch }

    let MarkIdle (key: Key) : unit =
        match buffer.TryGetValue key with
        | true, existing -> buffer.[key] <- { existing with IdleSeen = true }
        | false, _ ->
            buffer.[key] <-
                { Evidence = []
                  IdleSeen = true
                  TurnEpoch = defaultEpoch }

    let MarkIdleEpoch (physicalSessionId: string) (turnEpoch: int) : unit =
        let k = keyFor physicalSessionId turnEpoch

        match buffer.TryGetValue k with
        | true, existing -> buffer.[k] <- { existing with IdleSeen = true }
        | false, _ ->
            buffer.[k] <-
                { Evidence = []
                  IdleSeen = true
                  TurnEpoch = turnEpoch }

    let TakeAll (key: Key) : CurrentTurnEvidence list * bool =
        match buffer.TryGetValue key with
        | true, pending ->
            buffer.Remove key |> ignore
            pending.Evidence, pending.IdleSeen
        | false, _ -> [], false

    let TakeAllEpoch (physicalSessionId: string) (turnEpoch: int) : CurrentTurnEvidence list * bool =
        let k = keyFor physicalSessionId turnEpoch

        match buffer.TryGetValue k with
        | true, pending ->
            buffer.Remove k |> ignore
            pending.Evidence, pending.IdleSeen
        | false, _ -> [], false

    /// Drop the entry for a (session, epoch).  Called from the
    /// SessionClosed domain command so a deleted session cannot leak.
    let Forget (physicalSessionId: string) (turnEpoch: int) : unit =
        let k = keyFor physicalSessionId turnEpoch
        buffer.Remove k |> ignore
