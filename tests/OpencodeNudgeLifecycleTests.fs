module Wanxiangshu.Tests.OpencodeNudgeLifecycleTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.SessionRuntimeTransitions
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeaseAcceptancePure
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.Dispatch
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Dispatch.Protocol
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Hosts.Opencode.ChatHooks
open Wanxiangshu.Hosts.Opencode.ChatHooksDecoders
open Wanxiangshu.Hosts.Opencode.ChatHooksMessageIdDedup
open Wanxiangshu.Hosts.Opencode.NudgeTrigger
open Wanxiangshu.Hosts.Opencode.NudgeTriggerOps

// --- Production-shape fixtures (mirror packages/opencode/src/session/prompt.ts) ---
// In OpenCode, the chat.message hook is invoked with
//   { message: UserMessage; parts: Part[] }
// where `message` IS the info body — `role`, `id`, `agent`, etc. live at
// its top level, not under a nested `info` wrapper.

let private fallbackContinuationPart: obj =
    box
        {| ``type`` = "text"
           text = "\u200B"
           metadata =
            box
                {| wanxiangshu =
                    box
                        {| kind = "fallback_continuation"
                           schema = 2
                           continuationId = "fc-001"
                           continuationOrdinal = 1
                           attempt = 1
                           humanTurnId = "ht-1"
                           contextGeneration = 1
                           cancelGeneration = 0 |} |} |}

let private userPart (text: string) (nonce: string) : obj =
    box
        {| ``type`` = "text"
           text = text
           metadata = box {| nonce = nonce |} |}

let private userMessage (id: string) (parts: obj array) : obj =
    box
        {| role = "user"
           id = id
           parts = parts |}

let private hookOutput (msg: obj) (parts: obj array) : obj =
    createObj [ "message", box msg; "parts", box parts ]

// --- 1. isSystemMessage: real implementation, real store ---

let test_isSystemMessage_fallbackContinuationIsSystem () =
    let fr = FallbackRuntimeStore()
    let parts = [| fallbackContinuationPart |]

    let result = isSystemMessage parts fr "" "s-1" "msg-fc-1"

    check "fallback_continuation is classified as system" result
    check "store owner untouched" ((fr.GetSession "s-1").Owner = SessionOwner.NoOwner)
    check "store nonce untouched" ((fr.GetSession "s-1").ActiveNudgeNonce = "")

let test_isSystemMessage_acceptsFallbackContinuation () =
    let fr = FallbackRuntimeStore()

    let model =
        { ProviderID = "mock"
          ModelID = "model"
          Variant = None
          Temperature = None
          TopP = None
          MaxTokens = None
          ReasoningEffort = None
          Thinking = false }

    fr.UpdateSession(
        "s-accept",
        fun state ->
            let active = startDispatch model None state

            { active with
                PendingLease =
                    active.PendingLease
                    |> Option.map (fun lease -> { lease with ContinuationID = "fc-001" }) }
    )

    check
        "continuation starts in dispatch-started state"
        ((fr.GetSession "s-accept").PendingLease.Value.Status = LeaseStatus.Requested)

    let result =
        isSystemMessage [| fallbackContinuationPart |] fr "" "s-accept" "msg-fc-accept"

    check "fallback continuation remains system" result

    let accepted = (fr.GetSession "s-accept").PendingLease.Value

    check "host message advances continuation acceptance" (accepted.Status = LeaseStatus.Dispatched)
    check "accept binds real host user message id" (accepted.HostUserMessageId = "msg-fc-accept")

    check
        "HostUserMessageId bound on accept"
        ((fr.GetSession "s-accept").PendingLease.Value.HostUserMessageId = "msg-fc-accept")

let test_isSystemMessage_plainUserIsNotSystem () =
    let fr = FallbackRuntimeStore()
    let parts = [| userPart "hello" "" |]

    let result = isSystemMessage parts fr "" "s-2" "msg-u-1"

    check "plain user part without provenance is NOT system" (not result)

let test_isSystemMessage_activeNudgeNonceIsSystemAndConsumed () =
    let fr = FallbackRuntimeStore()
    let nonce = "nudge-nonce-42"
    fr.UpdateSession("s-3", armNudgeNonce nonce)

    let parts = [| userPart "go" nonce |]
    let result = isSystemMessage parts fr "" "s-3" "msg-n-1"

    check "active nudge nonce is classified as system" result
    check "nonce was atomically consumed" ((fr.GetSession "s-3").ActiveNudgeNonce = "")

let test_isSystemMessage_nonMatchingNonceDoesNotConsume () =
    let fr = FallbackRuntimeStore()
    fr.UpdateSession("s-4", armNudgeNonce "real-nonce")

    let parts = [| userPart "go" "fake-nonce" |]
    let result = isSystemMessage parts fr "" "s-4" "msg-n-2"

    check "non-matching nonce is NOT system" (not result)
    check "real nonce preserved" ((fr.GetSession "s-4").ActiveNudgeNonce = "real-nonce")

// --- 2. tryGetChatMessageRole: real implementation, production shape ---

let test_tryGetChatMessageRole_userMessage () =
    let out = hookOutput (userMessage "u-1" [||]) [||]
    check "role reads from message top level" (tryGetChatMessageRole out = "user")

let test_tryGetChatMessageRole_assistantMessage () =
    let assistantMsg =
        box
            {| role = "assistant"
               id = "a-1"
               parts = [||] |}

    let out = hookOutput assistantMsg [||]
    check "role reads from message top level" (tryGetChatMessageRole out = "assistant")

let test_tryGetChatMessageRole_noMessage () =
    let out = createObj [ "parts", box [||] ]
    check "no message → empty string" (tryGetChatMessageRole out = "")

// --- 3. isNudgeEvaluationEligible: real implementation, all branches ---

let test_isNudgeEvaluationEligible_allOrigins () =
    let eligible origin eventType =
        isNudgeEvaluationEligible origin eventType

    check "HumanTurnCompleted + idle eligible" (eligible TerminalOrigin.HumanTurnCompleted "session.idle")
    check "NudgeCompleted + idle eligible" (eligible TerminalOrigin.NudgeCompleted "session.idle")

    check
        "FallbackContinuationCompleted + idle eligible"
        (eligible TerminalOrigin.FallbackContinuationCompleted "session.idle")

    check "HumanTurnCompleted + status:idle eligible" (eligible TerminalOrigin.HumanTurnCompleted "session.status")
    check "NudgeCompleted + session.error blocked" (not (eligible TerminalOrigin.NudgeCompleted "session.error"))

    check
        "HumanTurnCompleted + session.error blocked"
        (not (eligible TerminalOrigin.HumanTurnCompleted "session.error"))

    check
        "CompactionContinuationCompleted + idle blocked"
        (not (eligible TerminalOrigin.CompactionContinuationCompleted "session.idle"))

    check
        "CompactionSummaryCompleted + idle blocked"
        (not (eligible TerminalOrigin.CompactionSummaryCompleted "session.idle"))

    check "TitleCompleted + idle blocked" (not (eligible TerminalOrigin.TitleCompleted "session.idle"))
    check "HumanTurnAborted + idle blocked" (not (eligible TerminalOrigin.HumanTurnAborted "session.idle"))
    check "ToolSubturnCompleted + idle blocked" (not (eligible TerminalOrigin.ToolSubturnCompleted "session.idle"))
    check "Unknown + idle blocked" (not (eligible TerminalOrigin.Unknown "session.idle"))

let test_activeFallbackLeaseVetoesNaturalStopCleanup () =
    let model =
        { ProviderID = "mock"
          ModelID = "model"
          Variant = None
          Temperature = None
          TopP = None
          MaxTokens = None
          ReasoningEffort = None
          Thinking = false }

    for status in
        [ LeaseStatus.Requested
          LeaseStatus.DispatchStarted
          LeaseStatus.AcceptanceUnknown
          LeaseStatus.Dispatched
          LeaseStatus.Running ] do
        let runtime = FallbackRuntimeStore()
        let sid = "active-fallback-" + string status

        runtime.UpdateSession(
            sid,
            fun state ->
                let active = startDispatch model None state

                { active with
                    PendingLease = active.PendingLease |> Option.map (fun lease -> { lease with Status = status }) }
        )

        check ($"non-terminal {status} lease protects owner") (hasActiveFallbackContinuation runtime sid)

    for status in [ LeaseStatus.Settled; LeaseStatus.Cancelled ] do
        let runtime = FallbackRuntimeStore()
        let sid = "terminal-fallback-" + string status

        runtime.UpdateSession(
            sid,
            fun state ->
                let active = startDispatch model None state

                { active with
                    PendingLease = active.PendingLease |> Option.map (fun lease -> { lease with Status = status }) }
        )

        check ($"terminal {status} lease does not protect owner") (not (hasActiveFallbackContinuation runtime sid))

    let ownerMismatch = FallbackRuntimeStore()
    let ownerMismatchSid = "fallback-owner-mismatch"

    ownerMismatch.UpdateSession(
        ownerMismatchSid,
        fun state ->
            let active = startDispatch model None state

            { active with
                PendingLease =
                    active.PendingLease
                    |> Option.map (fun lease ->
                        { lease with
                            Owner = SessionOwner.Human }) }
    )

    check
        "lease owner mismatch does not protect owner"
        (not (hasActiveFallbackContinuation ownerMismatch ownerMismatchSid))

// --- 4. TryConsumeActiveNudgeNonce: real implementation, real store ---

let test_tryConsumeActiveNudgeNonce_matchClears () =
    let fr = FallbackRuntimeStore()
    fr.UpdateSession("s-1", armNudgeNonce "nudge-nonce-1")
    check "stored before consume" ((fr.GetSession "s-1").ActiveNudgeNonce = "nudge-nonce-1")

    let consumed =
        fr.UpdateSessionReturning("s-1", tryConsumeNudgeNonce "nudge-nonce-1")

    check "consume returns true on match" consumed
    check "nonce cleared after consume" ((fr.GetSession "s-1").ActiveNudgeNonce = "")

let test_tryConsumeActiveNudgeNonce_mismatchNoOp () =
    let fr = FallbackRuntimeStore()
    fr.UpdateSession("s-2", armNudgeNonce "expected")
    let consumed = fr.UpdateSessionReturning("s-2", tryConsumeNudgeNonce "other-nonce")
    check "consume returns false on mismatch" (not consumed)
    check "nonce preserved on mismatch" ((fr.GetSession "s-2").ActiveNudgeNonce = "expected")

let test_tryConsumeActiveNudgeNonce_emptyNonceNoOp () =
    let fr = FallbackRuntimeStore()
    fr.UpdateSession("s-3", armNudgeNonce "anything")
    let consumed = fr.UpdateSessionReturning("s-3", tryConsumeNudgeNonce "")
    check "consume returns false on empty observed" (not consumed)
    check "nonce preserved on empty observed" ((fr.GetSession "s-3").ActiveNudgeNonce = "anything")

// --- 5. isSystemMessage ↔ TryConsumeActiveNudgeNonce integration ---

let test_isSystemMessage_consumeIsNotLeaky () =
    let fr = FallbackRuntimeStore()
    let nonce = "nudge-nonce-77"
    fr.UpdateSession("s-5", armNudgeNonce nonce)

    let firstResult = isSystemMessage [| userPart "first" nonce |] fr "" "s-5" "m-1"
    let secondResult = isSystemMessage [| userPart "second" nonce |] fr "" "s-5" "m-2"

    check "first match is system" firstResult
    check "after consume, second same-nonce message is NOT system" (not secondResult)

let test_chatMessageBindsOpaqueReceiptToRealMessageId () =
    let sid = "s-receipt-binding"
    let continuationId = "fc-receipt-binding"
    let workspace = Id.workspaceIdQuick "opencode-default"
    let waiter = HostReceiptWaiterRegistry.create workspace sid continuationId

    HostReceiptWaiter.resolveFromAcceptance waiter (OpaqueAccepted continuationId)
    check "opaque acceptance does not complete host receipt" (not waiter.Completed)

    let fr = FallbackRuntimeStore()

    let parts =
        [| box
               {| ``type`` = "text"
                  text = "\u200B"
                  metadata =
                   box
                       {| wanxiangshu =
                           box
                               {| kind = "fallback_continuation"
                                  schema = 2
                                  continuationId = continuationId |} |} |} |]

    check "continuation message is system-owned" (isSystemMessage parts fr "" sid "host-msg-binding")
    check "host receipt completed by chat.message" waiter.Completed

    match waiter.TransportState with
    | HostReceiptWaiterTransportState.ReceiptResolved(UserMessageObserved "host-msg-binding") ->
        check "receipt contains real host message id" true
    | other -> failwith ("expected real user-message receipt, got " + string other)

// --- 6. Classification order: system markers never raise human turn ---

let test_nudgeMarker_doesNotCountAsHumanTurn () =
    let fr = FallbackRuntimeStore()
    let nonce = "nudge-no-human"
    fr.UpdateSession("s-no-ht", armNudgeNonce nonce)
    let beforeTurn = (fr.GetSession "s-no-ht").HumanTurnId
    let beforeCancel = (fr.GetSession "s-no-ht").CancelGeneration

    let isSys = isSystemMessage [| userPart "go" nonce |] fr "" "s-no-ht" "msg-nudge-ht"

    check "nudge marker is system" isSys
    check "human turn id unchanged" ((fr.GetSession "s-no-ht").HumanTurnId = beforeTurn)
    check "cancel generation unchanged" ((fr.GetSession "s-no-ht").CancelGeneration = beforeCancel)

let test_continuationMarker_doesNotCountAsHumanTurn () =
    let fr = FallbackRuntimeStore()

    let model =
        { ProviderID = "mock"
          ModelID = "model"
          Variant = None
          Temperature = None
          TopP = None
          MaxTokens = None
          ReasoningEffort = None
          Thinking = false }

    fr.UpdateSession(
        "s-no-ht-fc",
        fun state ->
            let active = startDispatch model None state

            { active with
                PendingLease =
                    active.PendingLease
                    |> Option.map (fun lease -> { lease with ContinuationID = "fc-001" }) }
    )

    let beforeTurn = (fr.GetSession "s-no-ht-fc").HumanTurnId
    let beforeCancel = (fr.GetSession "s-no-ht-fc").CancelGeneration
    let beforeOwner = (fr.GetSession "s-no-ht-fc").Owner

    let isSys =
        isSystemMessage [| fallbackContinuationPart |] fr "" "s-no-ht-fc" "msg-fc-ht"

    check "continuation marker is system" isSys
    check "human turn id unchanged on continuation" ((fr.GetSession "s-no-ht-fc").HumanTurnId = beforeTurn)
    check "cancel generation unchanged on continuation" ((fr.GetSession "s-no-ht-fc").CancelGeneration = beforeCancel)
    check "owner not reset to Human" ((fr.GetSession "s-no-ht-fc").Owner = beforeOwner)

let test_dedupBeforeBind_secondObservationIsNoOp () =
    let fr = FallbackRuntimeStore()
    let sid = "s-dedup-order"
    let msgId = "msg-once"

    let model =
        { ProviderID = "mock"
          ModelID = "model"
          Variant = None
          Temperature = None
          TopP = None
          MaxTokens = None
          ReasoningEffort = None
          Thinking = false }

    fr.UpdateSession(
        sid,
        fun state ->
            let active = startDispatch model None state

            { active with
                PendingLease =
                    active.PendingLease
                    |> Option.map (fun lease -> { lease with ContinuationID = "fc-001" }) }
    )

    // Simulate ChatHooks order: dedup first, then classify only when fresh.
    let firstDup = markSeen sid msgId
    check "first markSeen is not duplicate" (not firstDup)

    let _ = isSystemMessage [| fallbackContinuationPart |] fr "" sid msgId
    let bound = (fr.GetSession sid).PendingLease.Value.HostUserMessageId
    check "first observation binds host msg" (bound = msgId)

    let secondDup = markSeen sid msgId
    check "second markSeen is duplicate" secondDup

    // Dedup gate means classify must not run again — binding stays stable.
    check "host binding stable under duplicate" ((fr.GetSession sid).PendingLease.Value.HostUserMessageId = msgId)
    forget sid

let run () =
    test_isSystemMessage_fallbackContinuationIsSystem ()
    test_isSystemMessage_acceptsFallbackContinuation ()
    test_isSystemMessage_plainUserIsNotSystem ()
    test_isSystemMessage_activeNudgeNonceIsSystemAndConsumed ()
    test_isSystemMessage_nonMatchingNonceDoesNotConsume ()
    test_tryGetChatMessageRole_userMessage ()
    test_tryGetChatMessageRole_assistantMessage ()
    test_tryGetChatMessageRole_noMessage ()
    test_isNudgeEvaluationEligible_allOrigins ()
    test_activeFallbackLeaseVetoesNaturalStopCleanup ()
    test_tryConsumeActiveNudgeNonce_matchClears ()
    test_tryConsumeActiveNudgeNonce_mismatchNoOp ()
    test_tryConsumeActiveNudgeNonce_emptyNonceNoOp ()
    test_isSystemMessage_consumeIsNotLeaky ()
    test_chatMessageBindsOpaqueReceiptToRealMessageId ()
    test_nudgeMarker_doesNotCountAsHumanTurn ()
    test_continuationMarker_doesNotCountAsHumanTurn ()
    test_dedupBeforeBind_secondObservationIsNoOp ()

let runAsync () : JS.Promise<unit> = Promise.lift (run ())
