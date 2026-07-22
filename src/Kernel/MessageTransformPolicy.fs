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
type CapsInjectionPolicy =
    | Include
    | Exclude

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

[<RequireQualifiedAccess>]
type ParallelHintPolicy =
    | Include
    | Exclude

let getParallelHintPolicy (agent: string) (isChildWorkspace: bool) : ParallelHintPolicy =
    let agent = normalizeAgent agent

    match agent with
    | "plan"
    | "title"
    | "compaction"
    | "exec"
    | "explore" -> ParallelHintPolicy.Exclude
    | _ ->
        if isChildWorkspace && (agent = "exec" || agent = "explore") then
            ParallelHintPolicy.Exclude
        else
            ParallelHintPolicy.Include
