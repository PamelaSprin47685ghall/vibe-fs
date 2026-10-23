namespace Wanxiangshu.OpenCode

open Wanxiangshu.Host
open Wanxiangshu.Persistence.Journal

module ResetScopeTool =
    val spec: factory: HostToolFactory -> journal: AgentJournal option -> ToolSpec
