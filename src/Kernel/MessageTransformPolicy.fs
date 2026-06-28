module Wanxiangshu.Kernel.MessageTransformPolicy

let defaultExcludedAgents =
    set [ "browser"; "investigator"; "executor"; "title"; "compaction" ]

let childWorkspaceExcludedAgents = set [ "exec"; "explore" ]

let shouldExcludeAgentFromProjection (agent: string) (isChildWorkspace: bool) : bool =
    Set.contains agent defaultExcludedAgents
    || (isChildWorkspace && Set.contains agent childWorkspaceExcludedAgents)