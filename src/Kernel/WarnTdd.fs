module Wanxiangshu.Kernel.WarnTdd

type WarnTdd = | IAmSureIHaveFollowedTddAndKolmolgorovPrinciples

let canonicalValue =
    "i-am-sure-i-have-followed-tdd-and-kolmolgorov-principles-and-kept-todo-updated"

let parseWarnTdd (s: string) : WarnTdd option =
    if s.ToLowerInvariant() = canonicalValue then
        Some IAmSureIHaveFollowedTddAndKolmolgorovPrinciples
    else
        None

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
          "pty_kill" ]

let isModificationTool (tool: string) : bool =
    Set.contains (tool.ToLowerInvariant()) modificationTools

// ── warn hook (acknowledgement for tools with side effects beyond code modification) ──

let warnCanonicalValue =
    "it-is-not-possible-to-do-it-using-other-tools-and-only-run-tests-when-static-analysis-cannot-handle-it"

let parseWarn (s: string) : bool = s = warnCanonicalValue

let warnRequiredTools: Set<string> =
    Set.ofList [ "executor"; "pty_spawn"; "pty_write"; "pty_read"; "pty_list"; "pty_kill" ]

let isWarnRequiredTool (tool: string) : bool =
    Set.contains (tool.ToLowerInvariant()) warnRequiredTools

let warnDescription =
    "Warning acknowledgement: '"
    + warnCanonicalValue
    + "' — acknowledge that this task cannot be done with other tools and only run tests when static analysis cannot handle it."

let warnTddDescription =
    "Warning acknowledgement: '"
    + canonicalValue
    + "' — acknowledge that tests are written first (TDD) and Kolmolgorov discipline is followed."

// ── warn_reuse (acknowledgement for subagent tools that should not be dispatched via continue) ──

let warnReuseCanonicalValue =
    "this-task-is-not-suitable-to-be-completed-via-continue-tool"

let parseWarnReuse (s: string) : bool =
    s.ToLowerInvariant().Trim() = warnReuseCanonicalValue

let subagentTools: Set<string> =
    Set.ofList [ "coder"; "investigator"; "meditator"; "browser" ]

let isSubagentTool (tool: string) : bool =
    Set.contains (tool.ToLowerInvariant()) subagentTools

let warnReuseDescription =
    "Warning acknowledgement: '"
    + warnReuseCanonicalValue
    + "' — acknowledge that this task is not suitable for completion via continue tool."
