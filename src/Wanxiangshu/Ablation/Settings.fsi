namespace Wanxiangshu.Ablation

[<RequireQualifiedAccess>]
module AblationSettings =
    val load: unit -> Result<AblationRegistry, AblationLoadError>
    val current: unit -> AblationRegistry
    val resetCache: unit -> unit
    val speculativeInvestigationMode: unit -> AblationMode
    val strengthForcedOff: unit -> bool
    val allowsTool: toolName: string -> bool
    val allowsToolSchema: toolName: string -> bool
    val allowsPrimaryAgent: agentName: string -> bool
    val fissionVisible: unit -> bool
    val allowsFactTag: factTag: string -> bool
