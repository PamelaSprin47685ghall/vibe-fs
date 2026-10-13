namespace Wanxiangshu.Mission.Relay.OpenCode

open Wanxiangshu.OpenCode

module DeliverTool =
    val description: unit -> string
    val admission: ToolAdmission
    val spec: factory: HostToolFactory -> scope: ToolRuntimeScope -> ToolSpec
