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
open Wanxiangshu.Shell.FallbackRecoveryWait
open Wanxiangshu.Shell.SubagentIo
open Wanxiangshu.Shell.ErrorClassify
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Omp.ChildSessionCommon
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

            let parentSessionId =
                let s = Dyn.get ctx "sessionId"
                if Dyn.isNullish s then "" else string s

            let run =
                Wanxiangshu.Omp.ChildSessionCommon.runOmpSubagentCore
                    fallbackRuntime
                    fallbackConfigOpt
                    childId
                    session
                    prompt
                    SubagentResetPolicy.KeepState
                    parentSessionId

            let signalObj = Option.defaultValue (box null) signal
            let! text = raceWithAbortSignal signalObj (fun () -> ()) run

            return text
    }
