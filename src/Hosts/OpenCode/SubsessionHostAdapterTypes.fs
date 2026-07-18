module Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterTypes

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Dispatch.Identity
open Wanxiangshu.Kernel.Dispatch.Protocol
open Wanxiangshu.Runtime.Dispatch
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.OpencodeSessionEventCodec
open Wanxiangshu.Hosts.Opencode.SubsessionDispatch
open Wanxiangshu.Runtime.OpencodeSessionPromptBuilder

let invoke1 (arg: obj) (method: string) (target: obj) : JS.Promise<obj> = unbox (target?(method) (arg))

let buildDispatchModelString =
    Wanxiangshu.Hosts.Opencode.SubsessionDispatch.buildDispatchModelString

let subsessionRegistry = sharedDispatchRegistry

let workspaceFor (directory: string) : WorkspaceId =
    if directory = "" then
        Id.workspaceIdQuick "opencode-default"
    else
        Id.workspaceIdQuick ("opencode:" + directory)

let loggerFor (_: WorkspaceId) : IDispatchEventLogger =
    Wanxiangshu.Runtime.Dispatch.InMemoryDispatchEventLogger() :> IDispatchEventLogger

let getDispatcher (directory: string) (sid: string) : Wanxiangshu.Runtime.Dispatch.SessionDispatcher =
    let ws = workspaceFor directory
    subsessionRegistry.GetOrCreate ws sid (loggerFor ws)

let trySessionApi (client: obj) : Result<obj, string> =
    match getSessionApiFromClient client with
    | Ok s -> Ok s
    | Error _ -> Error "session API missing"

let promptDigest (prompt: string) : string =
    Wanxiangshu.Runtime.FileSys.sha256HexTruncated prompt

let encodeDispatchIdentity
    (directory: string)
    (sessionId: string)
    (turnId: TurnId)
    (_model: FallbackModel option)
    (prompt: string)
    : Wanxiangshu.Kernel.Dispatch.Identity.DispatchIdentity =
    let ws = workspaceFor directory
    let logicalTurnId = TurnId.value turnId

    let core =
        Wanxiangshu.Kernel.Dispatch.Identity.DispatchIdentity.newId
            ws
            sessionId
            SubsessionTurn
            0
            0
            0
            logicalTurnId
            ""
            ""

    let meta = Map.ofList [ "prompt_digest", promptDigest prompt ]

    { SchemaVersion = core.SchemaVersion
      DispatchId = core.DispatchId
      WorkspaceId = core.WorkspaceId
      PhysicalSessionId = core.PhysicalSessionId
      Kind = core.Kind
      RunGeneration = core.RunGeneration
      CancelGeneration = core.CancelGeneration
      Attempt = core.Attempt
      LogicalTurnId = core.LogicalTurnId
      HumanTurnId = core.HumanTurnId
      RequestedAtMs = core.RequestedAtMs
      ExpectedParentId = core.ExpectedParentId
      Metadata = meta }

let buildBody (agent: string) (model: FallbackModel option) (prompt: string) (turnId: TurnId) : obj =
    let modelStr = buildDispatchModelString model
    let nonce = TurnId.value turnId
    createPromptBodyWithModelAndNonce (Some agent) modelStr prompt (Some nonce)

let decodeResponseForUserMessageId (response: obj) : string option =
    if isNull response then
        None
    else
        let id1 = Wanxiangshu.Runtime.Dyn.str response "id"

        if id1 <> "" then
            Some id1
        else
            let data = Wanxiangshu.Runtime.Dyn.get response "data"

            if Wanxiangshu.Runtime.Dyn.isNullish data then
                None
            else
                let id2 = Wanxiangshu.Runtime.Dyn.str data "id"

                if id2 <> "" then Some id2 else None

let toStringTerminal (t: Wanxiangshu.Kernel.Dispatch.Protocol.DispatchTerminal) : string =
    match t with
    | Wanxiangshu.Kernel.Dispatch.Protocol.Completed -> "completed"
    | Wanxiangshu.Kernel.Dispatch.Protocol.Failed e -> "failed:" + e.ErrorName
    | Wanxiangshu.Kernel.Dispatch.Protocol.Cancelled -> "cancelled"
    | Wanxiangshu.Kernel.Dispatch.Protocol.Superseded -> "superseded"
    | Wanxiangshu.Kernel.Dispatch.Protocol.SessionClosed -> "session_closed"
    | Wanxiangshu.Kernel.Dispatch.Protocol.RejectedBeforeSend e -> "rejected_before_send:" + e.ErrorName
    | Wanxiangshu.Kernel.Dispatch.Protocol.TransportUnavailable e -> "transport_unavailable:" + e.ErrorName
    | Wanxiangshu.Kernel.Dispatch.Protocol.AcceptanceUnknown e -> "acceptance_unknown:" + e.ErrorName
    | Wanxiangshu.Kernel.Dispatch.Protocol.AbortUnknown e -> "abort_unknown:" + e.ErrorName
    | Wanxiangshu.Kernel.Dispatch.Protocol.TimedOut e -> "timed_out:" + e.ErrorName
    | Wanxiangshu.Kernel.Dispatch.Protocol.Poisoned s -> "poisoned:" + s

/// Plain-function wrappers around the `SessionDispatcherExtensions`
/// type extensions. Required because F# type-extension members cannot be
/// invoked via dot syntax from a different module's top-level `let`
/// binding in every shape (especially with multiple curried arguments);
/// a plain `let` is unambiguous.
let bindHostUserMessage
    (dispatcher: Wanxiangshu.Runtime.Dispatch.SessionDispatcher)
    (logicalTurnId: string)
    (messageId: string)
    : unit =
    dispatcher.BindHostUserMessage logicalTurnId messageId

let onSessionClosed (dispatcher: Wanxiangshu.Runtime.Dispatch.SessionDispatcher) : unit = dispatcher.OnSessionClosed()

// ── Extracted plain functions ───────────────────────────────────────────────

/// Build the send-prompt function for a specific subsession turn.
/// The returned function matches the `DispatchIdentity -> JS.Promise<…>`
/// shape expected by `SessionDispatcher.Dispatch`.
let buildSendPrompt
    (client: obj)
    (agent: string)
    (directory: string)
    (sessionId: SessionId)
    (turn: TurnPlan)
    : DispatchIdentity -> JS.Promise<DispatchAcceptance> =
    fun (_: DispatchIdentity) ->
        promise {
            match trySessionApi client with
            | Error msg ->
                let err =
                    { ErrorName = "TransportUnavailable"
                      DomainError = None
                      Message = "opencode_session_api_missing: " + msg
                      StatusCode = None
                      IsRetryable = Some false }

                PendingTurnReceipt.markTransportRejected (TurnId.value turn.TurnId) err
                return! Promise.reject (System.Exception("opencode_session_api_missing:" + msg))
            | Ok session ->
                let body = buildBody agent turn.Model turn.Prompt turn.TurnId

                let arg =
                    box
                        {| path = box {| id = SessionId.value sessionId |}
                           body = body |}

                let! response = invoke1 arg "prompt" session

                let mid = decodeResponseForUserMessageId response

                match mid with
                | Some id when id <> "" ->
                    // ChatHooks resolves the receipt when it sees the
                    // chat.message event.  We only return the acceptance
                    // type here; we do NOT call PendingTurnReceipt.tryResolve.
                    return UserMessageAccepted id
                | _ ->
                    // OpaqueAccepted: no host user-message id to bind.
                    // Resolve the receipt here since ChatHooks will
                    // never be able to.
                    let receipt = OrderedTurnMarkerObserved
                    let _ = PendingTurnReceipt.tryResolve (TurnId.value turn.TurnId) receipt
                    return OpaqueAccepted("opencode:" + TurnId.value turn.TurnId)
        }

/// Build the query-dispatch-status function for a specific subsession turn.
/// The returned function is `unit -> JS.Promise<…>` so the caller can
/// hold it as a closure and invoke it at the right moment.
let buildQueryDispatchStatus
    (client: obj)
    (directory: string)
    (sessionId: SessionId)
    (turnId: TurnId)
    : unit -> JS.Promise<DispatchStatus> =
    fun () ->
        promise {
            let dispatcher = getDispatcher directory (SessionId.value sessionId)
            let nonce = TurnId.value turnId

            match dispatcher.ActiveLogicalTurnId with
            | Some active when active = nonce ->
                match trySessionApi client with
                | Ok session ->
                    try
                        let! resp =
                            invoke1 (box {| path = box {| id = SessionId.value sessionId |} |}) "messages" session

                        let data = Wanxiangshu.Runtime.Dyn.get resp "data"

                        if
                            Wanxiangshu.Runtime.Dyn.isNullish data
                            || not (Wanxiangshu.Runtime.Dyn.isArray data)
                        then
                            return StillPending
                        else
                            let msgs = unbox<obj array> data
                            let found = msgs |> Array.exists (SubsessionDispatch.isMessageMatch nonce)

                            if found then
                                let mid =
                                    msgs
                                    |> Array.tryFind (SubsessionDispatch.isMessageMatch nonce)
                                    |> Option.map (fun m -> Wanxiangshu.Runtime.Dyn.str m "id")
                                    |> Option.defaultValue ""

                                return DispatchStatus.Accepted(UserMessageObserved mid)
                            else
                                return StillPending
                    with _ ->
                        return StillPending
                | Error _ -> return StillPending

            // Dispatcher has no active turn — fall back to PendingTurnReceipt.
            | _ ->
                match PendingTurnReceipt.tryGetTransportState nonce with
                | Some PendingTurnReceipt.InFlight -> return StillPending
                | Some(PendingTurnReceipt.RejectedBeforeSend err) -> return TransportRejectedBeforeSend err
                | Some(PendingTurnReceipt.FailedAfterUnknown err) -> return TransportFailedAfterUnknownAcceptance err
                | None -> return Unknown
        }

/// Build the query-session-quiescence function for a specific subsession turn.
let buildQuerySessionQuiescence
    (client: obj)
    (sessionId: SessionId)
    (turnId: TurnId)
    : unit -> JS.Promise<QuiescenceStatus> =
    fun () ->
        promise {
            let nonce = TurnId.value turnId

            match trySessionApi client with
            | Ok session ->
                try
                    let! resp = invoke1 (box {| path = box {| id = SessionId.value sessionId |} |}) "messages" session

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
