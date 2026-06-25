module VibeFs.Kernel.HostTools

type Host = Opencode | Mimocode | Omp

let opencode = Opencode
let mimocode = Mimocode
let omp = Omp

let todoWriteToolName = function
    | Opencode -> "todowrite"
    | Mimocode -> "task"
    | Omp -> "todowrite"

let todoWritePromptName = function
    | Opencode -> "todo_write"
    | Mimocode -> "task"
    | Omp -> "todo_write"

let taskToolName = function
    | Opencode -> "task"
    | Mimocode -> "actor"
    | Omp -> "task"

let private opencodeAliases = [ "todo_write" ]
let private mimoAliases = [ "task" ]
let private ompAliases = [ "todo_write" ]

let normalizeToolName (host: Host) (toolName: string) : string =
    match host, toolName with
    | Opencode, "todo_write" -> "todowrite"
    | Mimocode, "task" -> "todowrite"
    | Mimocode, "actor" -> "task"
    | Omp, "todo_write" -> "todowrite"
    | _ -> toolName

let isTodoWriteToolName (toolName: string) : bool =
    toolName = todoWriteToolName Opencode
    || toolName = todoWriteToolName Mimocode
    || toolName = todoWriteToolName Omp
    || List.contains toolName opencodeAliases
    || List.contains toolName mimoAliases
    || List.contains toolName ompAliases

let allToolNames (host: Host) : string array =
    [| "coder"; "investigator"; "meditator"; "browser"; "executor"
       "executor_wait"; "executor_abort"
       "fuzzy_find"; "fuzzy_grep"; "websearch"; "webfetch"
       "knowledge_graph_fetch"; "return_bookkeeper"; "submit_review"; "return_reviewer"; "read"; "write"
       "bash"; taskToolName host; "grep"; "edit"; "patch"; "apply_patch"
       todoWriteToolName host; todoWritePromptName host; "stealth-browser-mcp_*"
       "question"; "ask_user_question"; "agent_report"
       "glob"; "skill" |]
    |> Array.distinct
