module Wanxiangshu.Kernel.MessageTransformPolicy

let defaultExcludedAgents =
    set [ "browser"; "investigator"; "executor"; "title"; "compaction" ]

let childWorkspaceExcludedAgents = set [ "exec"; "explore" ]

let shouldExcludeAgentFromProjection (agent: string) (isChildWorkspace: bool) : bool =
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
    if shouldExcludeAgentFromProjection agent isChildWorkspace then
        BacklogProjectionPolicy.Exclude
    else
        BacklogProjectionPolicy.Include

let getCapsInjectionPolicy (agent: string) (isChildWorkspace: bool) : CapsInjectionPolicy =
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

let getParallelHintPolicy (agent: string) (isChildWorkspace: bool) : ParallelHintPolicy =
    match agent with
    | "browser"
    | "executor"
    | "title"
    | "compaction"
    | "exec"
    | "explore" -> ParallelHintPolicy.Exclude
    | _ ->
        if isChildWorkspace && (agent = "exec" || agent = "explore") then
            ParallelHintPolicy.Exclude
        else
            ParallelHintPolicy.Include

let getContextBudgetPolicy (agent: string) (isChildWorkspace: bool) : ContextBudgetPolicy =
    match agent with
    | "browser"
    | "executor"
    | "title"
    | "compaction"
    | "exec"
    | "explore" -> ContextBudgetPolicy.Disable
    | "investigator"
    | "reviewer" -> ContextBudgetPolicy.DisableTodoEmergency
    | _ ->
        if isChildWorkspace && (agent = "exec" || agent = "explore") then
            ContextBudgetPolicy.Disable
        else
            ContextBudgetPolicy.Include
