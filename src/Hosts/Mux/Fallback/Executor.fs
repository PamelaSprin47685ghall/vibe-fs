module Wanxiangshu.Hosts.Mux.Fallback.Executor

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Dispatch.Identity
open Wanxiangshu.Kernel.Dispatch.Protocol
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Runtime.Dispatch
open Wanxiangshu.Runtime.MuxLogicalReceipt

let private workspaceFor (directory: string) : WorkspaceId =
    if directory = "" then
        Id.workspaceIdQuick "mux-default"
    else
        Id.workspaceIdQuick ("mux:" + directory)

let private loggerFor (_: WorkspaceId) : IDispatchEventLogger =
    InMemoryDispatchEventLogger() :> IDispatchEventLogger

let private invokeNudgeRaw
    (helpers: obj)
    (workspaceId: string)
    (text: string)
    (continuationId: string)
    : JS.Promise<obj> =
    promise {
        if Dyn.isNullish helpers then
            return! Promise.reject (System.Exception("Failed: helpers missing"))
        else
            let nudge = Dyn.get helpers "nudge"

            if Dyn.isNullish nudge then
                return! Promise.reject (System.Exception("Failed: helpers.nudge missing"))
            elif not (Dyn.typeIs nudge "function") then
                return! Promise.reject (System.Exception("Failed: helpers.nudge is not a function"))
            else
                return!
                    (nudge $ (workspaceId, text, null, null, null, continuationId))
                    |> unbox<JS.Promise<obj>>
    }

/// Map host nudge promise resolution to a DispatchAcceptance. Boolean true and
/// empty message ids become AcceptanceUnknown — never HostAccepted.
let private sendPromptFromNudge
    (helpers: obj)
    (sessionID: string)
    (text: string)
    (continuationId: string)
    (_identity: DispatchIdentity)
    : JS.Promise<DispatchAcceptance> =
    promise {
        let! result = invokeNudgeRaw helpers sessionID text continuationId
        let receipt = classify result sessionID continuationId continuationId

        match toDispatchAcceptance receipt with
        | Ok acceptance -> return acceptance
        | Error ex -> return! Promise.reject ex
    }

let private handleMuxDispatchOutcome
    (dispatcher: SessionDispatcher)
    (identity: DispatchIdentity)
    (outcome: DispatchOutcome)
    : JS.Promise<unit> =
    promise {
        match outcome with
        | DispatchOutcome.Accepted acceptance ->
            match acceptance with
            | UserMessageAccepted _
            | RunAccepted _ ->
                let! _ = dispatcher.CompleteByTurn identity.LogicalTurnId
                return ()
            | OpaqueAccepted _ ->
                // Mux never treats opaque acceptance as terminal success.
                let err =
                    { ErrorName = "AcceptanceUnknown"
                      DomainError = None
                      Message = "OpaqueAccepted is not a strong Mux receipt"
                      StatusCode = None
                      IsRetryable = Some true }

                let! _ = dispatcher.FailByTurn identity.LogicalTurnId err
                return! Promise.reject (System.Exception("AcceptanceUnknown: OpaqueAccepted is not a strong Mux receipt"))
        | DispatchOutcome.Failed terminal ->
            match terminal with
            | AcceptanceUnknown e ->
                return! Promise.reject (System.Exception("AcceptanceUnknown: " + e.Message))
            | RejectedBeforeSend e ->
                return! Promise.reject (System.Exception("Failed: " + e.Message))
            | Failed e ->
                return! Promise.reject (System.Exception("Failed: " + e.Message))
            | TransportUnavailable e ->
                return! Promise.reject (System.Exception("Failed: " + e.Message))
            | Cancelled -> return! Promise.reject (System.Exception("Failed: cancelled"))
            | Superseded ->
                return! Promise.reject (System.Exception("Failed: AnotherDispatchInFlight"))
            | SessionClosed -> return! Promise.reject (System.Exception("Failed: session closed"))
            | AbortUnknown e ->
                return! Promise.reject (System.Exception(abortUnavailableMessage + ": " + e.Message))
            | TimedOut e -> return! Promise.reject (System.Exception("Failed: " + e.Message))
            | Poisoned s -> return! Promise.reject (System.Exception("Failed: " + s))
            | Completed -> return ()
    }

let private dispatchMuxPrompt
    (directory: string)
    (helpers: obj)
    (sessionID: string)
    (text: string)
    (continuationId: string)
    : JS.Promise<unit> =
    promise {
        let ws = workspaceFor directory
        let dispatcher = sharedDispatchRegistry.GetOrCreate ws sessionID (loggerFor ws)

        let identity =
            DispatchIdentity.newId
                ws
                sessionID
                DispatchKind.FallbackContinuation
                0
                0
                0
                continuationId
                ""
                ""

        let sendPrompt = sendPromptFromNudge helpers sessionID text continuationId

        let! outcome, _ =
            dispatcher.Dispatch identity sendPrompt System.Threading.CancellationToken.None

        do! handleMuxDispatchOutcome dispatcher identity outcome
    }

let private getChatHistory (helpers: obj) (workspaceId: string) : JS.Promise<obj array> =
    if Dyn.isNullish helpers then
        Promise.lift [||]
    else
        let getter = Dyn.get helpers "getChatHistory"

        if Dyn.isNullish getter then
            Promise.lift [||]
        else
            unbox<JS.Promise<obj array>> (Dyn.call1 getter workspaceId)

let muxActionExecutor (helpers: obj) (directory: string) : IActionExecutor =
    { new IActionExecutor with
        member _.SendContinue(sessionID, model, continuationID) =
            let modelStr =
                match model.Variant with
                | Some v -> sprintf "%s/%s:%s" model.ProviderID model.ModelID v
                | _ -> sprintf "%s/%s" model.ProviderID model.ModelID

            dispatchMuxPrompt directory helpers sessionID ("continue " + modelStr) continuationID

        member _.RecoverWithPrompt(sessionID, model, promptText, continuationID) =
            let modelStr =
                match model.Variant with
                | Some v -> sprintf "%s/%s:%s" model.ProviderID model.ModelID v
                | _ -> sprintf "%s/%s" model.ProviderID model.ModelID

            dispatchMuxPrompt directory helpers sessionID (promptText + " " + modelStr) continuationID

        member _.FetchMessages sessionID = getChatHistory helpers sessionID

        member _.PropagateFailure(_sessionID: string) = Promise.lift ()

        member _.CaptureCurrentModel(_sessionID: string) = Promise.lift None

        // Mux capability degrade: no reliable abort → never promise Cancelled.
        member _.AbortRun(_sessionID: string) =
            Promise.reject (abortUnavailableException ()) }

/// Backward-compatible overload used by pure unit tests without a workspace root.
let muxActionExecutorDefault (helpers: obj) : IActionExecutor = muxActionExecutor helpers ""
