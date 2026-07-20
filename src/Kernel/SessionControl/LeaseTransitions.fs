module Wanxiangshu.Kernel.SessionControl.LeaseTransitions

/// Episode handlers over decoded SessionControlEvent. One handler per event
/// family; ordinals resolve their wire defaults against current state here.

open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.SessionControl.HumanTurn
open Wanxiangshu.Kernel.SessionControl.State
open Wanxiangshu.Kernel.SessionControl.Event
open Wanxiangshu.Kernel.SessionControl.LeaseIdentity
open Wanxiangshu.Kernel.SessionControl.LeaseIdentityOps
open Wanxiangshu.Kernel.SessionControl.LeaseTransitionsCompaction
open Wanxiangshu.Kernel.SessionControl.LeaseTransitionsContinuation
open Wanxiangshu.Kernel.SessionControl.LeaseTransitionsNudge

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
