module Wanxiangshu.Kernel.Session.Causality

[<RequireQualifiedAccess>]
/// Identifies a session lifecycle generation.
/// sessionGeneration increments on session create/restart, NOT on compaction.
type SessionEpoch =
    { SessionId: string
      SessionGeneration: int
      Lifecycle: string }

/// Binds every input/effect to its causal context.
/// session-start observations can use owner=None, humanTurnID=None
/// only when ObservationOptions allow it.
type CausalityContext =
    { SessionEpoch: SessionEpoch
      HumanTurnID: string option
      CancelGeneration: int
      ContextGeneration: int
      ContinuationID: string option
      RequestID: string
      Owner: string option }

[<RequireQualifiedAccess>]
/// Options for observations that may lack full identity.
type ObservationOptions =
    { AllowUnowned: bool
      AllowSessionStart: bool }

module ObservationOptions =
    let defaultOptions: ObservationOptions =
        { AllowUnowned = false
          AllowSessionStart = false }

    let sessionStart: ObservationOptions =
        { AllowUnowned = true
          AllowSessionStart = true }

module CausalityContext =

    let create
        (sessionEpoch: SessionEpoch)
        (humanTurnID: string option)
        (cancelGeneration: int)
        (contextGeneration: int)
        (continuationID: string option)
        (requestID: string)
        (owner: string option)
        : CausalityContext =
        { SessionEpoch = sessionEpoch
          HumanTurnID = humanTurnID
          CancelGeneration = cancelGeneration
          ContextGeneration = contextGeneration
          ContinuationID = continuationID
          RequestID = requestID
          Owner = owner }

    let isOwned (ctx: CausalityContext) : bool = ctx.Owner.IsSome

    let tryGetOwner (ctx: CausalityContext) : string option = ctx.Owner

    let hasHumanTurn (ctx: CausalityContext) : bool = ctx.HumanTurnID.IsSome

    let isContinuation (ctx: CausalityContext) : bool = ctx.ContinuationID.IsSome

    let sessionEpoch (ctx: CausalityContext) : SessionEpoch = ctx.SessionEpoch

    let requestId (ctx: CausalityContext) : string = ctx.RequestID

    let toSessionId (ctx: CausalityContext) : string = ctx.SessionEpoch.SessionId

/// Identity of a single human turn, carried by every Input and EffectResult.
/// Prevents old fallback, old nudge, and old model observations from
/// polluting a new turn. Only session-start / unowned observations may
/// omit this; they use CausalityContext with explicit ObservationOptions.
[<RequireQualifiedAccess>]
type TurnIdentity =
    { SessionId: string
      HumanTurnId: string
      SessionGeneration: int
      CancelGeneration: int
      UserMessageId: string }

module TurnIdentity =
    let create
        (sessionId: string)
        (humanTurnId: string)
        (sessionGeneration: int)
        (cancelGeneration: int)
        (userMessageId: string)
        : TurnIdentity =
        { SessionId = sessionId
          HumanTurnId = humanTurnId
          SessionGeneration = sessionGeneration
          CancelGeneration = cancelGeneration
          UserMessageId = userMessageId }

    let isStale (identity: TurnIdentity) (currentSessionGeneration: int) (currentCancelGeneration: int) : bool =
        identity.SessionGeneration < currentSessionGeneration
        || identity.CancelGeneration < currentCancelGeneration

    let matchesCausality (identity: TurnIdentity) (ctx: CausalityContext) : bool =
        identity.SessionId = ctx.SessionEpoch.SessionId
        && identity.HumanTurnId = (ctx.HumanTurnID |> Option.defaultValue "")
        && identity.SessionGeneration = ctx.SessionEpoch.SessionGeneration
        && identity.CancelGeneration = ctx.CancelGeneration

/// Origin classification for a terminal event, used by nudge to decide
/// whether to proceed with todo/review nudges.  PRD-06 §4.
[<RequireQualifiedAccess>]
type TerminalEventOrigin =
    | HumanTurnCompleted
    | HumanTurnAborted
    | CompactionSummaryCompleted
    | CompactionContinuationCompleted
    | FallbackContinuationCompleted
    | TitleCompleted
    | NudgeCompleted
    | ToolSubturnCompleted
    | Unknown

module TerminalEventOrigin =
    /// Only HumanTurnCompleted may enter normal todo/review nudge.
    let allowNudge (origin: TerminalEventOrigin) : bool =
        match origin with
        | TerminalEventOrigin.HumanTurnCompleted -> true
        | TerminalEventOrigin.HumanTurnAborted
        | TerminalEventOrigin.CompactionSummaryCompleted
        | TerminalEventOrigin.CompactionContinuationCompleted
        | TerminalEventOrigin.FallbackContinuationCompleted
        | TerminalEventOrigin.TitleCompleted
        | TerminalEventOrigin.NudgeCompleted
        | TerminalEventOrigin.ToolSubturnCompleted
        | TerminalEventOrigin.Unknown -> false

    let isFallback (origin: TerminalEventOrigin) : bool =
        match origin with
        | TerminalEventOrigin.FallbackContinuationCompleted -> true
        | _ -> false

    let isCompaction (origin: TerminalEventOrigin) : bool =
        match origin with
        | TerminalEventOrigin.CompactionSummaryCompleted
        | TerminalEventOrigin.CompactionContinuationCompleted -> true
        | _ -> false

    let isHuman (origin: TerminalEventOrigin) : bool =
        match origin with
        | TerminalEventOrigin.HumanTurnCompleted
        | TerminalEventOrigin.HumanTurnAborted -> true
        | _ -> false
