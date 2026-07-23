module Wanxiangshu.Kernel.HostTools

/// Injected by Runtime/Hosts at process start. Kernel never reads process.env.
let mutable private e2eSandbox = false

let setE2eSandbox (value: bool) : unit = e2eSandbox <- value

let isE2eSandbox () : bool = e2eSandbox

type Host =
    | Opencode
    | Mimocode
    | Mux
    | Omp

let opencode = Opencode
let mimocode = Mimocode
let mux = Mux
let omp = Omp

let todoWriteToolName (host: Host) : string =
    if e2eSandbox then
        "todowrite"
    else
        match host with
        | Opencode -> "todowrite"
        | Mimocode -> "task"
        | Mux -> "todowrite"
        | Omp -> "todowrite"

let todoWritePromptName (host: Host) : string =
    if e2eSandbox then
        "todo_write"
    else
        match host with
        | Opencode -> "todo_write"
        | Mimocode -> "task"
        | Mux -> "todo_write"
        | Omp -> "todo_write"

let taskToolName (host: Host) : string =
    if e2eSandbox then
        "task"
    else
        match host with
        | Opencode -> "task"
        | Mimocode -> "actor"
        | Mux -> "task"
        | Omp -> "task"

let private opencodeAliases = [ "todo_write" ]
let private mimoAliases = [ "task" ]
let private muxAliases = [ "todo_write"; "todo_read" ]
let private ompAliases = [ "todo_write" ]

/// Recomputed so setE2eSandbox after process start still affects membership.
let private todoWriteLookup () : Set<string> =
    Set.ofList
        [ todoWriteToolName Opencode
          todoWriteToolName Mimocode
          todoWriteToolName Mux
          todoWriteToolName Omp
          yield! opencodeAliases
          yield! mimoAliases
          yield! muxAliases
          yield! ompAliases ]

/// Map a Mux host tool name to the canonical name used by permission classification.
let normalizeToolNameForMux (toolName: string) : string =
    if toolName.StartsWith "file_edit_" then
        "edit"
    elif toolName = "file_read" then
        "read"
    elif toolName = "web_fetch" then
        "webfetch"
    elif toolName = "web_search" || toolName = "google_search" then
        "websearch"
    elif toolName = "todo_write" then
        todoWriteToolName Mux
    elif toolName = "todo_read" then
        "todo"
    elif toolName.StartsWith "agent_skill_" || toolName.StartsWith "skills_catalog_" then
        "skill"
    elif toolName = "ask_user_question" then
        "question"
    elif toolName = "task_complete" then
        "task_complete"
    elif toolName.StartsWith "task" then
        "task"
    else
        toolName

let normalizeToolName (host: Host) (toolName: string) : string =
    match host, toolName with
    | Opencode, "web_search" -> "websearch"
    | Mimocode, "web_search" -> "websearch"
    | Opencode, "todo_write" -> "todowrite"
    | Mimocode, "task" -> "todowrite"
    | Mimocode, "actor" -> "task"
    | Mux, _ -> normalizeToolNameForMux toolName
    | Omp, "todo_write" -> "todowrite"
    | _ -> toolName

let isTodoWriteToolName (toolName: string) : bool =
    Set.contains toolName (todoWriteLookup ())

/// Mux child-workspace spawn tool universe for `toolPolicy.disabledTools`.
let muxSpawnToolUniverse =
    [| "mux_agents_read"
       "mux_agents_write"
       "agent_skill_list"
       "agent_skill_write"
       "agent_skill_delete"
       "skills_catalog_search"
       "skills_catalog_read"
       "mux_config_read"
       "mux_config_write"
       "file_read"
       "attach_file"
       "desktop_screenshot"
       "desktop_move_mouse"
       "desktop_click"
       "desktop_double_click"
       "desktop_drag"
       "desktop_scroll"
       "desktop_type"
       "desktop_key_press"
       "agent_skill_read"
       "agent_skill_read_file"
       "file_edit_replace_string"
       "ask_user_question"
       "propose_plan"
       "bash"
       "task"
       "task_await"
       "task_apply_git_patch"
       "task_terminate"
       "task_list"
       "agent_report"
       "set_goal"
       "get_goal"
       "complete_goal"
       "heartbeat"
       "todo_write"
       "todo_read"
       "review_pane_update"
       "review_pane_get"
       "notify"
       "analytics_query"
       "web_fetch"
       "web_search"
       "google_search"
       "url_context" |]

/// Synthetic callID prefixes injected by the host (Semble search, caps
/// project-file reads). These are never the LLM's own decision and must not
/// trigger the parallel-tool hint.
let synthCallIdPrefixes: Set<string> = Set.ofList [ "semble-call-"; "caps-call-"; "rd-" ]

let isSynthCallId (callID: string) : bool =
    synthCallIdPrefixes |> Set.exists callID.StartsWith

let allToolNames (host: Host) : string array =
    [| "coder"
       "inspector"
       "meditator"
       "browser"
       "executor"
       "pty_spawn"
       "pty_write"
       "pty_read"
       "pty_list"
       "pty_kill"
       "fuzzy_find"
       "fuzzy_grep"
       "fuzzy_continue"
       "websearch"
       "webfetch"
       "submit_review"
       "return_reviewer"
       "read"
       "write"
       "bash"
       taskToolName host
       "grep"
       "edit"
       "patch"
       "apply_patch"
       todoWriteToolName host
       todoWritePromptName host
       "stealth-browser-mcp_*"
       "question"
       "ask_user_question"
       "agent_report"
       "glob"
       "skill" |]
    |> Array.distinct
