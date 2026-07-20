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
                    let lastAssistantIdOpt = tryGetLastAssistantMessageId msgs
                    let lastAssistantId = lastAssistantIdOpt |> Option.defaultValue ""

                    runtime.Update(sessionID, setLastAssistantMessageId lastAssistantId)

                    // Suppress duplicate empty-output idle events for the same assistant
                    // message while a continuation is in flight; this prevents stale
                    // FetchMessages snapshots from triggering extra retries.
                    if
                        isIdleNoContentAndNoTools msgs
                        && lastAssistantId <> ""
                        && lastAssistantId = state.LastAssistantMessageId
                    then
                        return None
                    else
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
    // Busy is only RunObserved once HostUserMessageId is bound (HostAccepted).
    (runtime.GetSession sessionID).PendingLease
    |> Option.filter (fun l ->
<<<<<<< HEAD
        l.HostUserMessageId <> ""
        && (l.Status = LeaseStatus.Dispatched || l.Status = LeaseStatus.Running))
    |> Option.filter (fun l -> l.Status = LeaseStatus.Dispatched)
=======
        l.Status = LeaseStatus.DispatchStarted
        || l.Status = LeaseStatus.AcceptanceUnknown
        || l.Status = LeaseStatus.Dispatched)
>>>>>>> 98bc01f6 (fix(mux): wire AcceptanceUnknown/AbortUnknown degrade paths end-to-end)
    |> Option.iter (fun l -> runtime.UpdateSession(sessionID, setPendingLease { l with Status = LeaseStatus.Running }))

    (runtime.GetSession sessionID).PendingNudgeLease
    |> Option.filter (fun l ->
<<<<<<< HEAD
        l.HostUserMessageId <> ""
        && l.Status = LeaseStatus.Dispatched)
=======
        l.Status = LeaseStatus.DispatchStarted
        || l.Status = LeaseStatus.AcceptanceUnknown
        || l.Status = LeaseStatus.Dispatched)
>>>>>>> 98bc01f6 (fix(mux): wire AcceptanceUnknown/AbortUnknown degrade paths end-to-end)
    |> Option.iter (fun l ->
        runtime.UpdateSession(sessionID, setPendingNudgeLease { l with Status = LeaseStatus.Running }))
