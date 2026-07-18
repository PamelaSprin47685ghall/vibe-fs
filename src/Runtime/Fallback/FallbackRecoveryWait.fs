module Wanxiangshu.Runtime.Fallback.FallbackRecoveryWait

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore

// ── Recovery settlement (with TaskComplete prioritizing terminal state) ──

let isRecoverySettled (runtime: FallbackRuntimeStore) (sessionID: string) : bool =
    match (runtime.GetSession sessionID).Consumed with
    | Some true -> true
    | _ ->
        match runtime.TryGetState sessionID with
        | Some st when
            st.Lifecycle = FallbackLifecycle.Cancelled
            || st.Lifecycle = FallbackLifecycle.TaskComplete
            ->
            true
        | Some st -> st.Phase = FallbackPhase.Exhausted
        | None -> false

let isToolCallTextRecoveryInProgress (runtime: FallbackRuntimeStore) (sessionID: string) : bool =
    match runtime.TryGetState sessionID with
    | Some st ->
        match st.Phase with
        | FallbackPhase.ScanningToolCallText
        | FallbackPhase.RecoveringToolCallText -> true
        | _ -> false
    | None -> false

let waitForRecovery (runtime: FallbackRuntimeStore) (sessionID: string) (_maxTurns: int) : JS.Promise<unit> =
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
let waitForToolCallTextRecovery (runtime: FallbackRuntimeStore) (sessionID: string) : JS.Promise<unit> =
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
