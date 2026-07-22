module Wanxiangshu.Hosts.Omp.SubsessionDispatch

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OmpHostBindings

let private acceptanceUnknown (name: string) (message: string) : DispatchFailure =
    DispatchFailure.HostAcceptanceUnknown
        { ErrorName = name
          DomainError = None
          Message = message
          StatusCode = None
          IsRetryable = Some true }

let private hostRejected (name: string) (message: string) (retryable: bool) : DispatchFailure =
    DispatchFailure.HostRejected
        { ErrorName = name
          DomainError = None
          Message = message
          StatusCode = None
          IsRetryable = Some retryable }

let private executePrompt (session: obj) (turn: TurnPlan) : JS.Promise<obj> =
    let modelOpt =
        turn.Model
        |> Option.bind (fun m -> formatModelString m.ProviderID m.ModelID m.Variant)

    match modelOpt with
    | None -> sessionPrompt session turn.Prompt
    | Some modelStr ->
        let body = buildSessionPromptBody turn.Prompt (Some modelStr) None None
        unbox<JS.Promise<obj>> (session?prompt (body))

/// Prompt resolve alone is NOT acceptance (SPEC §4.5). Only UserMessageObserved id → Ok.
let dispatch
    (session: obj)
    (_agent: string)
    (_sessionId: SessionId)
    (turn: TurnPlan)
    : JS.Promise<Result<HostStartReceipt, DispatchFailure>> =
    promise {
        let promptFn = Dyn.get session "prompt"

        if Dyn.isNullish session then
            return Error(hostRejected "HostUnavailable" "OMP host session object is nullish" false)
        elif Dyn.isNullish promptFn || not (Dyn.typeIs promptFn "function") then
            return Error(hostRejected "PromptUnavailable" "OMP host session does not support prompt API" false)
        else
            try
                let! response = executePrompt session turn

                match tryExtractMessageId response with
                | Some mid -> return Ok(UserMessageObserved mid)
                | None ->
                    return
                        Error(
                            acceptanceUnknown
                                "OmpPromptNoMessageId"
                                "OMP session.prompt resolved without a verifiable message id; ordered accept is not assumed"
                        )
            with ex ->
                return Error(acceptanceUnknown "DispatchFailed" ex.Message)
    }
