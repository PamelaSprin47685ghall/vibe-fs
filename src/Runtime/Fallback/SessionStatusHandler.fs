module Wanxiangshu.Runtime.Fallback.SessionStatusHandler

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.Fallback.LeaseValidation
open Wanxiangshu.Runtime.Fallback.NudgeHandler
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Runtime.Fallback.FallbackMessageCodec
open Wanxiangshu.Runtime.Fallback.FallbackMessageDetection
open Wanxiangshu.Runtime.SessionEventWriter

let emptyOutputError =
    FallbackEvent.SessionError
        { ErrorName = "EmptyOutputError"
          DomainError = None
          Message = "LLM returned empty output without tools"
          StatusCode = None
          IsRetryable = Some true }

let translateEvent
    (translator: IEventTranslator)
    (executor: IActionExecutor)
    (runtime: FallbackRuntimeStore)
    (sessionID: string)
    (rawEvent: obj)
    (pendingReview: (string -> bool) option)
    : JS.Promise<FallbackEvent option> =
    match translator.TranslateError rawEvent with
    | Some _ as ev -> promise { return ev }
    | None ->
        if translator.IsNewUserMessage(sessionID, rawEvent) then
            promise { return Some FallbackEvent.NewUserMessage }
        elif translator.IsSessionBusy rawEvent then
            promise { return Some FallbackEvent.SessionBusy }
        elif translator.IsSessionIdle rawEvent then
            promise {
                let state = runtime.GetOrCreateState sessionID

                if state.Lifecycle = FallbackLifecycle.Cancelled then
                    return Some FallbackEvent.SessionIdle
                else
                    let! msgs = executor.FetchMessages sessionID

                    match tryGetLastAssistantAbortInfo msgs with
                    | Some abortErr -> return Some(FallbackEvent.SessionError abortErr)
                    | None ->
                        if
                            isIdleNoContentAndNoTools msgs
                            && not (pendingReview |> Option.exists (fun f -> f sessionID))
                        then
                            return Some emptyOutputError
                        else
                            return Some FallbackEvent.SessionIdle
            }
        else
            promise { return None }

let handleUserAbort (runtime: FallbackRuntimeStore) (workspaceRoot: string) (sessionID: string) : JS.Promise<unit> =
    promise {
        do! appendUserAbortObservedOrFail workspaceRoot sessionID
        let _ = runtime.UpdateSessionReturning(sessionID, incrementCancelGeneration)
        do! cancelPendingMainLease runtime workspaceRoot sessionID "User aborted"
        do! cancelPendingNudge runtime workspaceRoot sessionID "User aborted"
    }

let updateBusyLeases (runtime: FallbackRuntimeStore) (sessionID: string) : unit =
    let markRunning (lease: PendingLease) =
        { lease with
            Status = LeaseStatus.Running }

    (runtime.GetSession sessionID).PendingLease
    |> Option.filter (fun l -> l.Status = LeaseStatus.DispatchStarted || l.Status = LeaseStatus.Dispatched)
    |> Option.iter (fun l -> runtime.UpdateSession(sessionID, setPendingLease { l with Status = LeaseStatus.Running }))

    (runtime.GetSession sessionID).PendingNudgeLease
    |> Option.filter (fun l -> l.Status = LeaseStatus.DispatchStarted || l.Status = LeaseStatus.Dispatched)
    |> Option.iter (fun l ->
        runtime.UpdateSession(sessionID, setPendingNudgeLease { l with Status = LeaseStatus.Running }))
