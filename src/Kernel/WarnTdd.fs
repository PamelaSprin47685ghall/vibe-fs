module Wanxiangshu.Kernel.WarnTdd

type WarnTdd =
    | IAmSureIHaveFollowedTddAndKolmolgorovPrinciples

let canonicalValue = "i-am-sure-i-have-followed-tdd-and-kolmolgorov-principles"

let parseWarnTdd (s: string) : WarnTdd option =
    if s.ToLowerInvariant() = canonicalValue then
        Some IAmSureIHaveFollowedTddAndKolmolgorovPrinciples
    else None

/// Tools where warn_tdd is enforced — all tools that can modify code.
let modificationTools: Set<string> =
    Set.ofList
        [ "coder"; "executor"; "write"; "edit"; "apply_patch"; "patch"
          "ast_edit"; "ast_grep_replace"; "file_edit_replace_string"; "file_edit_insert" ]

let isModificationTool (tool: string) : bool =
    Set.contains (tool.ToLowerInvariant()) modificationTools
