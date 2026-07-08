module Wanxiangshu.Tests.KernelHelpersTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Kernel.MessageTransformPolicy
open Wanxiangshu.Kernel.Message
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
    let agents = [ "browser"; "investigator"; "executor"; "title"; "compaction" ]

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
    let agents = [ "browser"; "investigator"; "executor"; "title"; "compaction" ]

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

// ── WarnTdd ─────────────────────────────────────────────────────────────────

let parseWarnTddExactMatch () =
    check
        "exact canonical value parses"
        (parseWarnTdd "i-am-sure-i-have-followed-tdd-and-kolmolgorov-principles-and-kept-todo-updated" = Some
                                                                                                               IAmSureIHaveFollowedTddAndKolmolgorovPrinciples)

let parseWarnTddCaseInsensitive () =
    check
        "uppercase variant parses"
        (parseWarnTdd "I-AM-SURE-I-HAVE-FOLLOWED-TDD-AND-KOLMOLGOROV-PRINCIPLES-AND-KEPT-TODO-UPDATED" = Some
                                                                                                               IAmSureIHaveFollowedTddAndKolmolgorovPrinciples)

let parseWarnTddRejectsWrongValue () =
    check "wrong value returns None" (parseWarnTdd "something-else" = None)
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
    // WarnTdd
    parseWarnTddExactMatch ()
    parseWarnTddCaseInsensitive ()
    parseWarnTddRejectsWrongValue ()
    isModificationToolRecognises ()
    isModificationToolCaseInsensitive ()
    isModificationToolRejectsNonModification ()
