module Wanxiangshu.Kernel.OmpSessionTools

let ompSubagentToolNames = [| "coder"; "inspector"; "meditator"; "browser" |]

let ompReviewChildToolNames = [| "read"; "return_reviewer" |]

let ompRunnerChildToolNames = [||]

let ompChildOnlyToolNames =
    [| "find"
       "edit"
       "write"
       "lsp"
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

    let isMainSession = ompSubagentToolNames |> Array.exists activeSet.Contains

    let withoutAlwaysStrip =
        tools |> Array.filter (fun name -> not (alwaysStripSet.Contains name))

    if isMainSession then
        withoutAlwaysStrip
        |> Array.filter (fun name -> not (childOnlySet.Contains name))
    else
        withoutAlwaysStrip

let isChildOnlyTool (name: string) : bool = Set.contains name childOnlySet
