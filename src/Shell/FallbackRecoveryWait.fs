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

let waitForRecovery (runtime: FallbackRuntimeState) (sessionID: string) (_maxTurns: int) : JS.Promise<unit> =
    promise {
        if sessionID = "" || isRecoverySettled runtime sessionID then
            return ()
        else
            let resolver = ref (fun () -> ())
            let p = Promise.create (fun resolve reject ->
                resolver.Value <- resolve
            )
            let rec checkSettled () =
                if isRecoverySettled runtime sessionID then
                    resolver.Value ()
                else
                    runtime.OnStateChanged sessionID checkSettled

            runtime.OnStateChanged sessionID checkSettled
            return! p
    }