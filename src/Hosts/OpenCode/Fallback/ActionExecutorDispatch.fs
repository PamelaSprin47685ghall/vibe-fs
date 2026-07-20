module Wanxiangshu.Hosts.Opencode.Fallback.ActionExecutorDispatch
open Wanxiangshu.Runtime

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Fallback.Continuation
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Hosts.Opencode.Fallback.ContinuationPromptBuilder
open Wanxiangshu.Runtime.Dispatch
open Wanxiangshu.Kernel.Dispatch.Identity
open Wanxiangshu.Kernel.Dispatch.Protocol
open Wanxiangshu.Runtime.Fallback.ContinuationDispatchOps
open Wanxiangshu.Runtime.Fallback.ContinuationDispatchRegistry

let fetchMessagesImpl (client: obj) (sessionID: string) : JS.Promise<obj array> =
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

[<Emit("(() => { const v = typeof process !== 'undefined' && process.env && process.env.WANXIANGSHU_OPENCODE_RECEIPT_TIMEOUT_MS; const n = v ? parseInt(v, 10) : 30000; return isNaN(n) ? 30000 : n; })()")>]
let private receiptTimeoutMs () : int = jsNative

let private handleContinuationResult
    (runtime: FallbackRuntimeStore)
    (workspaceRoot: string)
    (sessionID: string)
    (continuationID: string)
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
                // Host evidence only: receipt observed → Dispatched. Never prompt().
                let! _ = recordHostAcceptedContinuation runtime workspaceRoot sessionID continuationID
                let! _ = dispatcher.CompleteByTurn identity.LogicalTurnId
                ()
            | Error failure ->
                let errInput =
                    match failure with
                    | HostRejected e -> e
                    | HostAcceptanceUnknown e -> e

                let! _ = dispatcher.FailByTurn identity.LogicalTurnId errInput
                return raise (System.Exception(sprintf "Fallback continuation dispatch failed: %A" failure))
        | None ->
            match outcome with
            | DispatchOutcome.Accepted _ ->
                // Opaque accept without receipt waiter is not host evidence for OpenCode.
                ()
            | DispatchOutcome.Failed terminal ->
                return raise (System.Exception(sprintf "Fallback continuation dispatch failed: %A" terminal))
    }

let dispatchFallbackContinuation
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

        do!
            handleContinuationResult
                runtime
                directory
                sessionID
                continuationID
                dispatcher
                identity
                outcome
                receiptWaiterOpt
    }