module Wanxiangshu.Shell.FallbackRecoveryWait

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackSubagentGate
open Wanxiangshu.Kernel.SessionLoop
open Wanxiangshu.Shell.FallbackGateObservation
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.ChildSessionMailbox
open Wanxiangshu.Kernel.FallbackKernel

// ── Recovery settlement (with TaskComplete prioritizing terminal state) ──

let isRecoverySettled (runtime: FallbackRuntimeState) (sessionID: string) : bool =
    match runtime.GetConsumed sessionID with
    | Some true -> true
    | _ ->
        match runtime.TryGetState sessionID with
        | Some st -> st.Phase = FallbackPhase.Exhausted
        | None -> false

let isToolCallTextRecoveryInProgress (runtime: FallbackRuntimeState) (sessionID: string) : bool =
    match runtime.TryGetState sessionID with
    | Some st ->
        match st.Phase with
        | FallbackPhase.ScanningToolCallText
        | FallbackPhase.RecoveringToolCallText -> true
        | _ -> false
    | None -> false

let waitForRecovery (runtime: FallbackRuntimeState) (sessionID: string) (_maxTurns: int) : JS.Promise<unit> =
    promise {
        if sessionID = "" || isRecoverySettled runtime sessionID then
            return ()
        else
            let resolver = ref (fun () -> ())
            let p = Promise.create (fun resolve reject -> resolver.Value <- resolve)

            let rec checkSettled () =
                if isRecoverySettled runtime sessionID then
                    resolver.Value()
                else
                    runtime.OnStateChanged sessionID checkSettled

            runtime.OnStateChanged sessionID checkSettled
            return! p
    }

/// Wait for tool-call-as-text recovery to complete.  Returns immediately when
/// no scan or recovery is in progress.  The phase is set to
/// `ScanningToolCallText` synchronously by the state machine *before* any async
/// work, so a caller that arrives after `session.idle` is emitted but before
/// the scan finishes will observe the in-progress phase and block.
let waitForToolCallTextRecovery (runtime: FallbackRuntimeState) (sessionID: string) : JS.Promise<unit> =
    promise {
        if sessionID = "" || not (isToolCallTextRecoveryInProgress runtime sessionID) then
            return ()
        else
            let resolver = ref (fun () -> ())
            let p = Promise.create (fun resolve reject -> resolver.Value <- resolve)

            let rec checkSettled () =
                if not (isToolCallTextRecoveryInProgress runtime sessionID) then
                    resolver.Value()
                else
                    runtime.OnStateChanged sessionID checkSettled

            runtime.OnStateChanged sessionID checkSettled
            return! p
    }

// ── Subagent settlement via explicit SessionLoop gate model ──

let fallbackGateOpen (runtime: FallbackRuntimeState) (sessionID: string) : bool =
    needFallbackContinue (observe runtime sessionID)

let terminalObservation (runtime: FallbackRuntimeState) (sessionID: string) : bool =
    terminalObservation (observe runtime sessionID)

let gateMode (runtime: FallbackRuntimeState) (sessionID: string) : SessionGateMode =
    gateModeFromObservation (observe runtime sessionID)

let isSubagentSettled (runtime: FallbackRuntimeState) (sessionID: string) (expectedRunId: string) : bool =
    match runtime.GetSubsessionRun(sessionID, expectedRunId) with
    | Some run ->
        match run.Status with
        | SubsessionRunStatus.Settled
        | SubsessionRunStatus.Failed
        | SubsessionRunStatus.Cancelled -> true
        | _ -> false
    | None -> terminalObservation runtime sessionID

/// Register OnStateChanged exactly once; resolve on the next state-change signal.
let private waitForStateChange (runtime: FallbackRuntimeState) (sessionID: string) : JS.Promise<unit> =
    Promise.create (fun resolve _ -> runtime.OnStateChanged sessionID (fun () -> resolve ()))

/// Nested gate loop: fallback continue gate must settle first, then todo/review
/// nudge gates, then resolve.  Each gate waits for exactly one state change
/// before re-evaluating, mirroring the priority order in `SessionLoop.decide`.
let rec waitForSubagentSettle
    (runtime: FallbackRuntimeState)
    (sessionID: string)
    (expectedRunId: string)
    : JS.Promise<unit> =
    promise {
        if sessionID = "" then
            return ()
        elif isSubagentSettled runtime sessionID expectedRunId then
            return ()
        else
            let mode = gateMode runtime sessionID
            let action = decide mode

            match action with
            | FallbackContinue ->
                do! waitForStateChange runtime sessionID
                return! waitForSubagentSettle runtime sessionID expectedRunId
            | TodoNudge
            | ReviewNudge ->
                do! waitForStateChange runtime sessionID
                return! waitForSubagentSettle runtime sessionID expectedRunId
            | Resolve ->
                match runtime.GetSubsessionRun(sessionID, expectedRunId) with
                | Some _ ->
                    do! waitForStateChange runtime sessionID
                    return! waitForSubagentSettle runtime sessionID expectedRunId
                | None ->
                    if terminalObservation runtime sessionID then
                        return ()
                    elif
                        not (runtime.HasState sessionID)
                        && not (runtime.IsAwaitingBusy sessionID)
                        && not (runtime.IsNudgeActive sessionID)
                    then
                        // No runtime state ever registered and no gates open → caller
                        // has observed the host's initial idle boundary → settle.
                        return ()
                    else
                        do! waitForStateChange runtime sessionID
                        return! waitForSubagentSettle runtime sessionID expectedRunId
    }

let runSubsessionLoop
    (turnHost: ISessionTurnHost)
    (sessionId: string)
    (initialPrompt: string)
    (cfg: FallbackConfig)
    (chain: FallbackChain)
    (runtime: FallbackRuntimeState)
    (fetchMessages: string -> JS.Promise<obj array>)
    : JS.Promise<Result<unit, string>> =
    promise {
        let mutable state = runtime.GetOrCreateState sessionId

        state <-
            { state with
                Phase = FallbackPhase.Idle
                Lifecycle = FallbackLifecycle.Active
                CurrentIndex = 0
                ContinueCount = 0
                FailureCount = 0
                RecoveryCount = 0 }

        runtime.UpdateState sessionId state

        // Register a listener so tests can detect the loop is running via HasListeners
        runtime.OnStateChanged sessionId (fun () -> ())

        let mutable prompt = initialPrompt
        let mutable loopDone = false
        let mutable finalError = None

        while not loopDone do
            let modelOpt =
                match state.Phase with
                | FallbackPhase.Scanning(scanIdx, _) -> List.tryItem scanIdx chain
                | _ -> List.tryItem state.CurrentIndex chain

            match modelOpt with
            | None ->
                loopDone <- true
                finalError <- Some "No model available in fallback chain"
            | Some model ->
                let! outcome = turnHost.RunOneTurn(sessionId, model, prompt)

                match outcome with
                | TaskCompleted output ->
                    let nextState, _ =
                        StateMachine.transition state FallbackEvent.TaskCompleteCalled cfg chain

                    state <- nextState
                    runtime.UpdateState sessionId state
                    loopDone <- true

                | Failed error ->
                    let nextState, action =
                        StateMachine.transition state (FallbackEvent.SessionError error) cfg chain

                    state <- nextState
                    runtime.UpdateState sessionId state

                    match action with
                    | FallbackAction.SendContinue nextModel -> prompt <- "​" // zero-width space continuation character
                    | FallbackAction.RecoverWithPrompt(nextModel, promptText) -> prompt <- promptText
                    | FallbackAction.PropagateFailure ->
                        loopDone <- true
                        finalError <- Some error.Message
                    | _ ->
                        if state.Phase = FallbackPhase.Exhausted then
                            loopDone <- true
                            finalError <- Some error.Message

                | Cancelled ->
                    let nextState =
                        { state with
                            Lifecycle = FallbackLifecycle.Cancelled }

                    state <- nextState
                    runtime.UpdateState sessionId state
                    loopDone <- true
                    finalError <- Some "cancelled"

                | EndedWithoutTaskComplete ->
                    let! msgs = fetchMessages sessionId

                    if FallbackMessageCodec.allTodosCompleted msgs then
                        let nextState =
                            { state with
                                Phase = FallbackPhase.Idle
                                Lifecycle = FallbackLifecycle.TaskComplete }

                        state <- nextState
                        runtime.UpdateState sessionId state
                        loopDone <- true
                    else
                        match FallbackMessageCodec.scanToolCallAsText msgs with
                        | Some promptText ->
                            let nextState =
                                { state with
                                    Phase = FallbackPhase.RecoveringToolCallText }

                            state <- nextState
                            runtime.UpdateState sessionId state
                            prompt <- promptText
                        | None ->
                            let isToolFinish = FallbackMessageCodec.isLastAssistantToolFinish msgs
                            let hasResult = FallbackMessageCodec.hasToolResultAfter msgs
                            let taskComplete = (not isToolFinish) || hasResult

                            let nextState =
                                { state with
                                    Phase = FallbackPhase.Idle
                                    Lifecycle =
                                        if taskComplete then
                                            FallbackLifecycle.TaskComplete
                                        else
                                            FallbackLifecycle.Active }

                            state <- nextState
                            runtime.UpdateState sessionId state

                            if taskComplete then loopDone <- true else loopDone <- true

        if state.Lifecycle = FallbackLifecycle.TaskComplete then
            return Ok()
        else
            return Error(finalError |> Option.defaultValue "Turn finished without task completion")
    }
