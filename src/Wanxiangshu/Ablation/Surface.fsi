namespace Wanxiangshu.Ablation

module AblationSurface =
    val resetCache: unit -> unit
    val load: unit -> obj
    val modeFor: nodeId: string -> string
    val allowsTool: toolName: string -> bool
    val allowsToolSchema: toolName: string -> bool
    val allowsPrimaryAgent: agentName: string -> bool
    val strengthForcedOff: unit -> bool
    val fissionVisible: unit -> bool
    val toolMapEntry: toolName: string -> string
    val allowsFact: factTag: string -> bool
    val factMapEntry: factTag: string -> string
    val manifestNodeIds: unit -> string array
    val profileIds: unit -> string array
