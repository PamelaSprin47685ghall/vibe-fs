module Wanxiangshu.Tests.OmpSessionToolsTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.OmpSessionTools

let mainSessionStripsChildOnlyAndBash () =
    let active =
        [| "read"
           "edit"
           "write"
           "find"
           "fuzzy_find"
           "fuzzy_grep"
           "lsp"
           "browser"
           "search"
           "glob"
           "bash"
           "coder"
           "investigator"
           "meditator"
           "browser"
           "executor"
           "executor_wait"
           "executor_abort"
           "submit_review"
           "return_reviewer"
           "websearch"
           "webfetch"
           "todowrite" |]

    let filtered = filterOmpMainSessionActiveTools active
    let set = Set.ofArray filtered
    check "keeps read" (set.Contains "read")
    check "keeps coder" (set.Contains "coder")
    check "strips bash" (not (set.Contains "bash"))
    check "strips fuzzy_find" (not (set.Contains "fuzzy_find"))
    check "strips return_reviewer" (not (set.Contains "return_reviewer"))
    check "strips browser child-only" (not (set.Contains "browser"))

let childSessionKeepsChildTools () =
    let childOnly =
        [| "read"; "edit"; "write"; "find"; "fuzzy_find"; "return_reviewer" |]

    let filtered = filterOmpMainSessionActiveTools childOnly
    let set = Set.ofArray filtered
    check "child keeps edit" (set.Contains "edit")
    check "child keeps fuzzy_find" (set.Contains "fuzzy_find")
    check "child still strips bash" (not (set.Contains "bash"))
