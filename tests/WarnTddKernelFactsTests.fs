module Wanxiangshu.Tests.WarnTddKernelFactsTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.WarnTdd

/// modificationTools / warnRequiredTools are the SSOT for the entire pipeline.
/// Any drift here MUST fail these tests loudly; downstream schema + hooks assume
/// the canonical names match these sets exactly.

let kernelWarnTddSet () =
    let expected =
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

    equal "modificationTools set" expected modificationTools

let kernelWarnRequiredSet () =
    let expected =
        Set.ofList [ "executor"; "pty_spawn"; "pty_write"; "pty_read"; "pty_list"; "pty_kill" ]

    equal "warnRequiredTools set" expected warnRequiredTools

let kernelIsModificationToolMatrix () =
    let cases =
        [ "coder", true
          "executor", true
          "write", true
          "edit", true
          "apply_patch", true
          "patch", true
          "ast_edit", true
          "ast_grep_replace", true
          "file_edit_replace_string", true
          "file_edit_insert", true
          "pty_spawn", true
          "pty_write", true
          "pty_read", true
          "pty_list", true
          "pty_kill", true
          "read", false
          "fuzzy_grep", false
          "fuzzy_find", false
          "webfetch", false
          "websearch", false
          "inspector", false
          "meditator", false
          "browser", false
          "submit_review", false
          "todowrite", false
          "methodology", false ]

    for (tool, expected) in cases do
        equal ("isModificationTool " + tool) expected (isModificationTool tool)

let kernelIsWarnRequiredToolMatrix () =
    let cases =
        [ "executor", true
          "pty_spawn", true
          "pty_write", true
          "pty_read", true
          "pty_list", true
          "pty_kill", true
          "coder", false
          "write", false
          "edit", false
          "apply_patch", false
          "patch", false
          "ast_edit", false
          "ast_grep_replace", false
          "file_edit_replace_string", false
          "file_edit_insert", false
          "read", false
          "fuzzy_grep", false
          "todowrite", false ]

    for (tool, expected) in cases do
        equal ("isWarnRequiredTool " + tool) expected (isWarnRequiredTool tool)

let kernelCanonicalValuesAreNonEmpty () =
    check "warnDescription non-empty" (warnDescription <> "")

let kernelWarnDescriptionsDiffer () =
    check "warnTddDescription non-empty" (warnTddDescription <> "")
    check "warnDescription and warnTddDescription differ" (warnDescription <> warnTddDescription)

    check
        "warnTddDescription contains 'TDD' or 'tdd'"
        (warnTddDescription.Contains("TDD") || warnTddDescription.Contains("tdd"))

    check "warnTddDescription contains 'Kolmogorov'" (warnTddDescription.Contains("Kolmogorov"))

let kernelIsSubagentToolMatrix () =
    let cases =
        [ "coder", true
          "inspector", true
          "meditator", true
          "browser", true
          "read", false
          "write", false
          "executor", false ]

    for (tool, expected) in cases do
        equal ("isSubagentTool " + tool) expected (isSubagentTool tool)

let kernelWarnReuseDescriptionNonEmpty () =
    check "warnReuseDescription non-empty" (warnReuseDescription <> "")

    check
        "warnReuseDescription contains subagent"
        (warnReuseDescription.Contains("subagent")
         || warnReuseDescription.Contains("continue"))

let run () =
    kernelWarnTddSet ()
    kernelWarnRequiredSet ()
    kernelIsModificationToolMatrix ()
    kernelIsWarnRequiredToolMatrix ()
    kernelCanonicalValuesAreNonEmpty ()
    kernelWarnDescriptionsDiffer ()
    kernelIsSubagentToolMatrix ()
    kernelWarnReuseDescriptionNonEmpty ()
