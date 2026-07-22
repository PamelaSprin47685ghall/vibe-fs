module Wanxiangshu.Hosts.Omp.OmpSubsessionHostAdapterPrompts

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Hosts.Omp.SubsessionQuiescence

/// Status and close query helpers used by OmpSubsessionHostAdapter.

let private checkStatusApi (sessionApi: obj) (sessionId: SessionId) : JS.Promise<QuiescenceStatus option> =
    promise {
        try
            let statusFn = Dyn.get sessionApi "sessionStatus"

            if not (Dyn.isNullish statusFn) && Dyn.typeIs statusFn "function" then
                let arg = box {| sessionId = SessionId.value sessionId |}
                let! resp = unbox<JS.Promise<obj>> (sessionApi?sessionStatus (arg))

                if Dyn.typeIs resp "string" then
                    match (string resp).ToLowerInvariant() with
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
                return None
        with _ ->
            return None
    }

let private checkSessionApi (sessionApi: obj) (sessionId: SessionId) : JS.Promise<QuiescenceStatus option> =
    promise {
        try
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

let internal checkPiSessionStatus (sessionApi: obj) (sessionId: SessionId) : JS.Promise<QuiescenceStatus option> =
    promise {
        let! res1 = checkStatusApi sessionApi sessionId

        match res1 with
        | Some s -> return Some s
        | None -> return! checkSessionApi sessionApi sessionId
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
                    return None
        with _ ->
            return None
    }

let internal tryClosePiSession (sessionApi: obj) (sessionId: SessionId) : JS.Promise<QuiescenceStatus option> =
    promise {
        let arg = box {| sessionId = SessionId.value sessionId |}
        let fnNames = [ "sessionClose"; "closeSession"; "close" ]

        let attempt name =
            promise {
                try
                    let fn = Dyn.get sessionApi name

                    if not (Dyn.isNullish fn) && Dyn.typeIs fn "function" then
                        let! _ = safeResolve (sessionApi?(name) (arg))
                        return Some Stopped
                    else
                        return None
                with _ ->
                    return None
            }

        let rec loop names =
            promise {
                match names with
                | [] -> return None
                | name :: rest ->
                    let! res = attempt name

                    match res with
                    | Some status -> return Some status
                    | None -> return! loop rest
            }

        return! loop fnNames
    }

/// Resolve quiescence for an OMP physical session, consulting the session object,
/// its manager, and the PI session API in order.
let querySessionQuiescence
    (session: obj)
    (sessionApi: obj)
    (sessionId: SessionId)
    (_turnId: TurnId)
    : JS.Promise<QuiescenceStatus> =
    promise {
        let sm = Dyn.get session "sessionManager"

        let localStatus =
            detectStatus session |> Option.orElseWith (fun () -> detectStatus sm)

        match localStatus with
        | Some status -> return status
        | None ->
            if Dyn.isNullish sessionApi then
                return StopUnknown
            else
                let! piStatus = checkPiSessionStatus sessionApi sessionId
                return defaultArg piStatus StopUnknown
    }

/// Close an OMP physical session by trying the session object, then the session
/// manager, then the PI session API.
let closePhysicalSession (session: obj) (sessionApi: obj) (sessionId: SessionId) : JS.Promise<QuiescenceStatus> =
    promise {
        let! localClose = tryCloseSessionObj session

        match localClose with
        | Some status -> return status
        | None ->
            let sm = Dyn.get session "sessionManager"
            let! smClose = tryCloseSessionObj sm

            match smClose with
            | Some status -> return status
            | None ->
                if Dyn.isNullish sessionApi then
                    return StopUnknown
                else
                    let! piClose = tryClosePiSession sessionApi sessionId
                    return defaultArg piClose StopUnknown
    }
