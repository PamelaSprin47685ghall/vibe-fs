module Wanxiangshu.Tests.KernelHelpersTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Kernel.MessageTransformPolicy
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.CapsSynthPolicy
open Wanxiangshu.Kernel.WarnTdd

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
    check "backlogProjectionIdPrefix value" (backlogProjectionIdPrefix = "backlog-projection-")
    check "backlogPrefixIdPrefix value" (backlogPrefixIdPrefix = "backlog-prefix-")

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

let parseWarnTddExactMatch () =
    check
        "exact canonical value parses"
        (parseWarnTdd "i-am-sure-i-have-followed-tdd-and-kolmogorov-principles-and-kept-todo-updated" = Some
                                                                                                            IAmSureIHaveFollowedTddAndKolmogorovPrinciples)

let parseWarnTddCaseInsensitive () =
    check
        "uppercase variant parses"
        (parseWarnTdd "I-AM-SURE-I-HAVE-FOLLOWED-TDD-AND-KOLMOGOROV-PRINCIPLES-AND-KEPT-TODO-UPDATED" = Some
                                                                                                            IAmSureIHaveFollowedTddAndKolmogorovPrinciples)

let parseWarnTddRejectsWrongValue () =
    check "whitespace string returns None" (parseWarnTdd "   " = None)
    check "empty string returns None" (parseWarnTdd "" = None)

let isModificationToolRecognises () =
    check "coder is modification tool" (isModificationTool "coder")
    check "executor is modification tool" (isModificationTool "executor")
    check "write is modification tool" (isModificationTool "write")
    check "edit is modification tool" (isModificationTool "edit")
    check "apply_patch is modification tool" (isModificationTool "apply_patch")
    check "patch is modification tool" (isModificationTool "patch")
    check "ast_edit is modification tool" (isModificationTool "ast_edit")
    check "ast_grep_replace is modification tool" (isModificationTool "ast_grep_replace")
    check "file_edit_replace_string is modification tool" (isModificationTool "file_edit_replace_string")
    check "file_edit_insert is modification tool" (isModificationTool "file_edit_insert")

let isModificationToolCaseInsensitive () =
    check "Coder (capital C) recognised" (isModificationTool "Coder")
    check "EDIT (all caps) recognised" (isModificationTool "EDIT")

let isModificationToolRejectsNonModification () =
    check "read is not a modification tool" (not (isModificationTool "read"))
    check "search is not a modification tool" (not (isModificationTool "search"))
    check "fuzzy is not a modification tool" (not (isModificationTool "fuzzy"))
    check "random is not a modification tool" (not (isModificationTool "random"))

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
    // WarnTdd
    parseWarnTddExactMatch ()
    parseWarnTddCaseInsensitive ()
    parseWarnTddRejectsWrongValue ()
    isModificationToolRecognises ()
    isModificationToolCaseInsensitive ()
    isModificationToolRejectsNonModification ()
