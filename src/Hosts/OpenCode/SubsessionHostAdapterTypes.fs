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
open Wanxiangshu.Hosts.OpenCode.OpencodeSessionEventCodec
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
