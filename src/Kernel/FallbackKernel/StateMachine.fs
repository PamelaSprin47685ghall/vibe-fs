module Wanxiangshu.Kernel.FallbackKernel.StateMachine

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackKernel.Decision
open Wanxiangshu.Kernel.FallbackKernel.Recovery

let private sendOrContinue (cfg: FallbackConfig) (model: FallbackModel) (state: SessionFallbackState) =
    if state.ContinueCount >= cfg.LoopMaxContinues then
        { state with
            Phase = FallbackPhase.Exhausted
            ContinueCount = 0 },
        FallbackAction.PropagateFailure
    else
        { state with
            ContinueCount = state.ContinueCount + 1 },
        FallbackAction.SendContinue model

let private completeScan (scanIdx: int) (origIdx: int) (state: SessionFallbackState) =
    let k = updateFailureCount scanIdx origIdx state.FailureCount

    { state with
        Phase = FallbackPhase.Idle
        CurrentIndex = scanIdx
        FailureCount = k },
    FallbackAction.DoNothing

let private busyLifecycle (state: SessionFallbackState) =
    match state.Lifecycle with
    | FallbackLifecycle.TaskComplete ->
        { state with
            Lifecycle = FallbackLifecycle.Active }
    | _ -> state

let private handleScanningError
    (state: SessionFallbackState)
    (cfg: FallbackConfig)
    (chain: FallbackChain)
    (scanIdx: int)
    (origIdx: int)
    =
    let nextIdx = scanIdx + 1

    match selectModel chain nextIdx with
    | Some m ->
        sendOrContinue
            cfg
            m
            { state with
                Phase = FallbackPhase.Scanning(nextIdx, origIdx) }
    | None ->
        { state with
            Phase = FallbackPhase.Exhausted },
        FallbackAction.PropagateFailure

let private handleRetryingError
    (state: SessionFallbackState)
    (cfg: FallbackConfig)
    (chain: FallbackChain)
    (nextCount: int)
    =
    match selectModel chain state.CurrentIndex with
    | Some m ->
        sendOrContinue
            cfg
            m
            { state with
                Phase = FallbackPhase.Retrying nextCount }
    | None ->
        { state with
            Phase = FallbackPhase.Exhausted },
        FallbackAction.PropagateFailure

let private handleExhaustedError (state: SessionFallbackState) (cfg: FallbackConfig) (chain: FallbackChain) =
    let k = state.FailureCount + 1
    let start = scanStartIndex k state.CurrentIndex

    match selectModel chain start with
    | Some m ->
        sendOrContinue
            cfg
            m
            { state with
                Phase = FallbackPhase.Scanning(start, state.CurrentIndex)
                FailureCount = k }
    | None ->
        { state with
            Phase = FallbackPhase.Exhausted },
        FallbackAction.PropagateFailure

let handleSessionError (state: SessionFallbackState) (cfg: FallbackConfig) (chain: FallbackChain) (err: ErrorInput) =
    match state.Lifecycle, state.Phase with
    | FallbackLifecycle.Cancelled, _
    | FallbackLifecycle.TaskComplete, _ -> state, FallbackAction.DoNothing
    | _, _ ->
        let errorClass = classifyError err state cfg

        match state.Phase, errorClass with
        | _, ErrorClass.Ignore ->
            let ns =
                if errorInputIsAbort err then
                    { state with
                        Lifecycle = FallbackLifecycle.Cancelled }
                else
                    state

            ns, FallbackAction.DoNothing
        | FallbackPhase.Exhausted, _ -> state, FallbackAction.DoNothing
        | FallbackPhase.Scanning(scanIdx, origIdx), _ -> handleScanningError state cfg chain scanIdx origIdx
        | (FallbackPhase.Idle | FallbackPhase.ScanningToolCallText | FallbackPhase.RecoveringToolCallText),
          ErrorClass.RetrySame -> handleRetryingError state cfg chain 1
        | FallbackPhase.Retrying count, ErrorClass.RetrySame when count < cfg.MaxRetries ->
            handleRetryingError state cfg chain (count + 1)
        | _, ErrorClass.ImmediateFallback
        | _, ErrorClass.Exhausted
        | FallbackPhase.Retrying _, ErrorClass.RetrySame -> handleExhaustedError state cfg chain

let private handleSessionBusy (state: SessionFallbackState) =
    match state.Lifecycle with
    | FallbackLifecycle.TaskComplete -> busyLifecycle state, FallbackAction.DoNothing
    | _ ->
        let ns = busyLifecycle state

        match ns.Phase with
        | FallbackPhase.Scanning(scanIdx, origIdx) -> completeScan scanIdx origIdx ns
        | FallbackPhase.Retrying _ -> { ns with Phase = FallbackPhase.Idle }, FallbackAction.DoNothing
        | FallbackPhase.ScanningToolCallText
        | FallbackPhase.RecoveringToolCallText -> ns, FallbackAction.DoNothing
        | _ -> ns, FallbackAction.DoNothing

let private handleSessionIdle (state: SessionFallbackState) =
    match state.Lifecycle with
    | FallbackLifecycle.Cancelled -> state, FallbackAction.DoNothing
    | FallbackLifecycle.TaskComplete ->
        { state with
            Lifecycle = FallbackLifecycle.Active },
        FallbackAction.DoNothing
    | FallbackLifecycle.Active ->
        match state.Phase with
        | FallbackPhase.Scanning(scanIdx, origIdx) -> completeScan scanIdx origIdx state
        | FallbackPhase.Retrying _ ->
            { state with
                Phase = FallbackPhase.ScanningToolCallText },
            FallbackAction.ScanToolCallAsText
        | FallbackPhase.Idle ->
            { state with
                Phase = FallbackPhase.ScanningToolCallText },
            FallbackAction.ScanToolCallAsText
        | FallbackPhase.RecoveringToolCallText ->
            { state with
                Phase = FallbackPhase.ScanningToolCallText },
            FallbackAction.ScanToolCallAsText
        | _ -> state, FallbackAction.DoNothing

let private handleNewUserMessage (state: SessionFallbackState) =
    { state with
        Phase = FallbackPhase.Idle
        ContinueCount = 0
        FailureCount = 0
        CurrentIndex = 0
        Lifecycle = FallbackLifecycle.Active },
    FallbackAction.DoNothing

let private handleTaskCompleteCalled (state: SessionFallbackState) =
    { state with
        Phase = FallbackPhase.Idle
        Lifecycle = FallbackLifecycle.TaskComplete },
    FallbackAction.DoNothing

let transition (state: SessionFallbackState) (evt: FallbackEvent) (cfg: FallbackConfig) (chain: FallbackChain) =
    match evt with
    | FallbackEvent.SessionError err -> handleSessionError state cfg chain err
    | FallbackEvent.SessionBusy -> handleSessionBusy state
    | FallbackEvent.SessionIdle -> handleSessionIdle state
    | FallbackEvent.NewUserMessage -> handleNewUserMessage state
    | FallbackEvent.TaskCompleteCalled -> handleTaskCompleteCalled state
