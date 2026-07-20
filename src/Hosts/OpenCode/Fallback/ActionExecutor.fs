module Wanxiangshu.Hosts.Opencode.Fallback.ActionExecutor

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.Messaging.OpencodeSessionEventCodec
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.FallbackMessageCodec
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Fallback.Continuation
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Hosts.Opencode.Fallback.HostEventInspection
open Wanxiangshu.Hosts.Opencode.Fallback.ContinuationPromptBuilder
open Wanxiangshu.Runtime.Dispatch
open Wanxiangshu.Kernel.Dispatch.Identity
open Wanxiangshu.Kernel.Dispatch.Protocol

let private fetchMessagesImpl (client: obj) (sessionID: string) : JS.Promise<obj array> =
    promise {
        let arg = box {| path = box {| id = sessionID |} |}
        let! resp = invokeClient client "messages" arg
        let data = Dyn.get resp "data"

        if Dyn.isArray data then
            return (data :?> obj array)
        else
            return [||]
    }

let private workspaceFor (directory: string) : WorkspaceId =
    if directory = "" then
        Id.workspaceIdQuick "opencode-default"
    else
        Id.workspaceIdQuick ("opencode:" + directory)

let private loggerFor (_: WorkspaceId) : IDispatchEventLogger =
    Wanxiangshu.Runtime.Dispatch.InMemoryDispatchEventLogger() :> IDispatchEventLogger

/// Receipt timeout for OpenCode `chat.message` arrival on a continuation
/// dispatch. Overridable via environment for tests.
[<Emit("(() => { const v = typeof process !== 'undefined' && process.env && process.env.WANXIANGSHU_OPENCODE_RECEIPT_TIMEOUT_MS; const n = v ? parseInt(v, 10) : 30000; return isNaN(n) ? 30000 : n; })()")>]
let private receiptTimeoutMs () : int = jsNative

let private handleContinuationResult
    (dispatcher: SessionDispatcher)
    (identity: DispatchIdentity)
    (outcome: DispatchOutcome)
    (receiptWaiterOpt: HostReceiptWaiter option)
    : JS.Promise<unit> =
    promise {
        match receiptWaiterOpt with
        | Some waiter ->
            let timeoutErr =
                { ErrorName = "OpencodeReceiptTimeout"
                  DomainError = None
                  Message = "Timed out waiting for OpenCode chat.message receipt for continuation"
                  StatusCode = None
                  IsRetryable = Some false }

            let! result = HostReceiptWaiter.awaitWithTimeout waiter (receiptTimeoutMs ()) timeoutErr

            match result with
            | Ok _ ->
                // Real host receipt observed; mark the dispatcher slot complete.
                let! _ = dispatcher.CompleteByTurn identity.LogicalTurnId
                ()
            | Error failure ->
                // Receipt failed or timed out; fail the dispatcher turn so the
                // runtime does not keep an orphaned active dispatch.
                let errInput =
                    match failure with
                    | HostRejected e -> e
                    | HostAcceptanceUnknown e -> e

                let! _ = dispatcher.FailByTurn identity.LogicalTurnId errInput
                return raise (System.Exception(sprintf "Fallback continuation dispatch failed: %A" failure))
        | None ->
            match outcome with
            | DispatchOutcome.Accepted _ -> ()
            | DispatchOutcome.Failed terminal ->
                return raise (System.Exception(sprintf "Fallback continuation dispatch failed: %A" terminal))
    }

let private dispatchFallbackContinuation
    (runtime: FallbackRuntimeStore)
    (client: obj)
    (directory: string)
    (sessionID: string)
    (model: FallbackModel)
    (promptText: string)
    (mode: ContinuationMode)
    (continuationID: string)
    : JS.Promise<unit> =
    promise {
        let ws = workspaceFor directory
        let dispatcher = DispatchRegistryInstance.sharedDispatchRegistry.GetOrCreate ws sessionID (loggerFor ws)

        let lease = (runtime.GetSession sessionID).PendingLease
        let runGen = match lease with Some l -> l.SessionGeneration | None -> 0
        let cancelGen = match lease with Some l -> l.CancelGeneration | None -> 0
        let humanTurnId = match lease with Some l -> l.HumanTurnID | None -> ""

        let identity =
            DispatchIdentity.newId
                ws
                sessionID
                DispatchKind.FallbackContinuation
                runGen
                cancelGen
                0
                continuationID
                humanTurnId
                ""

        let sendPrompt = buildContinuationPrompt runtime client sessionID model promptText mode continuationID

        let! outcome, receiptWaiterOpt =
            dispatcher.Dispatch identity sendPrompt System.Threading.CancellationToken.None

        do! handleContinuationResult dispatcher identity outcome receiptWaiterOpt
    }

let private sendContinueImpl
    (runtime: FallbackRuntimeStore)
    (client: obj)
    (directory: string)
    (sessionID: string)
    (model: FallbackModel)
    (continuationID: string)
    : JS.Promise<unit> =
    dispatchFallbackContinuation
        runtime
        client
        directory
        sessionID
        model
        "\u200B"
        ContinuationMode.ResumeInterruptedTurn
        continuationID

let private captureCurrentModelImpl
    (runtime: FallbackRuntimeStore)
    (client: obj)
    (sessionID: string)
    : JS.Promise<FallbackModel option> =
    promise {
        let! msgs = fetchMessagesImpl client sessionID

        match Wanxiangshu.Runtime.Fallback.FallbackMessageCodec.tryGetLatestUserModel msgs with
        | Some m -> return Some m
        | None ->
            match (runtime.GetSession sessionID).Model with
            | Some m -> return Some m
            | None -> return! tryReadCurrentModel client sessionID
    }

let private recoverWithPromptImpl
    (runtime: FallbackRuntimeStore)
    (client: obj)
    (directory: string)
    (sessionID: string)
    (model: FallbackModel)
    (promptText: string)
    (continuationID: string)
    : JS.Promise<unit> =
    dispatchFallbackContinuation
        runtime
        client
        directory
        sessionID
        model
        promptText
        (ContinuationMode.RecoverToolCallText promptText)
        continuationID

let private abortRunImpl (client: obj) (sessionID: string) : JS.Promise<unit> =
    promise {
        let arg = box {| path = box {| id = sessionID |} |}
        do! invokeClient client "abort" arg |> Promise.map ignore
    }

let opencodeActionExecutorWithDir (runtime: FallbackRuntimeStore) (client: obj) (directory: string) : IActionExecutor =
    { new IActionExecutor with
        member _.SendContinue(sessionID, model, continuationID) =
            sendContinueImpl runtime client directory sessionID model continuationID

        member _.FetchMessages sessionID = fetchMessagesImpl client sessionID
        member _.PropagateFailure _sessionID = Promise.lift ()

        member _.CaptureCurrentModel sessionID =
            captureCurrentModelImpl runtime client sessionID

        member _.RecoverWithPrompt(sessionID, model, promptText, continuationID) =
            recoverWithPromptImpl runtime client directory sessionID model promptText continuationID

        member _.AbortRun sessionID = abortRunImpl client sessionID }

let opencodeActionExecutor (runtime: FallbackRuntimeStore) (client: obj) : IActionExecutor =
    opencodeActionExecutorWithDir runtime client ""
