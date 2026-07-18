module Wanxiangshu.Runtime.SubsessionPendingEvidence

open System.Collections.Generic
open Wanxiangshu.Kernel.Subsession.Types

/// In-memory buffer for evidence that arrives before an actor has an
/// active turn.
///
/// The buffer stores only evidence that can be attached to a future turn.
/// Session-level idle observations have no turn identity and are never stored.
module SubsessionPendingEvidence =

    /// Composite key: physical session id (string) + turn epoch (int).
    type Key = string

    type Pending =
        { Evidence: CurrentTurnEvidence list
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
                  TurnEpoch = turnEpoch }

    let TakeAll (key: Key) : CurrentTurnEvidence list =
        match buffer.TryGetValue key with
        | true, pending ->
            buffer.Remove key |> ignore
            pending.Evidence
        | false, _ -> []

    let TakeAllEpoch (physicalSessionId: string) (turnEpoch: int) : CurrentTurnEvidence list =
        let k = keyFor physicalSessionId turnEpoch

        match buffer.TryGetValue k with
        | true, pending ->
            buffer.Remove k |> ignore
            pending.Evidence
        | false, _ -> []

    /// Drop the entry for a (session, epoch).  Called from the
    /// SessionClosed domain command so a deleted session cannot leak.
    let Forget (physicalSessionId: string) (turnEpoch: int) : unit =
        let k = keyFor physicalSessionId turnEpoch
        buffer.Remove k |> ignore
