module Wanxiangshu.Hosts.Omp.ChildSessionRegistry

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Hosts.Omp.Codec
open Wanxiangshu.Hosts.Omp.MessagingCodec
open Wanxiangshu.Hosts.Omp.PiResolve
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.OmpHostBindings
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Hosts.Omp.SubagentRuntime
open Wanxiangshu.Kernel.FallbackKernel.Types

module Dyn = Wanxiangshu.Runtime.Dyn

let runSubagentOnExistingSession
    (scope: RuntimeScope)
    (pi: obj)
    (ctx: obj)
    (childId: string)
    (prompt: string)
    (signal: obj option)
    (fallbackRuntime: FallbackRuntimeStore)
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

            let root = Dyn.str ctx "cwd"

            let! text =
                Wanxiangshu.Hosts.Omp.SubagentRuntime.runOmpSubagentCore
                    fallbackRuntime
                    fallbackConfigOpt
                    root
                    childId
                    session
                    prompt
                    SubagentResetPolicy.KeepState
                    parentSessionId
                    pi
                    signal

            return text
    }
