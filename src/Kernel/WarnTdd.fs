module Wanxiangshu.Kernel.WarnTdd

let warnTddKey = "follow-tdd-and-kolmogorov-principles"
let warnKey = "impossible-via-other-tools"
let warnReuseKey = "not-suitable-via-continue-tool"

let canonicalValue =
    "i-am-sure-i-have-followed-tdd-and-kolmogorov-principles-and-kept-todo-updated"

let warnCanonicalValue =
    "it-is-not-possible-to-do-it-using-other-tools-and-only-run-tests-when-static-analysis-cannot-handle-it"

let warnReuseCanonicalValue =
    "this-task-is-not-suitable-to-be-completed-via-continue-tool"

/// Tools where warn_tdd is enforced — all tools that can modify code.
let modificationTools: Set<string> =
    Set.ofList
        [ "coder"
          "executor"
          "write"
          "edit"
          "apply_patch"
          "patch"
          "ast_edit"
          "ast_grep_replace"
          "file_edit_replace_string"
          "file_edit_insert"
          "pty_spawn"
          "pty_write"
          "pty_read"
          "pty_list"
          "pty_kill"
          "swap" ]

let isModificationTool (tool: string) : bool =
    Set.contains (tool.ToLowerInvariant()) modificationTools

// ── warn hook (acknowledgement for tools with side effects beyond code modification) ──

let warnRequiredTools: Set<string> =
    Set.ofList [ "executor"; "pty_spawn"; "pty_write"; "pty_read"; "pty_list"; "pty_kill" ]

let isWarnRequiredTool (tool: string) : bool =
    Set.contains (tool.ToLowerInvariant()) warnRequiredTools

let warnDescription =
    "MUST acknowledge that this task cannot be done with other tools and only run tests when static analysis cannot handle it."

let warnTddDescription =
    "MUST acknowledge that tests are written first (TDD) and Kolmogorov discipline is followed."

// ── warn_reuse (acknowledgement for subagent tools that should not be dispatched via continue) ──

let subagentTools: Set<string> =
    Set.ofList [ "coder"; "inspector"; "meditator"; "browser" ]

let isSubagentTool (tool: string) : bool =
    Set.contains (tool.ToLowerInvariant()) subagentTools

let warnReuseDescription =
    "MUST acknowledge that this task is not suitable for completion via continue tool."
