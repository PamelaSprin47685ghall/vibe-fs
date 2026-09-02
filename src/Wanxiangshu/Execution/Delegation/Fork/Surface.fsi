namespace Wanxiangshu.Execution.Delegation.Fork

/// JS-native semantic surface for fork child payload (P3 pilot).
module ForkChildPayloadSurface =

    val instructions:
        lang: string ->
            {| Base: string array
               CommissionerRecord: string
               Attachment: string
               Requirements: string |}

    /// Render unknown/unavailable calling prose without exposing binding names.
    val unavailableCalling: lang: string -> orchestrator: bool -> string

    /// Render one fork child payload document from JSON-shaped input.
    val render:
        lang: string ->
        input:
            {| Assignment: string
               CommissionerRecord: string option
               Attachment: string option
               RootRequirements: string array
               Payload: string option |} ->
            string

    val chooseRoad: calling: string -> byname: string -> charge: string -> obj

    val reuseBinding:
        byname: string -> boundAgent: string -> requestedAgent: string -> tier: string -> charge: string -> obj
