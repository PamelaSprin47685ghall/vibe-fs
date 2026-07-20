module Wanxiangshu.Hosts.Omp.SubsessionDispatch
open Wanxiangshu.Runtime

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Hosts.Omp.SubsessionQuiescence
open Wanxiangshu.Hosts.Omp.SubsessionDispatchStatus

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

let detectStatus = SubsessionQuiescence.detectStatus
let queryDispatchStatus = SubsessionDispatchStatus.queryDispatchStatus
