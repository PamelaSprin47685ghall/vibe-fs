module Wanxiangshu.Shell.FallbackRecoveryWait

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Shell.FallbackRuntimeState

let isRecoverySettled (runtime: FallbackRuntimeState) (sessionID: string) : bool =
    match runtime.GetConsumed sessionID with
    | Some true -> true
    | _ ->
        let st = runtime.GetOrCreateState sessionID
        st.Phase = FallbackPhase.Exhausted

let isToolCallTextRecoveryInProgress (runtime: FallbackRuntimeState) (sessionID: string) : bool =
    let st = runtime.GetOrCreateState sessionID

    match st.Phase with
    | FallbackPhase.ScanningToolCallText
    | FallbackPhase.RecoveringToolCallText -> true
    | _ -> false

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

let isSubagentSettled (runtime: FallbackRuntimeState) (sessionID: string) : bool =
    if sessionID = "" then
        true
    else if runtime.IsEventHandlingActive sessionID || runtime.IsAwaitingBusy sessionID then
        false
    else
        let st = runtime.GetOrCreateState sessionID
        let busy = runtime.GetBusyCount sessionID > 0
        let nudgeAct = runtime.IsNudgeActive sessionID

        if st.TaskComplete && not busy && not nudgeAct then
            true
        else
            let fbActive =
                if not (runtime.HasState sessionID) then
                    false
                else
                    match st.Phase with
                    | FallbackPhase.Scanning _
                    | FallbackPhase.Retrying _
                    | FallbackPhase.ScanningToolCallText
                    | FallbackPhase.RecoveringToolCallText -> true
                    | FallbackPhase.Idle ->
                        match runtime.GetConsumed sessionID with
                        | Some true -> true
                        | _ -> false
                    | FallbackPhase.Exhausted -> false

            let pending = runtime.IsSubsessionPending sessionID

            not pending && not busy && not fbActive && not nudgeAct

let waitForSubagentSettle (runtime: FallbackRuntimeState) (sessionID: string) : JS.Promise<unit> =
    promise {
        if sessionID = "" || isSubagentSettled runtime sessionID then
            return ()
        else
            let resolver = ref (fun () -> ())
            let p = Promise.create (fun resolve reject -> resolver.Value <- resolve)

            let rec checkSettled () =
                if isSubagentSettled runtime sessionID then
                    resolver.Value()
                else
                    runtime.OnStateChanged sessionID checkSettled

            runtime.OnStateChanged sessionID checkSettled
            return! p
    }
