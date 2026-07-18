module Wanxiangshu.Hosts.Omp.OmpSubsessionHostAdapterPrompts

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Hosts.Omp.SubsessionDispatch

/// Status and close query helpers used by OmpSubsessionHostAdapter.

let internal checkPiSessionStatus (sessionApi: obj) (sessionId: SessionId) : JS.Promise<QuiescenceStatus option> =
    promise {
        try
            let statusFn = Dyn.get sessionApi "sessionStatus"

            if not (Dyn.isNullish statusFn) && Dyn.typeIs statusFn "function" then
                let arg = box {| sessionId = SessionId.value sessionId |}
                let! resp = unbox<JS.Promise<obj>> (sessionApi?sessionStatus (arg))

                if Dyn.typeIs resp "string" then
                    let status = (string resp).ToLowerInvariant()

                    match status with
                    | "idle"
                    | "closed"
                    | "completed"
                    | "done"
                    | "stopped" -> return Some Stopped
                    | "busy"
                    | "running"
                    | "active"
                    | "pending" -> return Some StillRunning
                    | _ -> return None
                else
                    return detectStatus resp
            else
                let getSessionFn = Dyn.get sessionApi "session"

                if not (Dyn.isNullish getSessionFn) && Dyn.typeIs getSessionFn "function" then
                    let arg = box {| sessionId = SessionId.value sessionId |}
                    let! sObj = unbox<JS.Promise<obj>> (sessionApi?session (arg))
                    return detectStatus sObj
                else
                    return None
        with _ ->
            return None
    }

let internal safeResolve (v: obj) : JS.Promise<obj> = emitJsExpr v "Promise.resolve($0)"

let internal tryCloseSessionObj (obj: obj) : JS.Promise<QuiescenceStatus option> =
    promise {
        try
            let disposeFn = Dyn.get obj "dispose"

            if not (Dyn.isNullish disposeFn) && Dyn.typeIs disposeFn "function" then
                let! _ = safeResolve (obj?dispose ())
                return Some Stopped
            else
                let closeFn = Dyn.get obj "close"

                if not (Dyn.isNullish closeFn) && Dyn.typeIs closeFn "function" then
                    let! _ = safeResolve (obj?close ())
                    return Some Stopped
                else
                    let deleteFn = Dyn.get obj "delete"

                    if not (Dyn.isNullish deleteFn) && Dyn.typeIs deleteFn "function" then
                        let! _ = safeResolve (obj?delete ())
                        return Some Stopped
                    else
                        return None
        with _ ->
            return None
    }

let internal tryClosePiSession (sessionApi: obj) (sessionId: SessionId) : JS.Promise<QuiescenceStatus option> =
    promise {
        let arg = box {| sessionId = SessionId.value sessionId |}

        let fnNames =
            [| "sessionDelete"
               "deleteSession"
               "sessionClose"
               "closeSession"
               "delete"
               "close" |]

        let mutable executed = false
        let mutable success = false

        for name in fnNames do
            if not executed then
                try
                    let fn = Dyn.get sessionApi name

                    if not (Dyn.isNullish fn) && Dyn.typeIs fn "function" then
                        executed <- true
                        let! _ = safeResolve (sessionApi?(name) (arg))
                        success <- true
                with _ ->
                    ()

        if success then return Some Stopped else return None
    }
