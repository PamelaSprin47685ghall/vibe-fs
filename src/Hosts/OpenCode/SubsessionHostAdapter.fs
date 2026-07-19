module Wanxiangshu.Hosts.Opencode.SubsessionHostAdapter

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Dispatch.Identity
open Wanxiangshu.Kernel.Dispatch.Protocol
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dispatch
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Hosts.OpenCode.OpencodeSessionEventCodec
open Wanxiangshu.Runtime.CommandProcessor
open Wanxiangshu.Runtime.SubsessionPorts
open Wanxiangshu.Runtime.SubsessionActor
open Wanxiangshu.Runtime.SubsessionTranscript
open Wanxiangshu.Runtime.SubsessionActorRegistry
open Wanxiangshu.Hosts.Opencode.SubsessionDispatch
open Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterTypes

/// Re-export so existing tests (`OpopenSubsessionHostAdapterModelTests`)
/// can call `buildDispatchModelString` without knowing the inner module split.
let buildDispatchModelString =
    Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterTypes.buildDispatchModelString

/// OpenCode subagent host. Every prompt and abort goes through the per-session
/// `DispatchRegistry` so two requests on the same physical session cannot
/// race, and so the caller receives a true `HostStartReceipt` (or a typed
/// failure) instead of a `Promise.resolve = success` lie.
type OpencodeSubsessionHost(client: obj, agent: string, directory: string) =
    let workspace =
        Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterTypes.workspaceFor directory

    interface ISubsessionHost with
        member _.Dispatch(sessionId, turn) =
            let dispatcher: SessionDispatcher =
                Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterTypes.getDispatcher
                    directory
                    (SessionId.value sessionId)

            let identity: Wanxiangshu.Kernel.Dispatch.Identity.DispatchIdentity =
                Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterTypes.encodeDispatchIdentity
                    directory
                    (SessionId.value sessionId)
                    turn.TurnId
                    turn.Model
                    turn.Prompt

            let sendPrompt =
                Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterOps.buildSendPrompt
                    client
                    agent
                    directory
                    sessionId
                    turn

            /// Register a PendingTurnReceipt waiter BEFORE dispatching so the
            /// actor can await the real host user-message identity rather than
            /// the dispatcher's internal waiter (which resolves too early).
            let pendingReceipt =
                PendingTurnReceipt.register directory (SessionId.value sessionId) (TurnId.value turn.TurnId)

            promise {
                // Step 1: await the dispatcher slot.  If it fails (e.g.
                // RejectedBeforeSend because another dispatch is in flight),
                // mark the receipt as transport-rejected so the waiter does
                // not hang.
                let! dispatchOutcome = dispatcher.Dispatch identity sendPrompt (System.Threading.CancellationToken.None)

                match dispatchOutcome with
                | DispatchOutcome.Failed terminal ->
                    let turnId = TurnId.value turn.TurnId

                    // A host-side hook (e.g. chat.message) may have already
                    // resolved the receipt while the prompt promise was in
                    // flight.  Trust that resolution instead of failing.
                    let alreadyResolved =
                        match PendingTurnReceipt.tryFind turnId with
                        | None -> true
                        | Some w when w.Completed -> true
                        | Some _ -> false

                    if alreadyResolved then
                        let! receiptResult = pendingReceipt
                        return receiptResult
                    else
                        let err =
                            match terminal with
                            | RejectedBeforeSend e
                            | TransportUnavailable e ->
                                { ErrorName = e.ErrorName
                                  DomainError = e.DomainError
                                  Message = e.Message
                                  StatusCode = e.StatusCode
                                  IsRetryable = e.IsRetryable }
                            | DispatchTerminal.Failed e
                            | DispatchTerminal.AcceptanceUnknown e
                            | DispatchTerminal.AbortUnknown e
                            | DispatchTerminal.TimedOut e ->
                                { ErrorName = e.ErrorName
                                  DomainError = e.DomainError
                                  Message = e.Message
                                  StatusCode = e.StatusCode
                                  IsRetryable = e.IsRetryable }
                            | _ ->
                                { ErrorName = "DispatchFailed"
                                  DomainError = None
                                  Message =
                                    "dispatch terminal: "
                                    + Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterTypes.toStringTerminal terminal
                                  StatusCode = None
                                  IsRetryable = Some false }

                        match terminal with
                        | RejectedBeforeSend _
                        | TransportUnavailable _ ->
                            PendingTurnReceipt.markTransportRejected turnId err
                            return Error(HostRejected err)
                        | _ ->
                            PendingTurnReceipt.markTransportFailed turnId err
                            return Error(HostAcceptanceUnknown err)

                | DispatchOutcome.Accepted _ ->
                    // Step 2: dispatcher accepted — now await the real host
                    // user-message identity from PendingTurnReceipt (resolved
                    // by ChatHooks on chat.message, or by sendPrompt above for
                    // OpaqueAccepted).
                    let! receiptResult = pendingReceipt

                    match receiptResult with
                    | Ok hostReceipt -> return Ok hostReceipt
                    | Error(HostRejected err) ->
                        let err2 =
                            { ErrorName = err.ErrorName
                              DomainError = err.DomainError
                              Message = err.Message
                              StatusCode = err.StatusCode
                              IsRetryable = err.IsRetryable }

                        return Error(HostRejected err2)
                    | Error(HostAcceptanceUnknown err) ->
                        let err2 =
                            { ErrorName = err.ErrorName
                              DomainError = err.DomainError
                              Message = err.Message
                              StatusCode = err.StatusCode
                              IsRetryable = err.IsRetryable }

                        return Error(HostRejected err2)
            }

        member _.Abort(sessionId, turnId) =
            promise {
                let dispatcher: SessionDispatcher =
                    Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterTypes.getDispatcher
                        directory
                        (SessionId.value sessionId)

                let physicalAbort () : JS.Promise<bool> =
                    promise {
                        match Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterTypes.trySessionApi client with
                        | Ok session ->
                            let arg = box {| path = box {| id = SessionId.value sessionId |} |}
                            let! _ = Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterTypes.invoke1 arg "abort" session
                            return true
                        | Error _ -> return false
                    }

                let! result = dispatcher.CancelByTurn (TurnId.value turnId) physicalAbort

                match result with
                | AbortSent
                | CancelledBeforeAcceptance -> return Wanxiangshu.Kernel.Subsession.Types.RequestAcceptedAwaitIdle
                | AbortUnavailable -> return Wanxiangshu.Kernel.Subsession.Types.AbortUnavailable
                | AlreadyTerminal _ -> return Wanxiangshu.Kernel.Subsession.Types.ConfirmedStopped
            }

        member _.CancelPendingDispatch(turnId) =
            let nonce = TurnId.value turnId
            Wanxiangshu.Hosts.Opencode.SubsessionDispatch.PendingTurnReceipt.cancel nonce

        member _.QueryDispatchStatus(sessionId, turnId) =
            Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterOps.buildQueryDispatchStatus
                client
                directory
                sessionId
                turnId
                ()

        member this.QuerySessionQuiescence(sessionId, turnId) =
            Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterOps.buildQuerySessionQuiescence client sessionId turnId ()

        member _.ClosePhysicalSession(sessionId) =
            promise {
                let dispatcher: SessionDispatcher =
                    Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterTypes.getDispatcher
                        directory
                        (SessionId.value sessionId)

                dispatcher.OnSessionClosed()

                match Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterTypes.trySessionApi client with
                | Ok session ->
                    try
                        let arg = box {| path = box {| id = SessionId.value sessionId |} |}
                        let! _ = Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterTypes.invoke1 arg "delete" session
                        return Stopped
                    with _ ->
                        return StopUnknown
                | Error _ -> return StopUnknown
            }


/// Factory: construct a new OpenCode subsession host backed by the given
/// client.  All callers (SubagentRunExec, PluginServiceLoader, tests) go
/// through this function — they never call the ctor directly.
let createHost (client: obj) (agent: string) (directory: string) : ISubsessionHost =
    OpencodeSubsessionHost(client, agent, directory) :> ISubsessionHost

/// Bind a host-side user message to an in-flight logical turn so the
/// dispatcher can prove round-trip attribution.
let bindHostUserMessage (directory: string) (sessionId: string) (logicalTurnId: string) (messageId: string) : unit =
    let dispatcher =
        Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterTypes.getDispatcher directory sessionId

    Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterTypes.bindHostUserMessage dispatcher logicalTurnId messageId
