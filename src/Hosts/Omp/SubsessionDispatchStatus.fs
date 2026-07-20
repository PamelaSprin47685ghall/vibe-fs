module Wanxiangshu.Hosts.Omp.SubsessionDispatchStatus
open Wanxiangshu.Runtime

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime.Dyn

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

let private checkMessageForTurn (msg: obj) (target: string) : struct (bool * string option) =
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

    let msgId =
        if found || isUser then
            Dyn.str msg "id"
        else
            ""

    struct (found, if msgId <> "" then Some msgId else None)

let private dispatchStatusFromMessages (msgs: obj array) (turnId: TurnId) : DispatchStatus =
    let target = TurnId.value turnId
    let mutable accepted = false
    let mutable receipt = None

    for msg in msgs do
        let struct (msgFound, msgIdOpt) = checkMessageForTurn msg target

        if msgFound then
            accepted <- true
            receipt <- msgIdOpt
        elif msgIdOpt.IsSome then
            accepted <- true
            if receipt.IsNone then receipt <- msgIdOpt

    if accepted then
        match receipt with
        | Some id -> DispatchStatus.Accepted(UserMessageObserved id)
        | None -> DispatchStatus.Accepted OrderedTurnMarkerObserved
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
