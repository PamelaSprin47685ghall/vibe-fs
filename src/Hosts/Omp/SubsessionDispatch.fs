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
