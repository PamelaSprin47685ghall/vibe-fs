module Wanxiangshu.Hosts.Omp.SubsessionHostAdapter

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.CommandProcessor
open Wanxiangshu.Runtime.SubsessionPorts
open Wanxiangshu.Runtime.SubsessionActor
open Wanxiangshu.Hosts.Omp.SubsessionDispatch

/// OMP serial prompt API: resolve means prompt entered the ordered stream
/// (host-guaranteed barrier). Receipt is OrderedTurnMarkerObserved.
///
/// Contract: current turn error/idle events NEVER arrive before session.prompt resolves.
let private detectStatus (obj: obj) : QuiescenceStatus option =
    if Dyn.isNullish obj then
        None
    else
        let isIdleVal = Dyn.get obj "isIdle"

        if not (Dyn.isNullish isIdleVal) && Dyn.typeIs isIdleVal "boolean" then
            if unbox<bool> isIdleVal then
                Some Stopped
            else
                Some StillRunning
        else
            let isBusyVal = Dyn.get obj "isBusy"

            if not (Dyn.isNullish isBusyVal) && Dyn.typeIs isBusyVal "boolean" then
                if unbox<bool> isBusyVal then
                    Some StillRunning
                else
                    Some Stopped
            else
                let statusVal = Dyn.get obj "status"

                if not (Dyn.isNullish statusVal) && Dyn.typeIs statusVal "string" then
                    let status = (string statusVal).ToLowerInvariant()

                    match status with
                    | "idle"
                    | "closed"
                    | "completed"
                    | "done"
                    | "stopped" -> Some Stopped
                    | "busy"
                    | "running"
                    | "active"
                    | "pending" -> Some StillRunning
                    | _ -> None
                else
                    let activeTurn = Dyn.get obj "activeTurn"
                    let runningTurn = Dyn.get obj "runningTurn"
                    let currentTurn = Dyn.get obj "currentTurn"

                    if
                        (not (Dyn.isNullish activeTurn))
                        || (not (Dyn.isNullish runningTurn))
                        || (not (Dyn.isNullish currentTurn))
                    then
                        Some StillRunning
                    else
                        None

let private checkPiSessionStatus (sessionApi: obj) (sessionId: SessionId) : JS.Promise<QuiescenceStatus option> =
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

let private safeResolve (v: obj) : JS.Promise<obj> = emitJsExpr v "Promise.resolve($0)"

let private tryCloseSessionObj (obj: obj) : JS.Promise<QuiescenceStatus option> =
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

let private tryClosePiSession (sessionApi: obj) (sessionId: SessionId) : JS.Promise<QuiescenceStatus option> =
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

type OmpSubsessionHost(session: obj, agent: string, pi: obj) =
    interface ISubsessionHost with
        member _.Dispatch(sessionId, turn) =
            SubsessionDispatch.dispatch session agent sessionId turn

        member _.Abort(sessionId, _turnId) =
            promise {
                let mutable requested = false
                let mutable sawApi = false

                try
                    let abortFn = Dyn.get session "abort"

                    if not (Dyn.isNullish abortFn) then
                        sawApi <- true
                        do! unbox<JS.Promise<obj>> (session?abort ()) |> Promise.map ignore
                        requested <- true
                with _ ->
                    ()

                try
                    let sessionApi = Dyn.get pi "session"

                    if not (Dyn.isNullish sessionApi) then
                        sawApi <- true
                        let arg = box {| sessionId = SessionId.value sessionId |}
                        do! unbox<JS.Promise<obj>> (sessionApi?sessionAbort (arg)) |> Promise.map ignore
                        requested <- true
                with _ ->
                    ()

                if requested then return RequestAcceptedAwaitIdle
                elif sawApi then return AbortUnavailable
                else return AbortUnavailable
            }

        member _.CancelPendingDispatch(turnId) =
            // Best effort: the new unified DispatchRegistry does not
            // yet own an OMP per-session mailbox (OMP path goes through
            // the serial `session.prompt` Promise).  The previous
            // implementation was a no-op; we now at least record a
            // typed failure so the caller can distinguish "no-op"
            // from "unknown dispatch".  When the OMP per-session
            // mailbox lands, this becomes a registry call.
            let nonce = TurnId.value turnId
            ignore nonce

        member _.QueryDispatchStatus(sessionId, turnId) =
            promise {
                try
                    let sessionApi = Dyn.get pi "session"

                    let! dataOpt =
                        promise {
                            if not (Dyn.isNullish sessionApi) then
                                let arg = box {| sessionId = SessionId.value sessionId |}
                                let! resp = unbox<JS.Promise<obj>> (sessionApi?sessionMessages (arg))
                                return Some(Dyn.get resp "data")
                            else
                                let sm = Dyn.get session "sessionManager"

                                if not (Dyn.isNullish sm) then
                                    return Some(Dyn.get sm "messages")
                                else
                                    return None
                        }

                    match dataOpt with
                    | Some data when not (Dyn.isNullish data) && Dyn.isArray data ->
                        let msgs = unbox<obj array> data
                        let target = TurnId.value turnId
                        let mutable found = false

                        for msg in msgs do
                            let info = Dyn.get msg "info"

                            if not (Dyn.isNullish info) then
                                let cId1 = Dyn.str info "continuationId"
                                let cId2 = Dyn.str info "continuationID"

                                if cId1 = target || cId2 = target then
                                    found <- true

                        if found then
                            return DispatchStatus.Accepted OrderedTurnMarkerObserved
                        else
                            return DispatchStatus.Unknown
                    | _ -> return DispatchStatus.Unknown
                with _ ->
                    return DispatchStatus.Unknown
            }

        member _.QuerySessionQuiescence(sessionId, _turnId) =
            promise {
                match detectStatus session with
                | Some status -> return status
                | None ->
                    let sm = Dyn.get session "sessionManager"

                    match detectStatus sm with
                    | Some status -> return status
                    | None ->
                        let sessionApi = Dyn.get pi "session"

                        if not (Dyn.isNullish sessionApi) then
                            let! piStatus = checkPiSessionStatus sessionApi sessionId

                            match piStatus with
                            | Some status -> return status
                            | None -> return StopUnknown
                        else
                            return StopUnknown
            }

        member _.ClosePhysicalSession(sessionId) =
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
                        let sessionApi = Dyn.get pi "session"

                        if not (Dyn.isNullish sessionApi) then
                            let! piClose = tryClosePiSession sessionApi sessionId

                            match piClose with
                            | Some status -> return status
                            | None -> return StopUnknown
                        else
                            return StopUnknown
            }

let createHost (session: obj) (agent: string) (pi: obj) : ISubsessionHost =
    OmpSubsessionHost(session, agent, pi) :> ISubsessionHost
