module VibeFs.Kernel.OmpSessionTools

let ompSubagentToolNames =
    [| "coder"; "investigator"; "meditator"; "browser" |]

let ompReviewChildToolNames = [| "read"; "return_reviewer" |]

let ompRunnerChildToolNames = [| "executor_wait"; "executor_abort" |]

let ompChildOnlyToolNames =
    [| "find"
       "edit"
       "write"
       "lsp"
       "fuzzy_find"
       "fuzzy_grep"
       "executor_wait"
       "executor_abort"
       "return_reviewer"
       "search"
       "glob"
       "ast_edit"
       "ast_grep"
       "browser" |]

let ompAlwaysStripToolNames = [| "bash" |]

let private childOnlySet = Set.ofArray ompChildOnlyToolNames
let private alwaysStripSet = Set.ofArray ompAlwaysStripToolNames

let filterOmpMainSessionActiveTools (activeTools: string seq) : string array =
    let tools = activeTools |> Seq.toArray
    let activeSet = Set.ofArray tools

    let isMainSession =
        ompSubagentToolNames |> Array.exists activeSet.Contains

    let withoutAlwaysStrip =
        tools |> Array.filter (fun name -> not (alwaysStripSet.Contains name))

    if isMainSession then
        withoutAlwaysStrip |> Array.filter (fun name -> not (childOnlySet.Contains name))
    else
        withoutAlwaysStrip