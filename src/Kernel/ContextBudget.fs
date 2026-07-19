module Wanxiangshu.Kernel.ContextBudget

[<RequireQualifiedAccess>]
type UsageConfidence =
    | Observed
    | CalibratedEstimate
    | BootstrapEstimate

type PendingOutbound = { Fingerprint: string; Bytes: int }

type UsageCalibration =
    { AssistantMessageID: string
      InputTokens: int64
      OutboundBytes: int }

type UsageObservation =
    { AssistantMessageID: string
      InputTokens: int64 }

let calibrateUsage (observation: UsageObservation) (outbound: PendingOutbound) : UsageCalibration option =
    if observation.InputTokens > 0L && outbound.Bytes > 0 then
        Some
            { AssistantMessageID = observation.AssistantMessageID
              InputTokens = observation.InputTokens
              OutboundBytes = outbound.Bytes }
    else
        None

let tryCalibrateFromObservation
    (observation: UsageObservation)
    (maybePriorID: string option)
    (pendingOutbound: PendingOutbound option)
    : UsageCalibration option =
    // Accept only when observation has a non-empty ID that differs from prior,
    // and both token count and outbound bytes are positive.
    if
        observation.AssistantMessageID = ""
        || maybePriorID = Some observation.AssistantMessageID
        || observation.InputTokens <= 0L
    then
        None
    else
        match pendingOutbound with
        | Some po when po.Bytes >= 2000 -> calibrateUsage observation po
        | _ -> None

let estimateTokensFromCalibration (calibration: UsageCalibration) (bytes: int) : int64 option =
    if calibration.OutboundBytes <= 0 || bytes <= 0 || calibration.InputTokens <= 0L then
        None
    else
        Some(calibration.InputTokens * int64 bytes / int64 calibration.OutboundBytes)

let bootstrapHardSafety (tokens: int64) (effectiveLimit: int64) : bool =
    effectiveLimit > 0L && tokens * 4L >= effectiveLimit * 3L

/// Nudge 触发所需的 todo anchor 数。foldAfterFirst=true 需 2 个 anchor
/// 才缩减投影，foldAfterFirst=false 需 3 个。每次 anchor = 一次 todowrite
/// 调用。Nudge 触发后 LLM 需连续 N 次 todowrite 才能让投影缩减上下文，
/// 故触发点到 compaction 之间须预留 N 份 todowrite 空间 + 1 份 reserve。
let requiredFoldAnchorCount (foldAfterFirst: bool) : int = if foldAfterFirst then 2 else 3

type ProjectionBudgetCycle =
    { BaselineTokens: int64
      BaselineTodoOrdinal: int
      FoldFrontierOrdinal: int
      CompletedSegments: int
      RemainingTodoWritesUntilFold: int }

type ContextState = ProjectionBudgetCycle

/// Generalized trigger: (Q+M+1)*a >= (Q+1)*bEff + M*P
/// Q=CompletedSegments, M=RemainingTodoWritesUntilFold, P=BaselineTokens.
let F (a: int64) (bEff: int64) (P: int64) (Q: int) (M: int) : bool =
    let q = int64 Q
    let m = int64 M
    (q + m + 1L) * a >= (q + 1L) * bEff + m * P

/// Baseline >= 80% of effective limit → fold saturated, hand off to host compact.
let isCompactingRequired (baselineTokens: int64) (maxInputTokens: int64) : bool =
    baselineTokens >= (maxInputTokens * 8L) / 10L

let beginCycle (baselineTokens: int64) (baselineTodoOrdinal: int) (remainingUntilFold: int) : ProjectionBudgetCycle =
    { BaselineTokens = baselineTokens
      BaselineTodoOrdinal = baselineTodoOrdinal
      FoldFrontierOrdinal = baselineTodoOrdinal
      CompletedSegments = 0
      RemainingTodoWritesUntilFold = remainingUntilFold }

let advanceSegment (cycle: ProjectionBudgetCycle) (currentTodoOrdinal: int) : ProjectionBudgetCycle =
    let completed = max 0 (currentTodoOrdinal - cycle.BaselineTodoOrdinal)
    let remaining = max 0 (cycle.RemainingTodoWritesUntilFold - completed)

    { cycle with
        CompletedSegments = completed
        RemainingTodoWritesUntilFold = remaining }

let rebuildCycleAtFold
    (newBaselineTokens: int64)
    (newTodoOrdinal: int)
    (newFoldFrontier: int)
    (newRemainingUntilFold: int)
    : ProjectionBudgetCycle =
    { BaselineTokens = newBaselineTokens
      BaselineTodoOrdinal = newTodoOrdinal
      FoldFrontierOrdinal = newFoldFrontier
      CompletedSegments = 0
      RemainingTodoWritesUntilFold = newRemainingUntilFold }

let estimateTokens (currentTextBytes: int) (lastUsage: {| tokenCount: int; textBytes: int |} option) : int option =
    match lastUsage with
    | Some u when u.textBytes > 0 && currentTextBytes >= 0 ->
        let estimated = (int64 u.tokenCount * int64 currentTextBytes) / int64 u.textBytes
        Some(int estimated)
    | _ -> None

/// Host strips synthetic nudge each transform round; reinject whenever pressure still holds.
/// EmergencySignaled carries the todo ordinal at which the signal was emitted,
/// so classifyNudgeAction can distinguish LLM spontaneous todo from signal response.
type BudgetNudgeTrack =
    | Idle
    | EmergencySignaled of signalTodoOrdinal: int

/// Measurement quality indicator for context budget.
type MeasurementQuality =
    | Precise
    | Estimated
    | Degraded

type ContextBudgetPressure =
    | Disabled
    | BelowThreshold
    | Compacting
    | RequireTodoWriteEmergency

type NudgeAction =
    | NoNudge
    | InjectFirstSignal
    | InjectSameEpisode
    | InjectCatchUp

let classifyNudgeAction
    (pressure: ContextBudgetPressure)
    (track: BudgetNudgeTrack)
    (currentTodoOrdinal: int)
    (nudgeCount: int)
    (lastObservedTodoOrdinal: int option)
    : NudgeAction =
    match pressure with
    | RequireTodoWriteEmergency ->
        let observed = lastObservedTodoOrdinal |> Option.defaultValue 0
        let hasNewTodo = currentTodoOrdinal > observed

        match track with
        | EmergencySignaled signaled ->
            if hasNewTodo && currentTodoOrdinal > signaled then
                InjectCatchUp
            elif hasNewTodo then
                // LLM did a todo at or before the signal ordinal → spontaneous, give space
                NoNudge
            else
                // No new todo, still hot → reinject same episode
                InjectSameEpisode
        | Idle ->
            if hasNewTodo then
                // LLM spontaneously did a todo without being nudged → no nudge
                NoNudge
            elif nudgeCount >= 2 then
                InjectCatchUp
            else
                InjectFirstSignal
    | _ -> NoNudge

/// Effective budget calculation.
/// The caller/host provides the effective budget (e.g. limit minus reserve).
/// This function acts as an identity.
let effectiveMaxInputTokens (maxInputTokens: int) : int64 = int64 maxInputTokens

let classifyPressure (maxInputTokens: int) (currentTokens: int64) (state: ContextState) : ContextBudgetPressure =
    if maxInputTokens <= 0 then
        Disabled
    else
        let bEff = effectiveMaxInputTokens maxInputTokens

        if F currentTokens bEff state.BaselineTokens state.CompletedSegments state.RemainingTodoWritesUntilFold then
            RequireTodoWriteEmergency
        elif isCompactingRequired state.BaselineTokens bEff then
            Compacting
        else
            BelowThreshold

/// Conservative fallback: estimate current tokens from last known usage,
/// degrading gracefully when measurements are unavailable.
/// Returns Some(currentTokens) with a MeasurementQuality indicator.
/// Reuses estimateTokens() for token/byte ratio estimation.
let resolveCurrentTokens
    (maybeCurrentUsage: int64 option)
    (maybeRatio: (int * int) option)
    (encodedBytes: int)
    : (int64 * MeasurementQuality) option =
    match maybeCurrentUsage with
    | Some tokens -> Some(tokens, MeasurementQuality.Precise)
    | None ->
        let ratio =
            maybeRatio |> Option.map (fun (tc, tb) -> {| tokenCount = tc; textBytes = tb |})

        match estimateTokens encodedBytes ratio with
        | Some estimated -> Some(int64 estimated, MeasurementQuality.Estimated)
        | None ->
            // Neither real-time nor historical data available.
            // Conservative fallback: ~2 bytes/token (empirically reasonable for code-heavy
            // workloads; may degrade precision for CJK-heavy text).
            // The caller treats Degraded as a signal to be extra conservative.
            if encodedBytes > 0 then
                Some(int64 encodedBytes / 2L, MeasurementQuality.Degraded)
            else
                None
