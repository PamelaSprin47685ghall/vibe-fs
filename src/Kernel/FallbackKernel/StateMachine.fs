module Wanxiangshu.Kernel.FallbackKernel.StateMachine

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackKernel.Decision
open Wanxiangshu.Kernel.FallbackKernel.Recovery

let private sendOrContinue (cfg: FallbackConfig) (model: FallbackModel) (state: SessionFallbackState) =
    if state.ContinueCount >= cfg.LoopMaxContinues then
        { state with ContinueCount = 0 }, FallbackAction.SendContinue model
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

let handleSessionError (state: SessionFallbackState) (cfg: FallbackConfig) (chain: FallbackChain) (err: ErrorInput) =
    if state.Cancelled || state.TaskComplete then
        state, FallbackAction.DoNothing
    else
        let errorClass = classifyError err state cfg

        match state.Phase, errorClass with
        | _, ErrorClass.Ignore ->
            let ns =
                if err.ErrorName = "AbortError" || err.ErrorName = "MessageAbortedError" then
                    { state with Cancelled = true }
                else
                    state

            ns, FallbackAction.DoNothing

        | FallbackPhase.Exhausted, _ -> state, FallbackAction.DoNothing

        | FallbackPhase.Scanning(scanIdx, origIdx), _ ->
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

        | (FallbackPhase.Idle | FallbackPhase.ScanningToolCallText | FallbackPhase.RecoveringToolCallText), ErrorClass.RetrySame ->
            match selectModel chain state.CurrentIndex with
            | Some m ->
                sendOrContinue
                    cfg
                    m
                    { state with
                        Phase = FallbackPhase.Retrying 1 }
            | None ->
                { state with
                    Phase = FallbackPhase.Exhausted },
                FallbackAction.PropagateFailure

        | FallbackPhase.Retrying count, ErrorClass.RetrySame when count < cfg.MaxRetries ->
            match selectModel chain state.CurrentIndex with
            | Some m ->
                sendOrContinue
                    cfg
                    m
                    { state with
                        Phase = FallbackPhase.Retrying(count + 1) }
            | None ->
                { state with
                    Phase = FallbackPhase.Exhausted },
                FallbackAction.PropagateFailure

        | _, ErrorClass.ImmediateFallback
        | _, ErrorClass.Exhausted
        | FallbackPhase.Retrying _, ErrorClass.RetrySame ->
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

let private handleSessionBusy (state: SessionFallbackState) =
    match state.Phase with
    | FallbackPhase.Scanning(scanIdx, origIdx) -> completeScan scanIdx origIdx state
    | FallbackPhase.Retrying _ ->
        { state with Phase = FallbackPhase.Idle }, FallbackAction.DoNothing
    | FallbackPhase.ScanningToolCallText
    | FallbackPhase.RecoveringToolCallText ->
        state, FallbackAction.DoNothing
    | _ -> state, FallbackAction.DoNothing

let private handleSessionIdle (state: SessionFallbackState) =
    match state.Phase with
    | FallbackPhase.Scanning(scanIdx, origIdx) -> completeScan scanIdx origIdx state
    | FallbackPhase.Retrying _ when not state.TaskComplete && not state.Cancelled ->
        { state with Phase = FallbackPhase.ScanningToolCallText }, FallbackAction.ScanToolCallAsText
    | FallbackPhase.Retrying _ ->
        { state with Phase = FallbackPhase.Idle }, FallbackAction.DoNothing
    | FallbackPhase.Idle when not state.TaskComplete && not state.Cancelled ->
        { state with Phase = FallbackPhase.ScanningToolCallText }, FallbackAction.ScanToolCallAsText
    | FallbackPhase.RecoveringToolCallText when not state.TaskComplete && not state.Cancelled ->
        { state with Phase = FallbackPhase.ScanningToolCallText }, FallbackAction.ScanToolCallAsText
    | _ -> state, FallbackAction.DoNothing

let private handleNewUserMessage (state: SessionFallbackState) =
    { state with
        Phase = FallbackPhase.Idle
        ContinueCount = 0
        FailureCount = 0
        CurrentIndex = 0
        Cancelled = false
        TaskComplete = false },
    FallbackAction.DoNothing

let private handleTaskCompleteCalled (state: SessionFallbackState) =
    { state with
        Phase = FallbackPhase.Idle
        TaskComplete = true },
    FallbackAction.DoNothing

let transition (state: SessionFallbackState) (evt: FallbackEvent) (cfg: FallbackConfig) (chain: FallbackChain) =
    match evt with
    | FallbackEvent.SessionError err -> handleSessionError state cfg chain err
    | FallbackEvent.SessionBusy -> handleSessionBusy state
    | FallbackEvent.SessionIdle -> handleSessionIdle state
    | FallbackEvent.NewUserMessage -> handleNewUserMessage state
    | FallbackEvent.TaskCompleteCalled -> handleTaskCompleteCalled state
