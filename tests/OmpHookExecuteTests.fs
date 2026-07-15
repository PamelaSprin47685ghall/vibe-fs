module Wanxiangshu.Tests.OmpHookExecuteTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Omp.HookExecute

/// applyToolResultHook on a `coder` invocation must mirror the agent's intents
/// onto `ui_` so the chat UI shows a one-line summary before the agent finishes.
let hookCoderInjectUiLabel () =
    let args =
        createObj
            [ "intents",
              box
                  [| box
                         {| objective = "Add submit_review wip field"
                            background = "Reviewers must record progress separately from final review."
                            targets =
                             [| box
                                    {| file = "submitReview.ts"
                                       guide = "Add wip field plus canonical acknowledgment." |} |] |}
                     box
                         {| objective = "Wire HookExecute in SessionLifecycle"
                            background = "OmpSessionLifecycle.toolResult must apply labels on every tool call."
                            targets =
                             [| box
                                    {| file = "SessionLifecycle.fs"
                                       guide = "Call applyToolResultHook before backlog append." |} |] |} |] ]

    applyToolResultHook "coder" args
    let label = get args "ui_"
    check "ui label present" (not (isNullish label))
    let text = string label
    check "ui label contains first objective" (text.Contains "Add submit_review wip field")
    check "ui label contains second objective" (text.Contains "Wire HookExecute in SessionLifecycle")
    check "ui label joins with semicolon space" (text.Contains "; ")

/// applyToolResultHook on an `investigator` invocation must produce a single
/// joined label of all its entries. Without `ui_` injection the chat UI is
/// stuck waiting for a verbose tool call.
let hookInvestigatorInjectUiLabel () =
    let args =
        createObj
            [ "intents",
              box
                  [| box
                         {| objective = "Confirm per-workspace writeQueues contract"
                            background = "Per-workspace queues keep sessions from blocking each other."
                            questions = [| "Is the queue map keyed by workspaceRoot?" |] |} |] ]

    applyToolResultHook "investigator" args
    let label = get args "ui_"
    check "investigator ui label present" (not (isNullish label))
    let text = string label
    check "investigator label text" (text.Contains "Confirm per-workspace writeQueues contract")

/// For tools that do not declare intents, applyToolResultHook must not invent
/// a UI label. `ui_` is opt-in, never fabricated.
let hookNonSubagentDoesNotInjectUiLabel () =
    let args = createObj []
    applyToolResultHook "read" args
    check "read args untouched" (isNullish (get args "ui_"))
    applyToolResultHook "executor" args
    check "executor args untouched" (isNullish (get args "ui_"))

/// pi accepts three names for the patch tool. The hook must rewrite
/// `apply_patch` payload (`patch` key) into the canonical `patchText` key so
/// downstream consumers do not see three different shapes.
let hookApplyPatchNormalisesPatchToPatchText () =
    let args = createObj [ "patch", box "diff --git a/x b/x" ]
    applyToolResultHook "apply_patch" args
    check "patchText derived from patch" (str args "patchText" = "diff --git a/x b/x")

/// When the patch body arrives as the args root itself (string form), the
/// hook must no-op rather than throwing on `args?patchText <-` — JS strings
/// cannot accept new properties.
let hookApplyPatchStringArgsIsNoOp () =
    let args = "diff --git a/x b/x" :> obj
    applyToolResultHook "apply_patch" args
    let raw = string args
    check "string args left intact" (raw = "diff --git a/x b/x")

/// When the patch tool arrives under `patch` name (alternative id), the hook
/// must still normalise it. Two ids, one canonical key.
let hookPatchNameNormalisesToPatchText () =
    let args = createObj [ "patch", box "diff --git a/y b/y" ]
    applyToolResultHook "patch" args
    check "patch id rewritten" (str args "patchText" = "diff --git a/y b/y")

/// When `patchText` is already set, the hook must not overwrite it. Order
/// matters: user-supplied canonical form wins over patch/text fallbacks.
let hookApplyPatchLeavesExistingPatchTextUntouched () =
    let args =
        createObj [ "patchText", box "user-supplied"; "patch", box "should-be-ignored" ]

    applyToolResultHook "apply_patch" args
    check "existing patchText preserved" (str args "patchText" = "user-supplied")
