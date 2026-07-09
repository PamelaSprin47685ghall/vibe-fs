module Wanxiangshu.Omp.ChildSessionRegistry

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Omp.Codec
open Wanxiangshu.Omp.MessagingCodec
open Wanxiangshu.Omp.PiResolve
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.RuntimeScope
open Wanxiangshu.Shell.OmpHostBindings
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.SubagentIo
open Wanxiangshu.Kernel.FallbackKernel.Types

module Dyn = Wanxiangshu.Shell.Dyn

let runSubagentOnExistingSession
    (scope: RuntimeScope)
    (pi: obj)
    (ctx: obj)
    (childId: string)
    (prompt: string)
    (signal: obj option)
    (fallbackRuntime: FallbackRuntimeState)
    (fallbackConfigOpt: FallbackConfig option)
    : JS.Promise<string> =
    promise {
        match scope.TryFindKey("omp_session_" + childId) with
        | None -> return failwith ("Child session not found: " + childId)
        | Some sessionObj ->
            let session = unbox<obj> sessionObj

            let run =
                promise {
                    do! sessionPrompt session prompt
                    do! sessionWaitForIdle session
                    let sm = unbox<ISessionManager> (Dyn.get session "sessionManager")
                    let res = readAssistantText sm 0 "\n\n"
                    return Option.defaultValue noOutputText res
                }

            let signalObj = Option.defaultValue (box null) signal
            let! text = raceWithAbortSignal signalObj (fun () -> ()) run

            if childId <> "" && fallbackRuntime.GetConsumed childId <> Some false then
                let pst = fallbackRuntime.GetOrCreateState childId

                if pst.Phase = FallbackPhase.Exhausted then
                    return failwith "Fallback exhausted for child session"

            return text
    }
