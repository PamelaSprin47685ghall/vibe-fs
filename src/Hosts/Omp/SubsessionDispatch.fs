module Wanxiangshu.Hosts.Omp.SubsessionDispatch

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn

let dispatch
    (session: obj)
    (_agent: string)
    (_sessionId: SessionId)
    (turn: TurnPlan)
    : JS.Promise<Result<HostStartReceipt, DispatchFailure>> =
    promise {
        if Dyn.isNullish session then
            return
                Error(
                    DispatchFailure.HostRejected
                        { ErrorName = "HostUnavailable"
                          DomainError = None
                          Message = "OMP host session object is nullish"
                          StatusCode = None
                          IsRetryable = Some false }
                )
        else
            let promptFn = Dyn.get session "prompt"

            if Dyn.isNullish promptFn || not (Dyn.typeIs promptFn "function") then
                return
                    Error(
                        DispatchFailure.HostRejected
                            { ErrorName = "PromptUnavailable"
                              DomainError = None
                              Message = "OMP host session does not support prompt API"
                              StatusCode = None
                              IsRetryable = Some false }
                    )
            else
                try
                    // OMP session.prompt accepts the raw prompt string.
                    let! _response = unbox<JS.Promise<obj>> (session?prompt (turn.Prompt))
                    return Ok OrderedTurnMarkerObserved
                with ex ->
                    return
                        Error(
                            DispatchFailure.HostAcceptanceUnknown
                                { ErrorName = "DispatchFailed"
                                  DomainError = None
                                  Message = ex.Message
                                  StatusCode = None
                                  IsRetryable = Some true }
                        )
    }

/// Inspect a raw JS object and classify it as quiescent / still-running /
/// unknown.  Used by both the OpenCode adapter and the OMP host.
let detectStatus (obj: obj) : QuiescenceStatus option =
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

let private tryGetSessionMessages (session: obj) (sessionApi: obj) (sessionId: SessionId) : JS.Promise<obj option> =
    promise {
        if not (Dyn.isNullish sessionApi) then
            let arg = box {| sessionId = SessionId.value sessionId |}
            let! resp = unbox<JS.Promise<obj>> (sessionApi?sessionMessages (arg))
            return Some(Dyn.get resp "data")
        else
            let sm = Dyn.get session "sessionManager"

            if Dyn.isNullish sm then
                return None
            else
                let getEntries = Dyn.get sm "getEntries"

                let raw =
                    if Dyn.typeIs getEntries "function" then
                        Dyn.callMethod0 sm "getEntries"
                    else
                        Dyn.get sm "messages"

                if Dyn.isArray raw then return Some raw else return None
    }

let private checkMessageForTurn (msg: obj) (target: string) : struct (bool * bool) =
    let info = Dyn.get msg "info"

    let found =
        if Dyn.isNullish info then
            false
        else
            let cId1 = Dyn.str info "continuationId"
            let cId2 = Dyn.str info "continuationID"
            cId1 = target || cId2 = target

    let roleTarget =
        if Dyn.str msg "role" <> "" then
            msg
        else
            let m = Dyn.get msg "message"
            if not (Dyn.isNullish m) then m else info

    let isUser =
        if Dyn.isNullish roleTarget then
            false
        else
            (Dyn.str roleTarget "role").ToLowerInvariant() = "user"

    struct (found, isUser)

let private dispatchStatusFromMessages (msgs: obj array) (turnId: TurnId) : DispatchStatus =
    let target = TurnId.value turnId
    let mutable found = false
    let mutable anyUser = false

    for msg in msgs do
        let struct (msgFound, isUser) = checkMessageForTurn msg target

        if msgFound then
            found <- true

        if isUser then
            anyUser <- true

    if found || anyUser then
        DispatchStatus.Accepted OrderedTurnMarkerObserved
    else
        DispatchStatus.Unknown

/// Query whether the turn has been accepted by the host by inspecting the
/// physical session transcript. Used by the OMP subsession host adapter.
let queryDispatchStatus
    (session: obj)
    (sessionApi: obj)
    (sessionId: SessionId)
    (turnId: TurnId)
    : JS.Promise<DispatchStatus> =
    promise {
        try
            let! dataOpt = tryGetSessionMessages session sessionApi sessionId

            match dataOpt with
            | Some data when not (Dyn.isNullish data) && Dyn.isArray data ->
                let msgs = unbox<obj array> data
                return dispatchStatusFromMessages msgs turnId
            | _ -> return DispatchStatus.Unknown
        with _ ->
            return DispatchStatus.Unknown
    }
