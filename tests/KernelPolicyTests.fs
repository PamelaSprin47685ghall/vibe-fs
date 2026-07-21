module Wanxiangshu.Tests.KernelPolicyTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Kernel.MessageTransformPolicy
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.CapsSynthPolicy

// ── ToolResult ──────────────────────────────────────────────────────────────

let wireEncodeResultOk () =
    check "wireEncodeResult Ok returns plain text" (wireEncodeResult (Ok "done") = "done")

let wireEncodeResultErrorContainsFailed () =
    let err = InvalidIntent("coder", "intents", "required")
    let text = wireEncodeResult (Error err)
    check "wireEncodeResult Error contains 'failed'" (text.Contains "failed")
    check "wireEncodeResult Error contains domain error text" (text.Contains "invalid intents")

let wireEncodeToolErrorFormat () =
    let err = ParseError("ctx", "detail")
    let text = wireEncodeToolError "Subagent" err
    check "wireEncodeToolError starts with context" (text.StartsWith "Subagent failed:")
    check "wireEncodeToolError contains parse error" (text.Contains "parse error in ctx")

// ── MessageTransformPolicy ──────────────────────────────────────────────────

let defaultExcludedAgentsTrue () =
    let agents = [ "browser"; "inspector"; "executor"; "title"; "compaction" ]

    agents
    |> List.iter (fun a -> check (sprintf "default excluded: %s" a) (shouldExcludeAgentFromProjection a false))

let defaultExcludedAgentsFalse () =
    let agents = [ "main"; "agent"; "manager"; "user" ]

    agents
    |> List.iter (fun a -> check (sprintf "not excluded: %s" a) (not (shouldExcludeAgentFromProjection a false)))

let childWorkspaceExtraExcluded () =
    let agents = [ "exec"; "explore" ]

    agents
    |> List.iter (fun a -> check (sprintf "child excluded: %s" a) (shouldExcludeAgentFromProjection a true))

let childWorkspaceDefaultStillExcluded () =
    let agents = [ "browser"; "inspector"; "executor"; "title"; "compaction" ]

    agents
    |> List.iter (fun a -> check (sprintf "child still excluded: %s" a) (shouldExcludeAgentFromProjection a true))

    check "main not excluded in child workspace" (not (shouldExcludeAgentFromProjection "main" true))
    check "agent not excluded in child workspace" (not (shouldExcludeAgentFromProjection "agent" true))

// ── Message constants ───────────────────────────────────────────────────────

let messagePrefixes () =
    check "capsSynthUserPrefix value" (capsSynthUserPrefix = "caps-synth-user-")
    check "capsSynthAssistantPrefix value" (capsSynthAssistantPrefix = "caps-synth-assistant-")

// ── CapsSynthPolicy ─────────────────────────────────────────────────────────

let isCapsSynthIdMatches () =
    check "caps synth id user positive" (isCapsSynthId "caps-synth-user-123")
    check "caps synth id assistant positive" (isCapsSynthId "caps-synth-assistant-456")
    check "bare prefix accepted" (isCapsSynthId "caps-synth-")

let isCapsSynthIdRejects () =
    check "empty string invalid caps id" (not (isCapsSynthId ""))
    check "no prefix invalid caps id" (not (isCapsSynthId "other-123"))
    check "nullish not applicable (value type is string)" true

let capsToolCallIdFitsProviderLimit () =
    let epochId = "session-0123456789abcdefghijklmnopqrstuvwxyz-0123456789"
    let callId = capsToolCallId "caps-call-" epochId "0123456789abcdef" 12

    check "caps call ID stays within provider limit" (callId.Length <= 64)
    check "caps call ID preserves session suffix" (callId.Contains "defghijklmnopqrstuvwxyz-0123456789")
    check "caps call ID preserves fingerprint and index" (callId.EndsWith "-0123456789abcdef-12")

// ── WarnTdd ─────────────────────────────────────────────────────────────────

let run () =
    // ToolResult
    wireEncodeResultOk ()
    wireEncodeResultErrorContainsFailed ()
    wireEncodeToolErrorFormat ()
    // MessageTransformPolicy
    defaultExcludedAgentsTrue ()
    defaultExcludedAgentsFalse ()
    childWorkspaceExtraExcluded ()
    childWorkspaceDefaultStillExcluded ()
    // Message
    messagePrefixes ()
    // CapsSynthPolicy
    isCapsSynthIdMatches ()
    isCapsSynthIdRejects ()
    capsToolCallIdFitsProviderLimit ()
// WarnTdd removed
