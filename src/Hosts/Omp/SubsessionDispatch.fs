module Wanxiangshu.Hosts.Omp.SubsessionDispatch

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn

let resolveModelString (modelOpt: FallbackModel option) : string =
    match modelOpt with
    | Some model ->
        match model.Variant with
        | Some v -> sprintf "%s/%s:%s" model.ProviderID model.ModelID v
        | None -> sprintf "%s/%s" model.ProviderID model.ModelID
    | None -> ""

let preparePromptArg (agent: string) (sessionId: SessionId) (turn: TurnPlan) : obj =
    let modelStr = resolveModelString turn.Model
    let turnIdStr = TurnId.value turn.TurnId

    let pObj =
        let p =
            {| text = turn.Prompt
               model = modelStr
               continuationID = turnIdStr |}

        if agent <> "" then Dyn.withKey p "agent" agent else box p

    let body = box {| prompt = pObj |}

    box
        {| sessionId = SessionId.value sessionId
           body = body |}

let dispatch
    (session: obj)
    (agent: string)
    (sessionId: SessionId)
    (turn: TurnPlan)
    : JS.Promise<Result<HostStartReceipt, DispatchFailure>> =
    promise {
        let arg = preparePromptArg agent sessionId turn

        try
            let! resp = unbox<JS.Promise<obj>> (session?prompt (arg))

            let msgId =
                let id1 = Dyn.str resp "id"
                let id2 = Dyn.str (Dyn.get resp "data") "id"

                if id1 <> "" then id1
                elif id2 <> "" then id2
                else ""

            if msgId <> "" then
                return Ok(UserMessageObserved msgId)
            else
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
