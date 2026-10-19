namespace Wanxiangshu.Participant.Provider.Attempt

/// PROMPT-008: which physical request this is.
///
/// Real request semantics, not a flow stage. Each case determines which projection
/// to build, which instruction to send, and what success or failure does to the cursor.
[<RequireQualifiedAccess>]
type ProviderRequestKind =
    | WorkMain
    | BloggerMain
    | InteractionRepair
    | StrengthReplica

[<RequireQualifiedAccess>]
module ProviderRequestKind =

    let label (kind: ProviderRequestKind) =
        match kind with
        | ProviderRequestKind.WorkMain -> "work-main"
        | ProviderRequestKind.BloggerMain -> "blogger-main"
        | ProviderRequestKind.InteractionRepair -> "interaction-repair"
        | ProviderRequestKind.StrengthReplica -> "strength-replica"

    let clearsFailureCountOnSuccess (kind: ProviderRequestKind) =
        match kind with
        | ProviderRequestKind.WorkMain
        | ProviderRequestKind.BloggerMain -> true
        | ProviderRequestKind.InteractionRepair
        | ProviderRequestKind.StrengthReplica -> false

    let mayCarryProbe (kind: ProviderRequestKind) =
        match kind with
        | ProviderRequestKind.WorkMain -> true
        | ProviderRequestKind.BloggerMain
        | ProviderRequestKind.InteractionRepair
        | ProviderRequestKind.StrengthReplica -> false
