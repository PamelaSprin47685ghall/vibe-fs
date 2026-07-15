module Wanxiangshu.Kernel.EventLog.ContinuationProjection

/// Independent projection for continuation/compaction/nudge episode state.
///
/// Owner: Fallback / Nudge / Compaction subsystems
/// Input events: continuation_*, nudge_*, compaction_*, human_turn_started,
///               user_abort_observed, context_generation_changed
/// Query: SessionOwner, PendingLease, EpisodeStage
///
/// Phase 6: Split from SessionState.

open Wanxiangshu.Kernel.EventLog.Types
open Wanxiangshu.Kernel.EventLog.HumanTurnProjection
open Wanxiangshu.Kernel.FallbackKernel.Types

let private payloadField (key: string) (e: WanEvent) : string option = e.Payload |> Map.tryFind key

let private parseIntOpt (raw: string) : int option =
    if raw = "" then
        None
    else
        try
            Some(int raw)
        with _ ->
            None

// ── Lease state types ──

type ReplayLeaseState =
    { ContinuationID: string
      ContinuationOrdinal: int
      SessionGeneration: int
      HumanTurnID: string
      CancelGeneration: int
      Owner: string
      Model: string
      PromptText: string option
      Status: string }

type ReplayNudgeLeaseState =
    { NudgeID: string
      NudgeOrdinal: int
      Nonce: string
      Anchor: string
      HumanTurnID: string
      SessionGeneration: int
      CancelGeneration: int
      Status: string }

type ReplayCompactionState =
    { CompactionID: string
      CompactionOrdinal: int
      SessionGeneration: int
      HumanTurnID: string
      Status: string }

// ── Episode state ──

type OwnerEpisodeState =
    { Owner: string option
      ContinuationLease: ReplayLeaseState option
      ContinuationOrdinal: int
      ContinuationStage: EpisodeStage
      NudgeLease: ReplayNudgeLeaseState option
      NudgeOrdinal: int
      NudgeStage: EpisodeStage
      Compaction: ReplayCompactionState option
      CompactionOrdinal: int
      CompactionStage: EpisodeStage
      IsCompacted: bool
      CompactionGeneration: int
      SessionGeneration: int
      CancelGeneration: int
      HumanTurn: HumanTurnState option
      HumanTurnOrdinal: int
      LastHumanTurnMessageId: string option }



let emptyEpisodeState: OwnerEpisodeState =
    { Owner = None
      ContinuationLease = None
      ContinuationOrdinal = 0
      ContinuationStage = NoEpisode
      NudgeLease = None
      NudgeOrdinal = 0
      NudgeStage = NoEpisode
      Compaction = None
      CompactionOrdinal = 0
      CompactionStage = NoEpisode
      IsCompacted = false
      CompactionGeneration = 0
      SessionGeneration = 0
      CancelGeneration = 0
      HumanTurn = None
      HumanTurnOrdinal = 0
      LastHumanTurnMessageId = None }

// ── Helpers ──

let private humanTurnFromEvent (e: WanEvent) : HumanTurnState option = HumanTurnProjection.foldSingleEvent e

let private humanTurnOrdinal (currentOrdinal: int) (e: WanEvent) : int =
    e.Payload
    |> Map.tryFind "humanTurnOrdinal"
    |> Option.bind parseIntOpt
    |> Option.defaultValue (currentOrdinal + 1)

let private continuationStartOrdinal (currentOrdinal: int) (e: WanEvent) : int =
    e.Payload
    |> Map.tryFind "continuationOrdinal"
    |> Option.bind parseIntOpt
    |> Option.defaultValue (currentOrdinal + 1)

let private continuationStageOrdinal (currentOrdinal: int) (e: WanEvent) : int =
    e.Payload
    |> Map.tryFind "continuationOrdinal"
    |> Option.bind parseIntOpt
    |> Option.defaultValue currentOrdinal

let private nudgeStartOrdinal (currentOrdinal: int) (e: WanEvent) : int =
    e.Payload
    |> Map.tryFind "nudgeOrdinal"
    |> Option.bind parseIntOpt
    |> Option.defaultValue (currentOrdinal + 1)

let private nudgeStageOrdinal (currentOrdinal: int) (e: WanEvent) : int =
    e.Payload
    |> Map.tryFind "nudgeOrdinal"
    |> Option.bind parseIntOpt
    |> Option.defaultValue currentOrdinal

let private compactionStartOrdinal (currentOrdinal: int) (e: WanEvent) : int =
    e.Payload
    |> Map.tryFind "compactionOrdinal"
    |> Option.bind parseIntOpt
    |> Option.defaultValue (currentOrdinal + 1)

let private compactionStageOrdinal (currentOrdinal: int) (e: WanEvent) : int =
    e.Payload
    |> Map.tryFind "compactionOrdinal"
    |> Option.bind parseIntOpt
    |> Option.defaultValue currentOrdinal

let private clearEpisodeState (st: OwnerEpisodeState) : OwnerEpisodeState =
    { st with
        Owner = Some "Human"
        ContinuationLease = None
        ContinuationStage = NoEpisode
        NudgeLease = None
        NudgeStage = NoEpisode
        Compaction = None
        CompactionStage = NoEpisode
        IsCompacted = false
        CompactionGeneration = 0 }

// ── Ownership + generation fold ──

let foldGeneration
    (sessionGen: int, cancelGen: int, activeContGen: int, activeCancelGen: int, latestHumanTurn: HumanTurnState option)
    (e: WanEvent)
    : (int * int * int * int) =
    match e.Kind with
    | k when k = eventKindHumanTurnStarted ->
        let turnId = payloadField "turnId" e |> Option.defaultValue ""

        if turnId <> "" && latestHumanTurn |> Option.exists (fun t -> t.TurnId = turnId) then
            sessionGen, cancelGen, activeContGen, activeCancelGen
        else
            let nextCancelGen = cancelGen + 1
            sessionGen, nextCancelGen, sessionGen, nextCancelGen
    | k when k = eventKindUserAbortObserved -> sessionGen, cancelGen + 1, activeContGen, activeCancelGen
    | k when k = eventKindContinuationRequested ->
        let reqGen =
            e.Payload
            |> Map.tryFind "generation"
            |> Option.bind parseIntOpt
            |> Option.defaultValue sessionGen

        let reqCancel =
            e.Payload
            |> Map.tryFind "cancelGeneration"
            |> Option.bind parseIntOpt
            |> Option.defaultValue cancelGen

        sessionGen, cancelGen, reqGen, reqCancel
    | k when k = eventKindContextGenerationChanged ->
        let newGen =
            e.Payload
            |> Map.tryFind "generation"
            |> Option.bind parseIntOpt
            |> Option.defaultValue sessionGen

        let eventCompactionId =
            e.Payload |> Map.tryFind "compactionId" |> Option.defaultValue ""

        let eventCompactionOrdinal =
            e.Payload |> Map.tryFind "compactionOrdinal" |> Option.bind parseIntOpt

        if eventCompactionId <> "" && eventCompactionOrdinal.IsSome then
            sessionGen, cancelGen, activeContGen, activeCancelGen
        else
            newGen, cancelGen, activeContGen, activeCancelGen
    | _ -> sessionGen, cancelGen, activeContGen, activeCancelGen

// ── Main owner/lease folder ──

let foldOwnerAndLease (st: OwnerEpisodeState) (e: WanEvent) : OwnerEpisodeState =
    match e.Kind with
    | k when k = eventKindHumanTurnStarted ->
        let newOrdinal = humanTurnOrdinal st.HumanTurnOrdinal e
        let msgId = humanTurnFromEvent e |> Option.bind (fun h -> h.MessageId)
        let turnId = payloadField "turnId" e |> Option.defaultValue ""

        if
            newOrdinal <= st.HumanTurnOrdinal
            || (msgId.IsSome
                && st.LastHumanTurnMessageId.IsSome
                && msgId.Value = st.LastHumanTurnMessageId.Value)
        then
            st
        else
            { st with
                HumanTurnOrdinal = newOrdinal
                LastHumanTurnMessageId = msgId
                CancelGeneration = st.CancelGeneration + 1 }
            |> clearEpisodeState

    | k when k = eventKindUserAbortObserved ->
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

    | k when k = eventKindCompactionStarted ->
        let newOrdinal = compactionStartOrdinal st.CompactionOrdinal e
        let cid = payloadField "compactionId" e |> Option.defaultValue ""

        if newOrdinal <= st.CompactionOrdinal then
            st
        else
            let genVal =
                e.Payload
                |> Map.tryFind "generationAtStart"
                |> Option.orElse (e.Payload |> Map.tryFind "generation")
                |> Option.bind parseIntOpt
                |> Option.defaultValue st.SessionGeneration

            let humanTurnId =
                payloadField "humanTurnId" e
                |> Option.orElse (st.HumanTurn |> Option.map (fun t -> t.TurnId))
                |> Option.defaultValue ""

            { st with
                Owner = Some "Compaction"
                CompactionOrdinal = newOrdinal
                CompactionStage = Requested
                Compaction =
                    Some
                        { CompactionID = cid
                          CompactionOrdinal = newOrdinal
                          SessionGeneration = genVal
                          HumanTurnID = humanTurnId
                          Status = "started" }
                CompactionGeneration = genVal
                IsCompacted = false }

    | k when k = eventKindContextGenerationChanged ->
        let eventCompactionId =
            e.Payload |> Map.tryFind "compactionId" |> Option.defaultValue ""

        let eventCompactionOrdinal =
            e.Payload |> Map.tryFind "compactionOrdinal" |> Option.bind parseIntOpt

        let contextGeneration =
            e.Payload
            |> Map.tryFind "generation"
            |> Option.bind parseIntOpt
            |> Option.defaultValue st.CompactionGeneration

        let isMatch =
            eventCompactionId = ""
            || eventCompactionOrdinal.IsNone
            || (st.Compaction
                |> Option.exists (fun c ->
                    c.CompactionID = eventCompactionId
                    && c.CompactionOrdinal = eventCompactionOrdinal.Value))

        if isMatch then
            { st with
                IsCompacted = true
                CompactionGeneration =
                    if eventCompactionId <> "" && eventCompactionOrdinal.IsSome then
                        contextGeneration
                    else
                        st.CompactionGeneration }
        else
            st

    | k when k = eventKindCompactionSettled ->
        let eventOrdinal = compactionStageOrdinal st.CompactionOrdinal e
        let cid = payloadField "compactionId" e |> Option.defaultValue ""

        if
            eventOrdinal = st.CompactionOrdinal
            && st.CompactionStage <> Terminal
            && st.Compaction |> Option.exists (fun c -> c.CompactionID = cid)
        then
            { st with
                Owner =
                    (if st.Owner = Some "Compaction" then
                         Some "None"
                     else
                         st.Owner)
                CompactionStage = Terminal
                Compaction = None
                IsCompacted = false
                CompactionGeneration = 0 }
        else
            st

    | k when k = eventKindContinuationRequested ->
        let newOrdinal = continuationStartOrdinal st.ContinuationOrdinal e
        let contId = payloadField "continuationId" e |> Option.defaultValue ""

        if newOrdinal <= st.ContinuationOrdinal then
            st
        else
            let model = payloadField "model" e |> Option.defaultValue ""

            let gen =
                e.Payload
                |> Map.tryFind "generation"
                |> Option.bind parseIntOpt
                |> Option.defaultValue st.SessionGeneration

            let cancelGen =
                e.Payload
                |> Map.tryFind "cancelGeneration"
                |> Option.bind parseIntOpt
                |> Option.defaultValue st.CancelGeneration

            let humanTurnId =
                payloadField "humanTurnId" e
                |> Option.orElse (st.HumanTurn |> Option.map (fun t -> t.TurnId))
                |> Option.defaultValue ""

            let ownerVal = payloadField "owner" e |> Option.defaultValue "Fallback"

            let nextLease =
                { ContinuationID = contId
                  ContinuationOrdinal = newOrdinal
                  SessionGeneration = gen
                  HumanTurnID = humanTurnId
                  CancelGeneration = cancelGen
                  Owner = ownerVal
                  Model = model
                  PromptText = None
                  Status = "requested" }

            { st with
                Owner = Some ownerVal
                ContinuationOrdinal = newOrdinal
                ContinuationStage = Requested
                ContinuationLease = Some nextLease }

    | k when k = eventKindContinuationDispatchStarted ->
        let eventOrdinal = continuationStageOrdinal st.ContinuationOrdinal e
        let cid = payloadField "continuationId" e |> Option.defaultValue ""

        if eventOrdinal <> st.ContinuationOrdinal || st.ContinuationStage <> Requested then
            st
        else
            match
                st.ContinuationLease
                |> Option.bind (fun l ->
                    if l.ContinuationID = cid then
                        Some { l with Status = "dispatch_started" }
                    else
                        None)
            with
            | Some l ->
                { st with
                    ContinuationLease = Some l
                    ContinuationStage = DispatchStarted }
            | None -> st

    | k when k = eventKindContinuationDispatched ->
        let eventOrdinal = continuationStageOrdinal st.ContinuationOrdinal e
        let cid = payloadField "continuationId" e |> Option.defaultValue ""

        if
            eventOrdinal <> st.ContinuationOrdinal
            || st.ContinuationStage <> DispatchStarted
        then
            st
        else
            match
                st.ContinuationLease
                |> Option.bind (fun l ->
                    if l.ContinuationID = cid then
                        Some { l with Status = "dispatched" }
                    else
                        None)
            with
            | Some l ->
                { st with
                    ContinuationLease = Some l
                    ContinuationStage = Dispatched }
            | None -> st

    | k when
        k = eventKindContinuationFailed
        || k = eventKindContinuationCancelled
        || k = eventKindContinuationSettled
        ->
        let eventOrdinal = continuationStageOrdinal st.ContinuationOrdinal e
        let cid = payloadField "continuationId" e |> Option.defaultValue ""

        if eventOrdinal <> st.ContinuationOrdinal || st.ContinuationStage = Terminal then
            st
        elif st.ContinuationLease |> Option.exists (fun l -> l.ContinuationID = cid) then
            let nextOwner = if st.Owner = Some "Fallback" then Some "None" else st.Owner

            { st with
                Owner = nextOwner
                ContinuationLease = None
                ContinuationStage = Terminal }
        else
            st

    | k when k = eventKindNudgeRequested ->
        let newOrdinal = nudgeStartOrdinal st.NudgeOrdinal e
        let nid = payloadField "nudgeId" e |> Option.defaultValue ""

        if newOrdinal <= st.NudgeOrdinal then
            st
        else
            let nonce = payloadField "nonce" e |> Option.defaultValue ""
            let anchor = payloadField "anchor" e |> Option.defaultValue ""

            let humanTurnId =
                payloadField "humanTurnId" e
                |> Option.orElse (st.HumanTurn |> Option.map (fun t -> t.TurnId))
                |> Option.defaultValue ""

            let gen =
                e.Payload
                |> Map.tryFind "generation"
                |> Option.bind parseIntOpt
                |> Option.defaultValue st.SessionGeneration

            let cancelGen =
                e.Payload
                |> Map.tryFind "cancelGeneration"
                |> Option.bind parseIntOpt
                |> Option.defaultValue st.CancelGeneration

            let nextLease =
                { NudgeID = nid
                  NudgeOrdinal = newOrdinal
                  Nonce = nonce
                  Anchor = anchor
                  HumanTurnID = humanTurnId
                  SessionGeneration = gen
                  CancelGeneration = cancelGen
                  Status = "requested" }

            { st with
                Owner = Some "Nudge"
                NudgeOrdinal = newOrdinal
                NudgeStage = Requested
                NudgeLease = Some nextLease }

    | k when k = eventKindNudgeDispatched ->
        let eventOrdinal = nudgeStageOrdinal st.NudgeOrdinal e
        let nid = payloadField "nudgeId" e |> Option.defaultValue ""

        if eventOrdinal <> st.NudgeOrdinal || st.NudgeStage <> Requested then
            st
        else
            match
                st.NudgeLease
                |> Option.bind (fun nl ->
                    if nl.NudgeID = nid then
                        Some { nl with Status = "dispatched" }
                    else
                        None)
            with
            | Some l ->
                { st with
                    NudgeLease = Some l
                    NudgeStage = Dispatched }
            | None -> st

    | k when
        k = eventKindNudgeFailed
        || k = eventKindNudgeCancelled
        || k = eventKindNudgeSettled
        ->
        let eventOrdinal = nudgeStageOrdinal st.NudgeOrdinal e
        let nid = payloadField "nudgeId" e |> Option.defaultValue ""

        if eventOrdinal <> st.NudgeOrdinal || st.NudgeStage = Terminal then
            st
        elif st.NudgeLease |> Option.exists (fun nl -> nl.NudgeID = nid) then
            { st with
                Owner = (if st.Owner = Some "Nudge" then Some "None" else st.Owner)
                NudgeLease = None
                NudgeStage = Terminal }
        else
            st

    | k when k = eventKindAssistantCompleted ->
        let nextOwner =
            match st.Owner with
            | Some "Nudge" -> Some "None"
            | _ -> st.Owner

        { st with Owner = nextOwner }

    | k when k = eventKindNudgeDedupCleared || k = eventKindSubmitReviewWipRecorded ->
        { st with
            Owner = Some "None"
            NudgeLease = None
            NudgeStage = NoEpisode }

    | _ -> st

/// Fold a full event stream into OwnerEpisodeState.
let foldOwnerAndLeaseStream (sessionId: string) (events: WanEvent list) : OwnerEpisodeState =
    events
    |> List.filter (fun e -> e.Session = sessionId)
    |> List.fold foldOwnerAndLease emptyEpisodeState

/// Check if an event is an episode event (late-event eligible).
let isEpisodeEvent (e: WanEvent) : bool =
    e.Kind = eventKindContinuationRequested
    || e.Kind = eventKindContinuationDispatchStarted
    || e.Kind = eventKindContinuationDispatched
    || e.Kind = eventKindContinuationFailed
    || e.Kind = eventKindContinuationCancelled
    || e.Kind = eventKindContinuationSettled
    || e.Kind = eventKindNudgeRequested
    || e.Kind = eventKindNudgeDispatched
    || e.Kind = eventKindNudgeFailed
    || e.Kind = eventKindNudgeCancelled
    || e.Kind = eventKindNudgeSettled
    || e.Kind = eventKindCompactionStarted
    || e.Kind = eventKindCompactionSettled
    || e.Kind = eventKindHumanTurnStarted
