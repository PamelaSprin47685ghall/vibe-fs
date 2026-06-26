module Wanxiangshu.Kernel.HostTools

type Host = Opencode | Mimocode | Mux | Omp

let opencode = Opencode
let mimocode = Mimocode
let mux = Mux
let omp = Omp

let todoWriteToolName = function
    | Opencode -> "todowrite"
    | Mimocode -> "task"
    | Mux -> "todowrite"
    | Omp -> "todowrite"

let todoWritePromptName = function
    | Opencode -> "todo_write"
    | Mimocode -> "task"
    | Mux -> "todo_write"
    | Omp -> "todo_write"

let taskToolName = function
    | Opencode -> "task"
    | Mimocode -> "actor"
    | Mux -> "task"
    | Omp -> "task"

let private opencodeAliases = [ "todo_write" ]
let private mimoAliases = [ "task" ]
let private muxAliases = [ "todo_write"; "todo_read" ]
let private ompAliases = [ "todo_write" ]

/// Map a Mux host tool name to the canonical name used by permission classification.
let normalizeToolNameForMux (toolName: string) : string =
    if toolName.StartsWith "file_edit_" then "edit"
    elif toolName = "file_read" then "read"
    elif toolName = "web_fetch" then "webfetch"
    elif toolName = "web_search" || toolName = "google_search" then "websearch"
    elif toolName = "todo_write" then todoWriteToolName Mux
    elif toolName = "todo_read" then "todo"
    elif toolName.StartsWith "agent_skill_" || toolName.StartsWith "skills_catalog_" then "skill"
    elif toolName = "ask_user_question" then "question"
    elif toolName.StartsWith "task" then "task"
    else toolName

let normalizeToolName (host: Host) (toolName: string) : string =
    match host, toolName with
    | Opencode, "todo_write" -> "todowrite"
    | Mimocode, "task" -> "todowrite"
    | Mimocode, "actor" -> "task"
    | Mux, _ -> normalizeToolNameForMux toolName
    | Omp, "todo_write" -> "todowrite"
    | _ -> toolName

let isTodoWriteToolName (toolName: string) : bool =
    toolName = todoWriteToolName Opencode
    || toolName = todoWriteToolName Mimocode
    || toolName = todoWriteToolName Mux
    || toolName = todoWriteToolName Omp
    || List.contains toolName opencodeAliases
    || List.contains toolName mimoAliases
    || List.contains toolName muxAliases
    || List.contains toolName ompAliases

/// Mux child-workspace spawn tool universe for `toolPolicy.disabledTools`.
let muxSpawnToolUniverse =
    [| "mux_agents_read"; "mux_agents_write"; "agent_skill_list"; "agent_skill_write"; "agent_skill_delete"
       "skills_catalog_search"; "skills_catalog_read"; "mux_config_read"; "mux_config_write"; "file_read"
       "attach_file"; "desktop_screenshot"; "desktop_move_mouse"; "desktop_click"; "desktop_double_click"
       "desktop_drag"; "desktop_scroll"; "desktop_type"; "desktop_key_press"; "agent_skill_read"
       "agent_skill_read_file"; "file_edit_replace_string"; "ask_user_question"; "propose_plan"; "bash"
       "task"; "task_await"; "task_apply_git_patch"; "task_terminate"; "task_list"; "agent_report"; "set_goal"
       "get_goal"; "complete_goal"; "heartbeat"; "todo_write"; "todo_read"; "review_pane_update"
       "review_pane_get"; "notify"; "analytics_query"; "web_fetch"; "web_search"; "google_search"; "url_context" |]

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