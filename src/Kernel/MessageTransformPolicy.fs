module Wanxiangshu.Kernel.MessageTransformPolicy

let defaultExcludedAgents =
    set [ "browser"; "inspector"; "executor"; "title"; "compaction" ]

let childWorkspaceExcludedAgents = set [ "exec"; "explore" ]

let normalizeAgent (agent: string) : string =
    if isNull agent then "" else agent.Trim().ToLowerInvariant()

let shouldExcludeAgentFromProjection (agent: string) (isChildWorkspace: bool) : bool =
    let agent = normalizeAgent agent

    Set.contains agent defaultExcludedAgents
    || (isChildWorkspace && Set.contains agent childWorkspaceExcludedAgents)

[<RequireQualifiedAccess>]
type BacklogProjectionPolicy =
    | Include
    | Exclude

[<RequireQualifiedAccess>]
type CapsInjectionPolicy =
    | Include
    | Exclude

[<RequireQualifiedAccess>]
type ParallelHintPolicy =
    | Include
    | Exclude

[<RequireQualifiedAccess>]
type ContextBudgetPolicy =
    | Include
    | DisableTodoEmergency
    | Disable

let getBacklogProjectionPolicy (agent: string) (isChildWorkspace: bool) : BacklogProjectionPolicy =
    let agent = normalizeAgent agent

    if shouldExcludeAgentFromProjection agent isChildWorkspace then
        BacklogProjectionPolicy.Exclude
    else
        BacklogProjectionPolicy.Include

let getCapsInjectionPolicy (agent: string) (isChildWorkspace: bool) : CapsInjectionPolicy =
    let agent = normalizeAgent agent

    match agent with
    | "browser"
    | "executor"
    | "title"
    | "compaction"
    | "exec"
    | "explore" -> CapsInjectionPolicy.Exclude
    | _ ->
        if isChildWorkspace && (agent = "exec" || agent = "explore") then
            CapsInjectionPolicy.Exclude
        else
            CapsInjectionPolicy.Include

let getParallelHintPolicy (agent: string) : ParallelHintPolicy =
    match normalizeAgent agent with
    | "title"
    | "compaction" -> ParallelHintPolicy.Exclude
    | _ -> ParallelHintPolicy.Include

let getContextBudgetPolicy (agent: string) (isChildWorkspace: bool) : ContextBudgetPolicy =
    let agent = normalizeAgent agent

    match agent with
    | "browser"
    | "executor"
    | "title"
    | "compaction"
    | "exec"
    | "explore" -> ContextBudgetPolicy.Disable
    | "inspector"
    | "reviewer" -> ContextBudgetPolicy.DisableTodoEmergency
    | _ ->
        if isChildWorkspace && (agent = "exec" || agent = "explore") then
            ContextBudgetPolicy.Disable
        else
            ContextBudgetPolicy.Include
