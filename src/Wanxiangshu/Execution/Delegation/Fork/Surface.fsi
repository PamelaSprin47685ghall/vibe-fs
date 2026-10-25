namespace Wanxiangshu.Execution.Delegation.Fork

/// JS-native semantic surface for fork child payload (P3 pilot).
module ForkChildPayloadSurface =

    val instructions:
        lang: string ->
            {| Base: string array
               Requirements: string |}

    val unavailableCalling: lang: string -> orchestrator: bool -> string

    val render:
        lang: string ->
        input:
            {| Assignment: string
               RootRequirements: string array |} ->
            string

    val chooseWork: calling: string -> byname: string -> charge: string -> obj

    val reuseBinding:
        byname: string -> boundAgent: string -> requestedAgent: string -> tier: string -> charge: string -> obj
