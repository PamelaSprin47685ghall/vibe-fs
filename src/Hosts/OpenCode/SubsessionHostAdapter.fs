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
open Wanxiangshu.Runtime.OpencodeSessionEventCodec
open Wanxiangshu.Runtime.CommandProcessor
open Wanxiangshu.Runtime.SubsessionPorts
open Wanxiangshu.Runtime.SubsessionActor
open Wanxiangshu.Runtime.SubsessionTranscript
open Wanxiangshu.Runtime.SubsessionActorRegistry
open Wanxiangshu.Hosts.Opencode.SubsessionDispatch
open Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterTypes

/// Re-export so existing tests (`OpencodeSubsessionHostAdapterModelTests`)
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

            let sendPrompt (_: DispatchIdentity) : JS.Promise<DispatchAcceptance> =
                promise {
                    match Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterTypes.trySessionApi client with
                    | Error msg -> return! Promise.reject (System.Exception("opencode_session_api_missing:" + msg))
                    | Ok session ->
                        let body =
                            Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterTypes.buildBody
                                agent
                                turn.Model
                                turn.Prompt
                                turn.TurnId

                        let arg =
                            box
                                {| path = box {| id = SessionId.value sessionId |}
                                   body = body |}

                        let! response =
                            Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterTypes.invoke1 arg "prompt" session

                        let mid =
                            Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterTypes.decodeResponseForUserMessageId
                                response

                        match mid with
                        | Some id when id <> "" -> return UserMessageAccepted id
                        | _ -> return OpaqueAccepted("opencode:" + TurnId.value turn.TurnId)
                }

            promise {
                let! outcome = dispatcher.Dispatch identity sendPrompt (System.Threading.CancellationToken.None)

                match outcome with
                | DispatchOutcome.Accepted receipt ->
                    let hostReceipt =
                        match receipt with
                        | UserMessageAccepted id -> HostStartReceipt.UserMessageObserved id
                        | RunAccepted id -> HostStartReceipt.UserMessageObserved id
                        | OpaqueAccepted _ -> HostStartReceipt.OrderedTurnMarkerObserved

                    return Ok hostReceipt
                | DispatchOutcome.Failed terminal ->
                    let err =
                        { ErrorName = "DispatchFailed"
                          DomainError = None
                          Message =
                            "dispatch terminal: "
                            + Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterTypes.toStringTerminal terminal
                          StatusCode = None
                          IsRetryable = Some false }

                    return Error(HostRejected err)
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
            ignore (Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterTypes.subsessionRegistry, nonce)

        member _.QueryDispatchStatus(sessionId, turnId) =
            promise {
                let dispatcher: SessionDispatcher =
                    Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterTypes.getDispatcher
                        directory
                        (SessionId.value sessionId)

                let nonce = TurnId.value turnId

                match dispatcher.ActiveLogicalTurnId with
                | Some active when active = nonce ->
                    match Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterTypes.trySessionApi client with
                    | Ok session ->
                        try
                            let! resp =
                                Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterTypes.invoke1
                                    (box {| path = box {| id = SessionId.value sessionId |} |})
                                    "messages"
                                    session

                            let data = Wanxiangshu.Runtime.Dyn.get resp "data"

                            if
                                Wanxiangshu.Runtime.Dyn.isNullish data
                                || not (Wanxiangshu.Runtime.Dyn.isArray data)
                            then
                                return Wanxiangshu.Kernel.Subsession.Types.StillPending
                            else
                                let msgs = unbox<obj array> data
                                let found = msgs |> Array.exists (SubsessionDispatch.isMessageMatch nonce)

                                if found then
                                    let mid =
                                        msgs
                                        |> Array.tryFind (SubsessionDispatch.isMessageMatch nonce)
                                        |> Option.map (fun m -> Wanxiangshu.Runtime.Dyn.str m "id")
                                        |> Option.defaultValue ""

                                    return Wanxiangshu.Kernel.Subsession.Types.Accepted(UserMessageObserved mid)
                                else
                                    return Wanxiangshu.Kernel.Subsession.Types.StillPending
                        with _ ->
                            return Wanxiangshu.Kernel.Subsession.Types.StillPending
                    | Error _ -> return Wanxiangshu.Kernel.Subsession.Types.StillPending
                | _ -> return Wanxiangshu.Kernel.Subsession.Types.Unknown
            }

        member this.QuerySessionQuiescence(sessionId, turnId) =
            promise {
                let nonce = TurnId.value turnId

                match Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterTypes.trySessionApi client with
                | Ok session ->
                    try
                        let! resp =
                            Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterTypes.invoke1
                                (box {| path = box {| id = SessionId.value sessionId |} |})
                                "messages"
                                session

                        let data = Wanxiangshu.Runtime.Dyn.get resp "data"

                        if
                            Wanxiangshu.Runtime.Dyn.isNullish data
                            || not (Wanxiangshu.Runtime.Dyn.isArray data)
                        then
                            return StopUnknown
                        else
                            let msgs = unbox<obj array> data
                            let target = msgs |> Array.filter (SubsessionDispatch.isMessageMatch nonce)
                            let activeFound = target |> Array.exists SubsessionDispatch.isMessageActive

                            if activeFound then return StillRunning
                            elif target.Length > 0 then return Stopped
                            else return StopUnknown
                    with _ ->
                        return StopUnknown
                | Error _ -> return StopUnknown
            }

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
