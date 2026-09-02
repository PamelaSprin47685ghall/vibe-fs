namespace Wanxiangshu.Mission.Finality

/// GLORY-052/076 + §9.2.2–9.2.4 + SURFACE-004: Finality experience prompt owner.
/// Prose meaning lives in `resources/provider/lifecycle/finality/**` (PROMPT-019).
module FinalityPrompt =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        val Rejected: string = "lifecycle/finality/rejected"

        [<Literal>]
        val Blessed: string = "lifecycle/finality/blessed"

        [<Literal>]
        val Rest: string = "lifecycle/finality/rest"

        [<Literal>]
        val Steer: string = "lifecycle/finality/steer"

        [<Literal>]
        val SteerUnavailable: string = "lifecycle/finality/steer-unavailable"

    val blessedFromLogs: blessingHeaderInstructions: string list -> logs: (int * string) list -> string

    val blessed: blessingHeaderInstructions: string list -> workRecordBundle: string -> string

    val rejected: rejectionHeaderInstructions: string list -> reviewerWorkRecord: string -> string

    val steer: steerHeaderInstructions: string list -> siblingWorkRecord: string -> string
