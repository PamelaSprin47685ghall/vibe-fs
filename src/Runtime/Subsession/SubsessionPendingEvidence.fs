module Wanxiangshu.Runtime.SubsessionPendingEvidence

open Wanxiangshu.Kernel.Subsession.Types

/// In-memory buffer for evidence that arrives while a turn is active.
///
/// Evidence is NEVER cached across turns. When no epoch is active the buffer
/// discards the evidence, because a host event for a new turn cannot arrive
/// before StartRun has synchronously created the actor and begun the epoch.
/// Session-level idle observations have no turn identity and are never stored.
module SubsessionPendingEvidence =

    type private PendingSession =
        { NextEpoch: int
          ActiveEpoch: int option
          EpochEvidence: Map<int, CurrentTurnEvidence list> }

    let private capacity = 256

    let mutable private sessions: Map<string, PendingSession> = Map.empty

    let private emptySession =
        { NextEpoch = 0
          ActiveEpoch = None
          EpochEvidence = Map.empty }

    let private trim (existing: CurrentTurnEvidence list) : CurrentTurnEvidence list =
        if List.length existing < capacity then
            existing
        else
            List.skip (List.length existing - capacity) existing

    let BufferPreRun (physicalSessionId: string) (evidence: CurrentTurnEvidence) : unit =
        let existing =
            Map.tryFind physicalSessionId sessions |> Option.defaultValue emptySession

        match existing.ActiveEpoch with
        | Some epoch ->
            let current = Map.tryFind epoch existing.EpochEvidence |> Option.defaultValue []

            sessions <-
                Map.add
                    physicalSessionId
                    { existing with
                        EpochEvidence = Map.add epoch (trim (List.append current [ evidence ])) existing.EpochEvidence }
                    sessions
        | None -> ()

    let BeginRun (physicalSessionId: string) : int =
        let existing =
            Map.tryFind physicalSessionId sessions |> Option.defaultValue emptySession

        let epoch = existing.NextEpoch

        sessions <-
            Map.add
                physicalSessionId
                { NextEpoch = epoch + 1
                  ActiveEpoch = Some epoch
                  EpochEvidence = Map.add epoch [] existing.EpochEvidence }
                sessions

        epoch

    let BufferEpoch (physicalSessionId: string) (turnEpoch: int) (evidence: CurrentTurnEvidence) : unit =
        let existing =
            Map.tryFind physicalSessionId sessions |> Option.defaultValue emptySession

        let current = Map.tryFind turnEpoch existing.EpochEvidence |> Option.defaultValue []
        let nextEpoch = max existing.NextEpoch (turnEpoch + 1)

        sessions <-
            Map.add
                physicalSessionId
                { existing with
                    NextEpoch = nextEpoch
                    EpochEvidence = Map.add turnEpoch (trim (List.append current [ evidence ])) existing.EpochEvidence }
                sessions

    let TakeAllEpoch (physicalSessionId: string) (turnEpoch: int) : CurrentTurnEvidence list =
        match Map.tryFind physicalSessionId sessions with
        | Some existing ->
            let evidence =
                Map.tryFind turnEpoch existing.EpochEvidence |> Option.defaultValue []

            sessions <-
                Map.add
                    physicalSessionId
                    { existing with
                        EpochEvidence = Map.remove turnEpoch existing.EpochEvidence }
                    sessions

            evidence
        | None -> []

    let ForgetSession (physicalSessionId: string) : unit =
        sessions <- Map.remove physicalSessionId sessions

    let EndRun (physicalSessionId: string) (turnEpoch: int) : unit =
        match Map.tryFind physicalSessionId sessions with
        | Some existing when existing.ActiveEpoch = Some turnEpoch ->
            sessions <- Map.add physicalSessionId { existing with ActiveEpoch = None } sessions
        | _ -> ()
