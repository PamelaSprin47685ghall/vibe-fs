module VibeFs.Tests.OmpSessionLifecycleTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Omp.ChildSession
open VibeFs.Omp.SessionLifecycle

/// `recordsToBookkeeper` must accept every file-edit tool via `isFileEditTool`
/// (apply_patch, edit, write, ast_edit, …) plus the explicit subagent and IO
/// names. A missing entry here would silently let a write tool skip the
/// long-term knowledge graph bookkeeper and starve future compaction.
let recordsToBookkeeperIncludesApplyPatch () =
    clearChildSessionsForTest ()
    check "apply_patch records" (recordsToBookkeeper "apply_patch")
    check "edit records" (recordsToBookkeeper "edit")
    check "write records" (recordsToBookkeeper "write")
    check "ast_edit records" (recordsToBookkeeper "ast_edit")
    check "coder records" (recordsToBookkeeper "coder")
    check "investigator records" (recordsToBookkeeper "investigator")
    check "executor records" (recordsToBookkeeper "executor")
    check "patch alias records" (recordsToBookkeeper "patch")
    check "fuzzy_find never records" (not (recordsToBookkeeper "fuzzy_find"))
    check "read never records" (not (recordsToBookkeeper "read"))

/// `mode = "ro"` executor calls surface in the conversation but are
/// short-lived — they must not pollute the bookkeeper that survives context
/// compaction. Only this exact `mode` value exempts the call; any other mode
/// (or missing mode) keeps the bookkeeping obligation.
let isReadOnlyExecutorTrueForRoMode () =
    let args = createObj [ "mode", box "ro" ]
    check "executor ro exempted" (isReadOnlyExecutor "executor" args)

let isReadOnlyExecutorFalseForRwMode () =
    let rwArgs = createObj [ "mode", box "rw" ]
    check "executor rw recorded" (not (isReadOnlyExecutor "executor" rwArgs))
    let missingArgs = createObj []
    check "executor missing mode recorded" (not (isReadOnlyExecutor "executor" missingArgs))
    let coderArgs = createObj [ "mode", box "ro" ]
    check "coder ro mode still records" (not (isReadOnlyExecutor "coder" coderArgs))
    let nullArgs = box null
    check "executor null args recorded" (not (isReadOnlyExecutor "executor" nullArgs))

/// The child-session registry is the seam between `createChildSession` and
/// the `tool_result` filter. Marking and unmarking must be exact, with empty
/// ids treated as no-ops so a misconfigured host cannot poison the set.
let isChildSessionGuardSkipsBookkeeper () =
    clearChildSessionsForTest ()
    let parentId = "parent-session-1"
    let childId = "child-coder-session"
    check "parent initially not a child" (not (isChildSession parentId))
    check "child initially not a child" (not (isChildSession childId))
    markChildSession childId
    check "child now registered" (isChildSession childId)
    check "parent still not a child" (not (isChildSession parentId))
    check "empty id never registers" (not (isChildSession ""))
    unmarkChildSession childId
    check "child cleared after unmark" (not (isChildSession childId))
    markChildSession ""
    check "marking empty id does not register random id" (not (isChildSession "random-after-empty-mark"))
    markChildSession "probe"
    check "non-empty mark still works after empty mark" (isChildSession "probe")
    unmarkChildSession "probe"
