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
open Wanxiangshu.Runtime.Messaging.OpencodeSessionEventCodec
open Wanxiangshu.Runtime.CommandProcessor
open Wanxiangshu.Runtime.SubsessionPorts
open Wanxiangshu.Runtime.SubsessionActor
open Wanxiangshu.Runtime.SubsessionTranscript
open Wanxiangshu.Runtime.SubsessionActorRegistry
open Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterTypes

/// Re-export so existing tests (`OpopenSubsessionHostAdapterModelTests`)
/// can call `buildDispatchModelString` without knowing the inner module split.
let buildDispatchModelString =
    Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterTypes.buildDispatchModelString

/// OpenCode subagent host. Every prompt and abort goes through the per-session
/// `SessionDispatcher` so two requests on the same physical session cannot
/// race, and so the caller receives a true `HostStartReceipt` (or a typed
/// failure) instead of a `Promise.resolve = success` lie.
type OpencodeSubsessionHost(client: obj, agent: string, directory: string) =
    interface ISubsessionHost with
        member _.Dispatch(sessionId, turn) =
            let dispatcher = getDispatcher directory (SessionId.value sessionId)

            let identity =
                encodeDispatchIdentity directory (SessionId.value sessionId) turn.TurnId turn.Model turn.Prompt

            let sendPrompt =
                SubsessionHostAdapterOps.buildSendPrompt client agent directory sessionId turn

            promise {
                let! outcome, receiptWaiterOpt =
                    dispatcher.Dispatch identity sendPrompt System.Threading.CancellationToken.None

                match receiptWaiterOpt with
                | Some waiter ->
                    // Wait for the real host user-message identity.
                    let! result = waiter.Promise

                    match result with
                    | Ok receipt -> return Ok receipt
                    | Error failure -> return Error failure
                | None ->
                    // Dispatcher failed before creating the waiter
                    // (e.g. identity mismatch).
                    match outcome with
                    | DispatchOutcome.Accepted a -> return Ok(HostReceiptWaiter.dispatchAcceptanceToHostReceipt a)
                    | DispatchOutcome.Failed terminal ->
                        return Error(HostReceiptWaiter.dispatchFailureOfTerminal terminal)
            }

        member _.Abort(sessionId, turnId) =
            promise {
                let dispatcher = getDispatcher directory (SessionId.value sessionId)

                let physicalAbort () : JS.Promise<bool> =
                    promise {
                        match trySessionApi client with
                        | Ok session ->
                            let arg = box {| path = box {| id = SessionId.value sessionId |} |}
                            let! _ = invoke1 arg "abort" session
                            return true
                        | Error _ -> return false
                    }

                let! result = dispatcher.CancelByTurn (TurnId.value turnId) physicalAbort

                match result with
                | DispatchCancelResult.AbortSent
                | DispatchCancelResult.CancelledBeforeAcceptance ->
                    return (Wanxiangshu.Kernel.Subsession.Types.AbortResult.RequestAcceptedAwaitIdle)
                | DispatchCancelResult.AbortUnavailable ->
                    return (Wanxiangshu.Kernel.Subsession.Types.AbortResult.AbortUnavailable)
                | DispatchCancelResult.AlreadyTerminal _ ->
                    return (Wanxiangshu.Kernel.Subsession.Types.AbortResult.ConfirmedStopped)
            }

        member _.CancelPendingDispatch(turnId) =
            HostReceiptWaiterRegistry.cancelByTurn (workspaceFor directory) (TurnId.value turnId)

        member _.QueryDispatchStatus(sessionId, turnId) =
            SubsessionHostAdapterOps.buildQueryDispatchStatus client directory sessionId turnId ()

        member this.QuerySessionQuiescence(sessionId, turnId) =
            SubsessionHostAdapterOps.buildQuerySessionQuiescence client sessionId turnId ()

        member _.ClosePhysicalSession(sessionId) =
            promise {
                let sid = SessionId.value sessionId
                let dispatcher = getDispatcher directory sid
                dispatcher.OnSessionClosed()

                HostReceiptWaiterRegistry.removeSession (workspaceFor directory) sid

                try
                    do! SubsessionHostAdapterOps.deleteSession client directory sid
                    return Stopped
                with _ ->
                    return StopUnknown
            }


/// Factory: construct a new OpenCode subsession host backed by the given
/// client.  All callers (SubagentRunExec, PluginServiceLoader, tests) go
/// through this function — they never call the ctor directly.
let createHost (client: obj) (agent: string) (directory: string) : ISubsessionHost =
    OpencodeSubsessionHost(client, agent, directory) :> ISubsessionHost

/// Bind a host-side user message to an in-flight logical turn so the
/// dispatcher can prove round-trip attribution.
let bindHostUserMessage (directory: string) (sessionId: string) (logicalTurnId: string) (messageId: string) : unit =
    let dispatcher = getDispatcher directory sessionId
    bindHostUserMessage dispatcher logicalTurnId messageId
