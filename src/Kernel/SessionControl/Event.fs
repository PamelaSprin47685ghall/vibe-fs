module Wanxiangshu.Kernel.SessionControl.Event

/// Strongly typed session-control events decoded from the wire envelope.
/// Projections fold these; they never touch Map keys again.

open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Kernel.EventSourcing.EventPayload
open Wanxiangshu.Kernel.SessionControl.HumanTurn

type ContinuationRequestEvent =
    { ContinuationId: string
      Ordinal: int option
      Generation: int option
      CancelGeneration: int option
      HumanTurnId: string option
      Owner: string
      Model: string }

type EpisodeStageEvent = { Id: string; Ordinal: int option }

type NudgeRequestEvent =
    { NudgeId: string
      Ordinal: int option
      Nonce: string
      Anchor: string
      HumanTurnId: string option
      Generation: int option
      CancelGeneration: int option }

type CompactionStartEvent =
    { CompactionId: string
      Ordinal: int option
      GenerationAtStart: int option
      HumanTurnId: string option }

type CompactionStageEvent =
    { CompactionId: string
      Ordinal: int option
      Generation: int option }

type SessionControlEvent =
    | HumanTurn of ordinal: int option * state: HumanTurnState
    | UserAbort
    | ContinuationRequested of ContinuationRequestEvent
    | ContinuationDispatchStarted of EpisodeStageEvent
    | ContinuationDispatched of EpisodeStageEvent
    | ContinuationTerminal of EpisodeStageEvent
    | NudgeRequested of NudgeRequestEvent
    | NudgeDispatched of EpisodeStageEvent
    | NudgeTerminal of EpisodeStageEvent
    | CompactionStarted of CompactionStartEvent
    | ContextGenerationChanged of CompactionStageEvent
    | CompactionSettled of EpisodeStageEvent
    | AssistantCompleted
    | NudgeDedupClearedOrWip

let private episode (idField: string) (ordinalField: string) (e: WanEvent) : EpisodeStageEvent =
    { Id = fieldOr idField "" e
      Ordinal = tryIntField ordinalField e }

let private decodeContinuation (k: string) (e: WanEvent) : SessionControlEvent option =
    if k = eventKindContinuationRequested then
        Some(
            ContinuationRequested
                { ContinuationId = fieldOr Field.continuationId "" e
                  Ordinal = tryIntField Field.continuationOrdinal e
                  Generation = tryIntField Field.generation e
                  CancelGeneration = tryIntField Field.cancelGeneration e
                  HumanTurnId = tryField Field.humanTurnId e
                  Owner = fieldOr Field.owner "Fallback" e
                  Model = fieldOr Field.model "" e }
        )
    elif k = eventKindContinuationDispatchStarted then
        Some(ContinuationDispatchStarted(episode Field.continuationId Field.continuationOrdinal e))
    elif k = eventKindContinuationDispatched then
        Some(ContinuationDispatched(episode Field.continuationId Field.continuationOrdinal e))
    elif
        k = eventKindContinuationFailed
        || k = eventKindContinuationCancelled
        || k = eventKindContinuationSettled
    then
        Some(ContinuationTerminal(episode Field.continuationId Field.continuationOrdinal e))
    else
        None

let private decodeNudge (k: string) (e: WanEvent) : SessionControlEvent option =
    if k = eventKindNudgeRequested then
        Some(
            NudgeRequested
                { NudgeId = fieldOr Field.nudgeId "" e
                  Ordinal = tryIntField Field.nudgeOrdinal e
                  Nonce = fieldOr Field.nonce "" e
                  Anchor = fieldOr Field.anchor "" e
                  HumanTurnId = tryField Field.humanTurnId e
                  Generation = tryIntField Field.generation e
                  CancelGeneration = tryIntField Field.cancelGeneration e }
        )
    elif k = eventKindNudgeDispatched then
        Some(NudgeDispatched(episode Field.nudgeId Field.nudgeOrdinal e))
    elif
        k = eventKindNudgeFailed
        || k = eventKindNudgeCancelled
        || k = eventKindNudgeSettled
    then
        Some(NudgeTerminal(episode Field.nudgeId Field.nudgeOrdinal e))
    else
        None

let private decodeCompaction (k: string) (e: WanEvent) : SessionControlEvent option =
    if k = eventKindCompactionStarted then
        let genAtStart =
            tryIntField Field.generationAtStart e
            |> Option.orElse (tryIntField Field.generation e)

        Some(
            CompactionStarted
                { CompactionId = fieldOr Field.compactionId "" e
                  Ordinal = tryIntField Field.compactionOrdinal e
                  GenerationAtStart = genAtStart
                  HumanTurnId = tryField Field.humanTurnId e }
        )
    elif k = eventKindContextGenerationChanged then
        Some(
            ContextGenerationChanged
                { CompactionId = fieldOr Field.compactionId "" e
                  Ordinal = tryIntField Field.compactionOrdinal e
                  Generation = tryIntField Field.generation e }
        )
    elif k = eventKindCompactionSettled then
        Some(CompactionSettled(episode Field.compactionId Field.compactionOrdinal e))
    else
        None

let decode (e: WanEvent) : SessionControlEvent option =
    let k = e.Kind

    if k = eventKindHumanTurnStarted then
        HumanTurn.foldSingleEvent e
        |> Option.map (fun h -> HumanTurn(tryIntField Field.humanTurnOrdinal e, h))
    elif k = eventKindUserAbortObserved then
        Some UserAbort
    elif k = eventKindAssistantCompleted then
        Some AssistantCompleted
    elif k = eventKindNudgeDedupCleared || k = eventKindSubmitReviewWipRecorded then
        Some NudgeDedupClearedOrWip
    else
        decodeContinuation k e
        |> Option.orElse (decodeNudge k e)
        |> Option.orElse (decodeCompaction k e)
