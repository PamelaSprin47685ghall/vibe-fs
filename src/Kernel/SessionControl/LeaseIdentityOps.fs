module Wanxiangshu.Kernel.SessionControl.LeaseIdentityOps

open Wanxiangshu.Kernel.SessionControl.HumanTurn
open Wanxiangshu.Kernel.SessionControl.State
open Wanxiangshu.Kernel.SessionControl.Event
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.SessionControl.LeaseIdentity

let advanceLease
    (field: LeaseField)
    (status: string)
    (nextStage: EpisodeStage)
    (st: OwnerEpisodeState)
    (ev: EpisodeStageEvent)
    : OwnerEpisodeState =
    let eventOrdinal = defaultOrdinal (ordinalOf field st) ev.Ordinal
    let currentOrdinal = ordinalOf field st
    let currentStage = stageOf field st

    let guardResult =
        match field with
        | ContinuationField -> guardContinuationStage eventOrdinal nextStage currentOrdinal currentStage
        | NudgeField -> guardNudgeStage eventOrdinal nextStage currentOrdinal currentStage

    match guardResult with
    | Ok ->
        match leaseIdOf field st with
        | Some l when idOfLeaseUnion l = ev.Id ->
            st |> applyLeaseInState field (setLeaseStatus field status l) nextStage
        | _ -> st
    | _ -> st

let requestLease
    (field: LeaseField)
    (owner: string)
    (st: OwnerEpisodeState)
    (evOrdinal: int option)
    (evHumanTurnId: string option)
    (evSessionGen: int option)
    (evCancelGen: int option)
    : OwnerEpisodeState =
    let currentOrdinal = ordinalOf field st
    let newOrdinal = defaultOrdinal currentOrdinal evOrdinal

    if newOrdinal <= currentOrdinal then
        st
    else
        let nextLease =
            match field with
            | ContinuationField ->
                Continuation
                    { ContinuationID = ""
                      ContinuationOrdinal = newOrdinal
                      SessionGeneration = defaultGeneration st.SessionGeneration evSessionGen
                      HumanTurnID = deriveHumanTurnId st.HumanTurn evHumanTurnId
                      HostUserMessageId = ""
                      CancelGeneration = defaultCancelGeneration st.CancelGeneration evCancelGen
                      Owner = owner
                      Model = ""
                      PromptText = None
                      Status = "requested" }
            | NudgeField ->
                Nudge
                    { NudgeID = ""
                      NudgeOrdinal = newOrdinal
                      Nonce = ""
                      Anchor = ""
                      HumanTurnID = deriveHumanTurnId st.HumanTurn evHumanTurnId
                      HostUserMessageId = ""
                      SessionGeneration = defaultGeneration st.SessionGeneration evSessionGen
                      CancelGeneration = defaultCancelGeneration st.CancelGeneration evCancelGen
                      Status = "requested" }

        st
        |> applyLeaseInState field nextLease Requested
        |> (fun s -> { s with Owner = Some owner })

let releaseOwnerIf (agent: string) (st: OwnerEpisodeState) : OwnerEpisodeState =
    { st with
        Owner = if st.Owner = Some agent then Some "None" else st.Owner }

let userAbortState (st: OwnerEpisodeState) : OwnerEpisodeState =
    { st with
        Owner = Some "None"
        ContinuationLease = None
        ContinuationStage = NoEpisode
        NudgeLease = None
        NudgeStage = NoEpisode
        Compaction = None
        CompactionStage = NoEpisode
        IsCompacted = false
        CompactionGeneration = 0 }

let nudgeDedupState (st: OwnerEpisodeState) : OwnerEpisodeState =
    { st with
        Owner = Some "None"
        NudgeLease = None
        NudgeStage = NoEpisode }

let continuationTerminal (evId: string) (st: OwnerEpisodeState) : OwnerEpisodeState =
    let hasId = st.ContinuationLease |> Option.exists (fun l -> l.ContinuationID = evId)

    { st with
        ContinuationLease = None
        ContinuationStage = Terminal
        Owner =
            if hasId then
                releaseOwnerIf "Fallback" st |> (fun s -> s.Owner)
            else
                st.Owner }

let nudgeTerminal (evId: string) (st: OwnerEpisodeState) : OwnerEpisodeState =
    let hasId = st.NudgeLease |> Option.exists (fun nl -> nl.NudgeID = evId)

    { st with
        NudgeLease = None
        NudgeStage = Terminal
        Owner =
            if hasId then
                releaseOwnerIf "Nudge" st |> (fun s -> s.Owner)
            else
                st.Owner }
