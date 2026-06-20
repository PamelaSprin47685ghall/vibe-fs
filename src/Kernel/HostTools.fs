module VibeFs.Kernel.HostTools

type Host = Opencode | Mimocode

let opencode = Opencode
let mimocode = Mimocode

let todoWriteToolName = function
    | Opencode -> "todowrite"
    | Mimocode -> "task"

let todoWritePromptName = function
    | Opencode -> "todo_write"
    | Mimocode -> "task"

let taskToolName = function
    | Opencode -> "task"
    | Mimocode -> "actor"

let private opencodeAliases = [ "todo_write" ]
let private mimoAliases = [ "task" ]

let normalizeToolName (host: Host) (toolName: string) : string =
    match host, toolName with
    | Opencode, "todo_write" -> "todowrite"
    | Mimocode, "task" -> "todowrite"
    | Mimocode, "actor" -> "task"
    | _ -> toolName

let isTodoWriteToolName (toolName: string) : bool =
    toolName = todoWriteToolName Opencode
    || toolName = todoWriteToolName Mimocode
    || List.contains toolName opencodeAliases
    || List.contains toolName mimoAliases

let allToolNames (host: Host) : string array =
    [| "coder"; "investigator"; "meditator"; "browser"; "executor"
       "fuzzy_find"; "fuzzy_grep"; "websearch"; "webfetch"
       "fetch_wiki"; "submit_wiki"; "submit_review"; "return_reviewer"; "read"; "write"
       "bash"; taskToolName host; "grep"; "edit"; "patch"; "apply_patch"
       todoWriteToolName host; todoWritePromptName host; "stealth-browser-mcp_*"
       "question"; "ask_user_question"; "agent_report"
       "glob"; "skill" |]
    |> Array.distinct
