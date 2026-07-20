module Wanxiangshu.Hosts.Opencode.ChatHooks

open Fable.Core
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Config
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Wanxiangshu.Runtime.ChatHookOutputCodec
open Wanxiangshu.Runtime.OpencodeAgentConfigWire
open Wanxiangshu.Hosts.Opencode.ChatHooksDecoders
open Wanxiangshu.Hosts.Opencode.ChatHooksClassification
open Wanxiangshu.Hosts.Opencode.ChatHooksMessageIdDedup
open Wanxiangshu.Hosts.Opencode.SessionLifecycleObserver
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Runtime.Clock
open Wanxiangshu.Runtime.EventLogRuntimeStore

let internal isSystemMessage
    (parts: obj)
    (fr: FallbackRuntimeStore)
    (workspaceRoot: string)
    (sessionIDStr: string)
    (msgId: string)
    : bool =
    ChatHooksClassification.isSystemMessage parts fr workspaceRoot sessionIDStr msgId

let private recordContinuationUserMessage
    (workspaceRoot: string)
    (sessionId: string)
    (messageId: string)
    (continuationId: string)
    : JS.Promise<unit> =
    promise {
        let at = getTimestampMs().ToString()

        let wanEvent: WanEvent =
            { V = 2
              Session = sessionId
              Kind = eventKindContinuationHostAccepted
              At = at
              Payload = Map [ "continuationId", continuationId; "userMessageId", messageId ]
              EventId = None
              WriterId = None
              Sequence = None
              Checksum = None }

        do! appendEventsAndCacheOrFail workspaceRoot [ wanEvent ]
    }

let private recordProvenanceIfPresent
    (parts: obj)
    (msgId: string)
    (workspaceRoot: string)
    (sessionIDStr: string)
    : JS.Promise<unit> =
    promise {
        if msgId <> "" then
            match tryDecodeWanxiangshuProvenance parts with
            | Some provenance ->
                do! recordContinuationUserMessage workspaceRoot sessionIDStr msgId provenance.ContinuationId
            | None -> ()
    }

let private applyToolOverrides (host: Host) (agent: string) (output: obj) : unit =
    match chatMessageFromHookOutput output with
    | None -> ()
    | Some message ->
        match chatMessageToolsFromOutput output with
        | None -> ()
        | Some tools ->
            match filterChatToolsForAgent host agent (Some tools) with
            | None -> ()
            | Some filtered -> setKey message "tools" (encodeToolsOverridesToMessage filtered)

/// SPEC §七 fixed order:
/// 1. decode session + message id
/// 2. message id dedup (before any cancel side-effect)
/// 3. bind PendingDispatch / classify SystemGenerated
/// 4. system → skip OnNewHumanMessage
/// 5. remaining user role → human turn (cancel owners)
/// 6. provenance record + tool overrides
let chatMessageFor
    (host: Host)
    (registry: ChildAgentRegistry)
    (lifecycleObserver: SessionLifecycleObserver)
    (input: obj)
    (output: obj)
    : JS.Promise<unit> =
    promise {
        let agent = resolveAgent registry input output

        let sessionID =
            Wanxiangshu.Kernel.Primitives.Identity.Id.sessionIdQuick (sessionIdFromHookInput input "")

        let parts = partsFromHookOutput output
        let msgObj = Dyn.get output "message"
        let msgId = if Dyn.isNullish msgObj then "" else Dyn.str msgObj "id"

        let sessionIDStr =
            Wanxiangshu.Kernel.Primitives.Identity.Id.sessionIdValue sessionID

        let fr = lifecycleObserver.FallbackRuntime

        do! lifecycleObserver.handleChatMessage (sessionID, agent, parts)

        // Step 2: msgId dedup BEFORE bind / human-turn cancel.
        let isDuplicate = msgId <> "" && markSeen sessionIDStr msgId

        if not isDuplicate then
            // Step 3–4: bind PendingDispatch + system classification.
            let isSystem =
                ChatHooksClassification.isSystemMessage parts fr lifecycleObserver.WorkspaceRoot sessionIDStr msgId

            let messageRole = tryGetChatMessageRole output

            // Step 5: only residual human user turns cancel owners.
            if not isSystem && messageRole = "user" then
                let modelOpt = tryGetModelStringFromHook input output
                do! lifecycleObserver.OnNewHumanMessage(sessionIDStr, agent, modelOpt, msgId)

            do! recordProvenanceIfPresent parts msgId lifecycleObserver.WorkspaceRoot sessionIDStr

        applyToolOverrides host agent output
    }

let chatMessage
    (registry: ChildAgentRegistry)
    (lifecycleObserver: SessionLifecycleObserver)
    (input: obj)
    (output: obj)
    : JS.Promise<unit> =
    chatMessageFor opencode registry lifecycleObserver input output

let noop (_a: obj) (_b: obj) : JS.Promise<unit> = Promise.lift ()
let noopEvent (_a: obj) : JS.Promise<unit> = Promise.lift ()
