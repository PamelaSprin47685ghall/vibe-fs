namespace Wanxiangshu.Mission.Relay.OpenCode

open Wanxiangshu.OpenCode

module SuicideTool =
    val admission: ToolAdmission
    val spec: factory: HostToolFactory -> scope: ToolRuntimeScope -> ToolSpec
