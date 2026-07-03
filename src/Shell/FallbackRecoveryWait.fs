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

[<Emit("new Promise(function(resolve){ queueMicrotask(resolve); })")>]
let private yieldMicrotask () : JS.Promise<unit> = jsNative

let waitForRecovery (runtime: FallbackRuntimeState) (sessionID: string) (maxTurns: int) : JS.Promise<unit> =
    let rec loop (remaining: int) : JS.Promise<unit> =
        promise {
            if sessionID = "" || remaining <= 0 || isRecoverySettled runtime sessionID then ()
            else
                do! yieldMicrotask ()
                do! loop (remaining - 1)
        }
    loop maxTurns