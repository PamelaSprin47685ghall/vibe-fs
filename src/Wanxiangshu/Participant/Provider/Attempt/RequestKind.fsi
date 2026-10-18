namespace Wanxiangshu.Participant.Provider.Attempt

[<RequireQualifiedAccess>]
type ProviderRequestKind =
    | WorkMain
    | BloggerMain
    | InteractionRepair
    | StrengthReplica

[<RequireQualifiedAccess>]
module ProviderRequestKind =
    val label: kind: ProviderRequestKind -> string
    val clearsFailureCountOnSuccess: kind: ProviderRequestKind -> bool
    val mayCarryProbe: kind: ProviderRequestKind -> bool
