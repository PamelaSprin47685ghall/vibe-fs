module Wanxiangshu.Kernel.SessionControl.LeaseTransitions

/// Episode handlers over decoded SessionControlEvent. One handler per event
/// family; ordinals resolve their wire defaults against current state here.

open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.SessionControl.HumanTurn
open Wanxiangshu.Kernel.SessionControl.State
open Wanxiangshu.Kernel.SessionControl.Event
open Wanxiangshu.Kernel.SessionControl.LeaseIdentity
open Wanxiangshu.Kernel.SessionControl.LeaseIdentityOps

// ── Human turn ───────────────────────────────────────────────────────────────

let private handleHumanTurn (st: OwnerEpisodeState) (ordinal: int option) (turn: HumanTurnState) : OwnerEpisodeState =
    let newOrdinal = defaultOrdinal st.HumanTurnOrdinal ordinal
    let msgId = turn.MessageId

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
            LastHumanTurnMessageId = msgId }
        |> userAbortState

// ── Compaction ───────────────────────────────────────────────────────────────

let private handleCompactionStarted (st: OwnerEpisodeState) (ev: CompactionStartEvent) : OwnerEpisodeState =
    let newOrdinal = defaultOrdinal st.CompactionOrdinal ev.Ordinal

    if newOrdinal <= st.CompactionOrdinal then
        st
    else
        let genVal = defaultGeneration st.SessionGeneration ev.GenerationAtStart

        { st with
            Owner = Some "Compaction"
            CompactionOrdinal = newOrdinal
            CompactionStage = Requested
            CompactionGeneration = genVal
            IsCompacted = false
            Compaction =
                Some
                    { CompactionID = ev.CompactionId
                      CompactionOrdinal = newOrdinal
                      SessionGeneration = genVal
                      HumanTurnID = deriveHumanTurnId st.HumanTurn ev.HumanTurnId
                      Status = "started" } }

let private handleContextGenerationChanged (st: OwnerEpisodeState) (ev: CompactionStageEvent) : OwnerEpisodeState =
    let contextGeneration = defaultGeneration st.CompactionGeneration ev.Generation

    if guardCompactionMatch ev.CompactionId ev.Ordinal st.Compaction then
        { st with
            IsCompacted = true
            CompactionGeneration =
                if ev.CompactionId <> "" && ev.Ordinal.IsSome then
                    contextGeneration
                else
                    st.CompactionGeneration }
    else
        st

let private handleCompactionSettled (st: OwnerEpisodeState) (ev: EpisodeStageEvent) : OwnerEpisodeState =
    let eventOrdinal = defaultOrdinal st.CompactionOrdinal ev.Ordinal

    if
        eventOrdinal = st.CompactionOrdinal
        && st.CompactionStage <> Terminal
        && st.Compaction |> Option.exists (fun c -> c.CompactionID = ev.Id)
    then
        { st with
            Owner = Some "None"
            CompactionStage = Terminal
            Compaction = None
            IsCompacted = false
            CompactionGeneration = 0 }
    else
        st

// ── Continuation ─────────────────────────────────────────────────────────────

let private handleContinuationRequested (st: OwnerEpisodeState) (ev: ContinuationRequestEvent) : OwnerEpisodeState =
    let currentOrdinal = st.ContinuationOrdinal
    let newOrdinal = defaultOrdinal currentOrdinal ev.Ordinal

    if newOrdinal <= currentOrdinal then
        st
    else
        let nextLease =
            { ContinuationID = ev.ContinuationId
              ContinuationOrdinal = newOrdinal
              SessionGeneration = defaultGeneration st.SessionGeneration ev.Generation
              HumanTurnID = deriveHumanTurnId st.HumanTurn ev.HumanTurnId
              CancelGeneration = defaultCancelGeneration st.CancelGeneration ev.CancelGeneration
              Owner = ev.Owner
              Model = ev.Model
              PromptText = None
              Status = "requested" }

        { st with
            Owner = Some ev.Owner
            ContinuationOrdinal = newOrdinal
            ContinuationStage = Requested
            ContinuationLease = Some nextLease }

let private handleContinuationTerminal (st: OwnerEpisodeState) (ev: EpisodeStageEvent) : OwnerEpisodeState =
    let eventOrdinal = defaultOrdinal st.ContinuationOrdinal ev.Ordinal

    if eventOrdinal <> st.ContinuationOrdinal || st.ContinuationStage = Terminal then
        st
    elif st.ContinuationLease |> Option.exists (fun l -> l.ContinuationID = ev.Id) then
        releaseOwnerIf
            "Fallback"
            { st with
                ContinuationLease = None
                ContinuationStage = Terminal }
    else
        st

// ── Nudge ────────────────────────────────────────────────────────────────────

let private handleNudgeRequested (st: OwnerEpisodeState) (ev: NudgeRequestEvent) : OwnerEpisodeState =
    let currentOrdinal = st.NudgeOrdinal
    let newOrdinal = defaultOrdinal currentOrdinal ev.Ordinal

    if newOrdinal <= currentOrdinal then
        st
    else
        let nextLease =
            { NudgeID = ev.NudgeId
              NudgeOrdinal = newOrdinal
              Nonce = ev.Nonce
              Anchor = ev.Anchor
              HumanTurnID = deriveHumanTurnId st.HumanTurn ev.HumanTurnId
              SessionGeneration = defaultGeneration st.SessionGeneration ev.Generation
              CancelGeneration = defaultCancelGeneration st.CancelGeneration ev.CancelGeneration
              Status = "requested" }

        { st with
            Owner = Some "Nudge"
            NudgeOrdinal = newOrdinal
            NudgeStage = Requested
            NudgeLease = Some nextLease }

let private handleNudgeTerminal (st: OwnerEpisodeState) (ev: EpisodeStageEvent) : OwnerEpisodeState =
    let eventOrdinal = defaultOrdinal st.NudgeOrdinal ev.Ordinal

    if eventOrdinal <> st.NudgeOrdinal || st.NudgeStage = Terminal then
        st
    elif st.NudgeLease |> Option.exists (fun nl -> nl.NudgeID = ev.Id) then
        releaseOwnerIf
            "Nudge"
            { st with
                NudgeLease = None
                NudgeStage = Terminal }
    else
        st

// ── Terminal helpers ──────────────────────────────────────────────────────────

let private handleAssistantCompleted (st: OwnerEpisodeState) : OwnerEpisodeState =
    if st.Owner = Some "Nudge" then
        { st with
            Owner = Some "None"
            NudgeLease = None
            NudgeStage = NoEpisode }
    else
        st

let private handleNudgeDedupClearedOrWip (st: OwnerEpisodeState) : OwnerEpisodeState =
    { st with
        Owner = Some "None"
        NudgeLease = None
        NudgeStage = NoEpisode }

// ── Dispatcher ───────────────────────────────────────────────────────────────

let foldOwnerAndLeaseEvent (st: OwnerEpisodeState) (ev: SessionControlEvent) : OwnerEpisodeState =
    match ev with
    | HumanTurn(ordinal, turn) -> handleHumanTurn st ordinal turn
    | UserAbort -> userAbortState st
    | CompactionStarted ev -> handleCompactionStarted st ev
    | ContextGenerationChanged ev -> handleContextGenerationChanged st ev
    | CompactionSettled ev -> handleCompactionSettled st ev
    | ContinuationRequested ev -> handleContinuationRequested st ev
    | ContinuationDispatchStarted ev -> advanceLease ContinuationField "dispatch_started" DispatchStarted st ev
    | ContinuationDispatched ev -> advanceLease ContinuationField "dispatched" Dispatched st ev
    | ContinuationTerminal ev -> handleContinuationTerminal st ev
    | NudgeRequested ev -> handleNudgeRequested st ev
    | NudgeDispatched ev -> advanceLease NudgeField "dispatched" Dispatched st ev
    | NudgeTerminal ev -> handleNudgeTerminal st ev
    | AssistantCompleted -> handleAssistantCompleted st
    | NudgeDedupClearedOrWip -> handleNudgeDedupClearedOrWip st
