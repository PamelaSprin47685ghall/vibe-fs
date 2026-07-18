module Wanxiangshu.Tests.OpencodeNudgeLifecycleTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionPropertyTransitions
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Hosts.Opencode.ChatHooks
open Wanxiangshu.Hosts.Opencode.ChatHooksDecoders
open Wanxiangshu.Hosts.Opencode.NudgeTrigger

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

    let result = isSystemMessage parts fr "s-1" "msg-fc-1"

    check "fallback_continuation is classified as system" result
    check "store owner untouched" (fr.GetSessionOwner "s-1" = SessionOwner.NoOwner)
    check "store nonce untouched" (fr.GetActiveNudgeNonce "s-1" = "")

let test_isSystemMessage_plainUserIsNotSystem () =
    let fr = FallbackRuntimeStore()
    let parts = [| userPart "hello" "" |]

    let result = isSystemMessage parts fr "s-2" "msg-u-1"

    check "plain user part without provenance is NOT system" (not result)

let test_isSystemMessage_activeNudgeNonceIsSystemAndConsumed () =
    let fr = FallbackRuntimeStore()
    let nonce = "nudge-nonce-42"
    fr.SetActiveNudgeNonce "s-3" nonce

    let parts = [| userPart "go" nonce |]
    let result = isSystemMessage parts fr "s-3" "msg-n-1"

    check "active nudge nonce is classified as system" result
    check "nonce was atomically consumed" (fr.GetActiveNudgeNonce "s-3" = "")

let test_isSystemMessage_nonMatchingNonceDoesNotConsume () =
    let fr = FallbackRuntimeStore()
    fr.SetActiveNudgeNonce "s-4" "real-nonce"

    let parts = [| userPart "go" "fake-nonce" |]
    let result = isSystemMessage parts fr "s-4" "msg-n-2"

    check "non-matching nonce is NOT system" (not result)
    check "real nonce preserved" (fr.GetActiveNudgeNonce "s-4" = "real-nonce")

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
    check "UnknownLegacyStop + idle blocked" (not (eligible TerminalOrigin.UnknownLegacyStop "session.idle"))

// --- 4. TryConsumeActiveNudgeNonce: real implementation, real store ---

let test_tryConsumeActiveNudgeNonce_matchClears () =
    let fr = FallbackRuntimeStore()
    fr.SetActiveNudgeNonce "s-1" "nudge-nonce-1"
    check "stored before consume" (fr.GetActiveNudgeNonce "s-1" = "nudge-nonce-1")
    let consumed = fr.TryConsumeActiveNudgeNonce("s-1", "nudge-nonce-1")
    check "consume returns true on match" consumed
    check "nonce cleared after consume" (fr.GetActiveNudgeNonce "s-1" = "")

let test_tryConsumeActiveNudgeNonce_mismatchNoOp () =
    let fr = FallbackRuntimeStore()
    fr.SetActiveNudgeNonce "s-2" "expected"
    let consumed = fr.TryConsumeActiveNudgeNonce("s-2", "other-nonce")
    check "consume returns false on mismatch" (not consumed)
    check "nonce preserved on mismatch" (fr.GetActiveNudgeNonce "s-2" = "expected")

let test_tryConsumeActiveNudgeNonce_emptyNonceNoOp () =
    let fr = FallbackRuntimeStore()
    fr.SetActiveNudgeNonce "s-3" "anything"
    let consumed = fr.TryConsumeActiveNudgeNonce("s-3", "")
    check "consume returns false on empty observed" (not consumed)
    check "nonce preserved on empty observed" (fr.GetActiveNudgeNonce "s-3" = "anything")

// --- 5. isSystemMessage ↔ TryConsumeActiveNudgeNonce integration ---

let test_isSystemMessage_consumeIsNotLeaky () =
    let fr = FallbackRuntimeStore()
    let nonce = "nudge-nonce-77"
    fr.SetActiveNudgeNonce "s-5" nonce

    let firstResult = isSystemMessage [| userPart "first" nonce |] fr "s-5" "m-1"
    let secondResult = isSystemMessage [| userPart "second" nonce |] fr "s-5" "m-2"

    check "first match is system" firstResult
    check "after consume, second same-nonce message is NOT system" (not secondResult)

let run () =
    test_isSystemMessage_fallbackContinuationIsSystem ()
    test_isSystemMessage_plainUserIsNotSystem ()
    test_isSystemMessage_activeNudgeNonceIsSystemAndConsumed ()
    test_isSystemMessage_nonMatchingNonceDoesNotConsume ()
    test_tryGetChatMessageRole_userMessage ()
    test_tryGetChatMessageRole_assistantMessage ()
    test_tryGetChatMessageRole_noMessage ()
    test_isNudgeEvaluationEligible_allOrigins ()
    test_tryConsumeActiveNudgeNonce_matchClears ()
    test_tryConsumeActiveNudgeNonce_mismatchNoOp ()
    test_tryConsumeActiveNudgeNonce_emptyNonceNoOp ()
    test_isSystemMessage_consumeIsNotLeaky ()

let runAsync () : JS.Promise<unit> = Promise.lift (run ())
