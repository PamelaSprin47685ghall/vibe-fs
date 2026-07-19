module Wanxiangshu.Hosts.Opencode.ChatHooks

open Wanxiangshu.Runtime.Fallback.RuntimeStore

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dispatch
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeaseAcceptancePure
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Hosts.Opencode.ChatHooksDecoders
open Wanxiangshu.Hosts.Opencode.SubsessionDispatch

open Wanxiangshu.Kernel.Config
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.Clock
open Wanxiangshu.Runtime.EventLogRuntimeStore
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Wanxiangshu.Runtime.ChatHookOutputCodec
open Wanxiangshu.Runtime.OpencodeAgentConfigWire
open Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterTypes
open Wanxiangshu.Hosts.Opencode.ChatHooksMessageIdDedup

/// Append a continuation_host_accepted event recording that a user message
/// was observed for the given continuation. Does not modify memory state.
let recordContinuationUserMessage
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
              Payload = Map [ "continuationId", continuationId; "userMessageId", messageId ] }

        do! appendEventsAndCacheOrFail workspaceRoot [ wanEvent ]
    }

/// Attempt to consume a nudge or subsession nonce observed in a
/// chat.message hook payload. Returns true when the nonce is recognised.
let private tryConsumeNudgeIfMatched
    (fr: FallbackRuntimeStore)
    (workspaceRoot: string)
    (sessionIDStr: string)
    (msgId: string)
    (nonce: string)
    : bool =
    // Child subsession turn marker: ChatHooks resolves the host
    // receipt for SubsessionActor. Never forges TurnStarted from
    // the prompt Promise alone.
    let ws = workspaceFor workspaceRoot

    let receiptResult =
        HostReceiptWaiterRegistry.tryResolve
            ws
            sessionIDStr
            nonce
            (Wanxiangshu.Kernel.Subsession.Types.UserMessageObserved msgId)

    if receiptResult <> ResolveAttemptResult.NotFound then
        true
    else
        let activeNudgeNonce = (fr.GetSession sessionIDStr).ActiveNudgeNonce

        if activeNudgeNonce <> "" && nonce = activeNudgeNonce then
            // Transition the nudge lease to Dispatched and consume the
            // nonce now that the matching message has been observed.
            // This decouples the lease lifetime from the prompt()
            // Promise in NudgeEffect.sendNudge.
            let consumeAndDispatch (s: FallbackSessionRuntime) : FallbackSessionRuntime * bool =
                let s1, transitioned =
                    match s.PendingNudgeLease with
                    | Some lease when lease.Nonce = nonce ->
                        tryTransitionPendingNudgeLeaseReturning
                            lease.NudgeID
                            LeaseStatus.DispatchStarted
                            LeaseStatus.Dispatched
                            s
                    | _ -> s, false

                let s2, consumed = tryConsumeNudgeNonce nonce s1
                s2, transitioned || consumed

            let _ = fr.UpdateSessionReturning(sessionIDStr, consumeAndDispatch)
            true
        else
            match (fr.GetSession sessionIDStr).PendingLease with
            | Some lease when
                (lease.Status = LeaseStatus.DispatchStarted
                 || lease.Status = LeaseStatus.Dispatched
                 || lease.Status = LeaseStatus.Running)
                && lease.ContinuationID = nonce
                ->
                true
            | _ -> false

let private tryAcceptFallbackContinuation
    (fr: FallbackRuntimeStore)
    (sessionIDStr: string)
    (continuationId: string)
    : bool =
    fr.UpdateSessionReturning(sessionIDStr, tryAcceptPendingLeaseReturning continuationId)

/// Classify whether a chat.message hook payload is system-synthesised.
/// Internal so the regression suite can bind directly to this entry
/// point (mirroring the `static member internal isNaturalStop` pattern
/// in NudgeTrigger) instead of re-encoding the rule in test fixtures.
let internal isSystemMessage
    (parts: obj)
    (fr: FallbackRuntimeStore)
    (workspaceRoot: string)
    (sessionIDStr: string)
    (msgId: string)
    : bool =
    // Continuation prompts carry versioned provenance and should never
    // be confused with nudges, so check the namespaced kind first.
    match tryGetWanxiangshuKind parts with
    | Some "fallback_continuation" ->
        match tryDecodeWanxiangshuProvenance parts with
        | Some provenance ->
            let _ = tryAcceptFallbackContinuation fr sessionIDStr provenance.ContinuationId
            true
        | None -> true
    | _ ->
        match tryGetNonceFromParts parts with
        | Some nonce -> tryConsumeNudgeIfMatched fr workspaceRoot sessionIDStr msgId nonce
        | None -> false

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

/// Message-id dedup MUST happen before any side-effect that could cancel an
/// active fallback lease (`OnNewHumanMessage` does exactly that). The contract
/// is: classify -> dedup -> bind dispatch -> record provenance -> tool
/// overrides. The progress hook is preserved at the top because it only touches
/// the `ProgressObserver` stream, never the leases.
let chatMessageFor
    (host: Host)
    (registry: ChildAgentRegistry)
    (lifecycleObserver: Wanxiangshu.Hosts.Opencode.SessionLifecycleObserver.SessionLifecycleObserver)
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

        // Step 1: classify
        let isSystem =
            isSystemMessage parts fr lifecycleObserver.WorkspaceRoot sessionIDStr msgId

        let messageRole = tryGetChatMessageRole output

        // Step 2: dedup — drop the hook if the host already surfaced this id.
        let isDuplicate = msgId <> "" && markSeen sessionIDStr msgId

        if not isDuplicate then
            // Step 3: bind dispatch (only when this is a real user turn)
            if not isSystem && messageRole = "user" then
                let modelOpt = tryGetModelStringFromHook input output
                do! lifecycleObserver.OnNewHumanMessage(sessionIDStr, agent, modelOpt, msgId)

        do! recordProvenanceIfPresent parts msgId lifecycleObserver.WorkspaceRoot sessionIDStr

        applyToolOverrides host agent output
    }

let chatMessage
    (registry: ChildAgentRegistry)
    (lifecycleObserver: Wanxiangshu.Hosts.Opencode.SessionLifecycleObserver.SessionLifecycleObserver)
    (input: obj)
    (output: obj)
    : JS.Promise<unit> =
    chatMessageFor opencode registry lifecycleObserver input output

let noop (_a: obj) (_b: obj) : JS.Promise<unit> = Promise.lift ()
let noopEvent (_a: obj) : JS.Promise<unit> = Promise.lift ()
