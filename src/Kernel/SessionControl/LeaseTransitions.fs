module Wanxiangshu.Kernel.SessionControl.LeaseTransitions

/// Episode handlers over decoded SessionControlEvent. One handler per event
/// family; ordinals resolve their wire defaults against current state here.

open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.SessionControl.HumanTurn
open Wanxiangshu.Kernel.SessionControl.State
open Wanxiangshu.Kernel.SessionControl.Event

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

let private humanTurnIdOr (fallback: HumanTurnState option) (explicitId: string option) : string =
    explicitId
    |> Option.orElse (fallback |> Option.map (fun t -> t.TurnId))
    |> Option.defaultValue ""

let private handleHumanTurn (st: OwnerEpisodeState) (ordinal: int option) (turn: HumanTurnState) : OwnerEpisodeState =
    let newOrdinal = ordinal |> Option.defaultValue (st.HumanTurnOrdinal + 1)
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
            LastHumanTurnMessageId = msgId
            CancelGeneration = st.CancelGeneration + 1 }
        |> clearEpisodeState

let private handleUserAbort (st: OwnerEpisodeState) : OwnerEpisodeState =
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

let private handleCompactionStarted (st: OwnerEpisodeState) (ev: CompactionStartEvent) : OwnerEpisodeState =
    let newOrdinal = ev.Ordinal |> Option.defaultValue (st.CompactionOrdinal + 1)

    if newOrdinal <= st.CompactionOrdinal then
        st
    else
        let genVal = ev.GenerationAtStart |> Option.defaultValue st.SessionGeneration

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
                      HumanTurnID = humanTurnIdOr st.HumanTurn ev.HumanTurnId
                      Status = "started" } }

let private handleContextGenerationChanged (st: OwnerEpisodeState) (ev: CompactionStageEvent) : OwnerEpisodeState =
    let contextGeneration = ev.Generation |> Option.defaultValue st.CompactionGeneration

    let isMatch =
        ev.CompactionId = ""
        || ev.Ordinal.IsNone
        || (st.Compaction
            |> Option.exists (fun c -> c.CompactionID = ev.CompactionId && c.CompactionOrdinal = ev.Ordinal.Value))

    if isMatch then
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
    let eventOrdinal = ev.Ordinal |> Option.defaultValue st.CompactionOrdinal

    if
        eventOrdinal = st.CompactionOrdinal
        && st.CompactionStage <> Terminal
        && st.Compaction |> Option.exists (fun c -> c.CompactionID = ev.Id)
    then
        let nextOwner =
            if st.Owner = Some "Compaction" then
                Some "None"
            else
                st.Owner

        { st with
            Owner = nextOwner
            CompactionStage = Terminal
            Compaction = None
            IsCompacted = false
            CompactionGeneration = 0 }
    else
        st

let private handleContinuationRequested (st: OwnerEpisodeState) (ev: ContinuationRequestEvent) : OwnerEpisodeState =
    let newOrdinal = ev.Ordinal |> Option.defaultValue (st.ContinuationOrdinal + 1)

    if newOrdinal <= st.ContinuationOrdinal then
        st
    else
        let nextLease =
            { ContinuationID = ev.ContinuationId
              ContinuationOrdinal = newOrdinal
              SessionGeneration = ev.Generation |> Option.defaultValue st.SessionGeneration
              HumanTurnID = humanTurnIdOr st.HumanTurn ev.HumanTurnId
              CancelGeneration = ev.CancelGeneration |> Option.defaultValue st.CancelGeneration
              Owner = ev.Owner
              Model = ev.Model
              PromptText = None
              Status = "requested" }

        { st with
            Owner = Some ev.Owner
            ContinuationOrdinal = newOrdinal
            ContinuationStage = Requested
            ContinuationLease = Some nextLease }

let private transitionLease
    (status: string)
    (expected: EpisodeStage)
    (next: EpisodeStage)
    (st: OwnerEpisodeState)
    (ev: EpisodeStageEvent)
    : OwnerEpisodeState =
    let eventOrdinal = ev.Ordinal |> Option.defaultValue st.ContinuationOrdinal

    if eventOrdinal <> st.ContinuationOrdinal || st.ContinuationStage <> expected then
        st
    else
        match
            st.ContinuationLease
            |> Option.bind (fun l ->
                if l.ContinuationID = ev.Id then
                    Some { l with Status = status }
                else
                    None)
        with
        | Some l ->
            { st with
                ContinuationLease = Some l
                ContinuationStage = next }
        | None -> st

let private handleContinuationTerminal (st: OwnerEpisodeState) (ev: EpisodeStageEvent) : OwnerEpisodeState =
    let eventOrdinal = ev.Ordinal |> Option.defaultValue st.ContinuationOrdinal

    if eventOrdinal <> st.ContinuationOrdinal || st.ContinuationStage = Terminal then
        st
    elif st.ContinuationLease |> Option.exists (fun l -> l.ContinuationID = ev.Id) then
        let nextOwner = if st.Owner = Some "Fallback" then Some "None" else st.Owner

        { st with
            Owner = nextOwner
            ContinuationLease = None
            ContinuationStage = Terminal }
    else
        st

let private handleNudgeRequested (st: OwnerEpisodeState) (ev: NudgeRequestEvent) : OwnerEpisodeState =
    let newOrdinal = ev.Ordinal |> Option.defaultValue (st.NudgeOrdinal + 1)

    if newOrdinal <= st.NudgeOrdinal then
        st
    else
        let nextLease =
            { NudgeID = ev.NudgeId
              NudgeOrdinal = newOrdinal
              Nonce = ev.Nonce
              Anchor = ev.Anchor
              HumanTurnID = humanTurnIdOr st.HumanTurn ev.HumanTurnId
              SessionGeneration = ev.Generation |> Option.defaultValue st.SessionGeneration
              CancelGeneration = ev.CancelGeneration |> Option.defaultValue st.CancelGeneration
              Status = "requested" }

        { st with
            Owner = Some "Nudge"
            NudgeOrdinal = newOrdinal
            NudgeStage = Requested
            NudgeLease = Some nextLease }

let private handleNudgeDispatched (st: OwnerEpisodeState) (ev: EpisodeStageEvent) : OwnerEpisodeState =
    let eventOrdinal = ev.Ordinal |> Option.defaultValue st.NudgeOrdinal

    if eventOrdinal <> st.NudgeOrdinal || st.NudgeStage <> Requested then
        st
    else
        match
            st.NudgeLease
            |> Option.bind (fun nl ->
                if nl.NudgeID = ev.Id then
                    Some { nl with Status = "dispatched" }
                else
                    None)
        with
        | Some l ->
            { st with
                NudgeLease = Some l
                NudgeStage = Dispatched }
        | None -> st

let private handleNudgeTerminal (st: OwnerEpisodeState) (ev: EpisodeStageEvent) : OwnerEpisodeState =
    let eventOrdinal = ev.Ordinal |> Option.defaultValue st.NudgeOrdinal

    if eventOrdinal <> st.NudgeOrdinal || st.NudgeStage = Terminal then
        st
    elif st.NudgeLease |> Option.exists (fun nl -> nl.NudgeID = ev.Id) then
        let nextOwner = if st.Owner = Some "Nudge" then Some "None" else st.Owner

        { st with
            Owner = nextOwner
            NudgeLease = None
            NudgeStage = Terminal }
    else
        st

let private handleAssistantCompleted (st: OwnerEpisodeState) : OwnerEpisodeState =
    let nextOwner = if st.Owner = Some "Nudge" then Some "None" else st.Owner
    { st with Owner = nextOwner }

let private handleNudgeDedupClearedOrWip (st: OwnerEpisodeState) : OwnerEpisodeState =
    { st with
        Owner = Some "None"
        NudgeLease = None
        NudgeStage = NoEpisode }

let foldOwnerAndLeaseEvent (st: OwnerEpisodeState) (ev: SessionControlEvent) : OwnerEpisodeState =
    match ev with
    | HumanTurn(ordinal, turn) -> handleHumanTurn st ordinal turn
    | UserAbort -> handleUserAbort st
    | CompactionStarted ev -> handleCompactionStarted st ev
    | ContextGenerationChanged ev -> handleContextGenerationChanged st ev
    | CompactionSettled ev -> handleCompactionSettled st ev
    | ContinuationRequested ev -> handleContinuationRequested st ev
    | ContinuationDispatchStarted ev -> transitionLease "dispatch_started" Requested DispatchStarted st ev
    | ContinuationDispatched ev -> transitionLease "dispatched" DispatchStarted Dispatched st ev
    | ContinuationTerminal ev -> handleContinuationTerminal st ev
    | NudgeRequested ev -> handleNudgeRequested st ev
    | NudgeDispatched ev -> handleNudgeDispatched st ev
    | NudgeTerminal ev -> handleNudgeTerminal st ev
    | AssistantCompleted -> handleAssistantCompleted st
    | NudgeDedupClearedOrWip -> handleNudgeDedupClearedOrWip st
