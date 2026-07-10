module Wanxiangshu.Shell.FallbackRecoveryWait

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.SessionLoop
open Wanxiangshu.Shell.FallbackRuntimeState

// ── Recovery settlement (unchanged logic, kept for backward compat) ──

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

/// True when fallback work is actively in progress or the session is busy in any way.
/// Maps to `NeedFallbackContinue` in the gate model.
let fallbackGateOpen (runtime: FallbackRuntimeState) (sessionID: string) : bool =
    let taskComplete =
        match runtime.TryGetState sessionID with
        | Some state -> state.TaskComplete
        | None -> false

    if taskComplete then
        false
    elif runtime.IsEventHandlingActive sessionID then
        true
    elif runtime.IsAwaitingBusy sessionID then
        true
    elif runtime.IsSubsessionPending sessionID then
        true
    elif runtime.GetBusyCount sessionID > 0 then
        true
    elif runtime.IsNudgeActive sessionID then
        // nudge activity alone doesn't open the fallback gate, but it blocks resolve
        false
    else
        match runtime.TryGetState sessionID with
        | Some st ->
            match st.Phase with
            | FallbackPhase.Retrying _
            | FallbackPhase.Scanning _
            | FallbackPhase.ScanningToolCallText
            | FallbackPhase.RecoveringToolCallText -> true
            | FallbackPhase.Idle ->
                // Idle + consumed=true + TaskComplete=false = fallback continue received
                // but model turn still running → not terminal, gate stays open.
                match runtime.GetConsumed sessionID with
                | Some true -> not st.TaskComplete
                | _ -> false
            | FallbackPhase.Exhausted -> false
        | None -> false

/// True when the session has reached a terminal observation: TaskComplete, Exhausted,
/// or consumed=false.  A fresh session with no state and no consumed result is NOT
/// terminal — terminality requires explicit evidence (TaskComplete / Exhausted / Consumed=false).
/// Uses TryGetState so observation never creates hidden state.
let terminalObservation (runtime: FallbackRuntimeState) (sessionID: string) : bool =
    match runtime.TryGetState sessionID with
    | Some st ->
        if st.TaskComplete then
            true
        elif st.Phase = FallbackPhase.Exhausted then
            true
        else
            match runtime.GetConsumed sessionID with
            | Some false -> true
            | _ -> false
    | None ->
        match runtime.GetConsumed sessionID with
        | Some false -> true
        | _ -> false

/// Derive the GateState from runtime observation.
/// NeedReviewNudge is always false here because review/nudge dispatch is represented
/// by the runtime's active gate (event handling / awaiting busy), not a separate nudge flag.
let gateState (runtime: FallbackRuntimeState) (sessionID: string) : GateState =
    { NeedFallbackContinue = fallbackGateOpen runtime sessionID
      NeedTodoNudge = runtime.IsNudgeActive sessionID
      NeedReviewNudge = false }

/// The session is settled only when: non-empty sessionID, terminal observation holds,
/// and the gate model resolves to Resolve (no higher-priority gate open).
let isSubagentSettled (runtime: FallbackRuntimeState) (sessionID: string) : bool =
    if sessionID = "" then
        false
    elif not (terminalObservation runtime sessionID) then
        false
    else
        let gates = gateState runtime sessionID
        decide gates = Resolve

/// Register OnStateChanged exactly once; resolve on the next state-change signal.
let private waitForStateChange (runtime: FallbackRuntimeState) (sessionID: string) : JS.Promise<unit> =
    Promise.create (fun resolve _ -> runtime.OnStateChanged sessionID (fun () -> resolve ()))

/// Nested gate loop: fallback continue gate must settle first, then todo/review
/// nudge gates, then resolve.  Each gate waits for exactly one state change
/// before re-evaluating, mirroring the priority order in `SessionLoop.decide`.
let rec waitForSubagentSettle (runtime: FallbackRuntimeState) (sessionID: string) : JS.Promise<unit> =
    promise {
        if sessionID = "" then
            return ()
        elif isSubagentSettled runtime sessionID then
            return ()
        else
            match decide (gateState runtime sessionID) with
            | FallbackContinue ->
                do! waitForStateChange runtime sessionID
                return! waitForSubagentSettle runtime sessionID
            | TodoNudge
            | ReviewNudge ->
                do! waitForStateChange runtime sessionID
                return! waitForSubagentSettle runtime sessionID
            | Resolve ->
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
                    return! waitForSubagentSettle runtime sessionID
    }
