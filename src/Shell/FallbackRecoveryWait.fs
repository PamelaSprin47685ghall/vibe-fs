module Wanxiangshu.Shell.FallbackRecoveryWait

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackSubagentGate
open Wanxiangshu.Kernel.SessionLoop
open Wanxiangshu.Shell.FallbackGateObservation
open Wanxiangshu.Shell.FallbackRuntimeState

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
            match decide (gateMode runtime sessionID) with
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
