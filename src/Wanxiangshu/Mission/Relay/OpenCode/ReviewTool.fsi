namespace Wanxiangshu.Mission.Relay.OpenCode

open Wanxiangshu.OpenCode

module ReviewTool =
    val admission: ToolAdmission
    val spec: factory: HostToolFactory -> scope: ToolRuntimeScope -> ToolSpec

