namespace Wanxiangshu.Execution.Delegation.Fork.Host

/// Join-admission owner surface. Permit validation observations are plain text;
/// the private FamilyRecoveryPermit and HostForkRuntime remain opaque.
[<RequireQualifiedAccess>]
module JoinSurface =
    val validatePermit:
        permitRoot: string ->
        permitSequence: int64 ->
        currentRoot: string ->
        currentSequence: int64 ->
        permitMembers: string array ->
        currentMembers: string array ->
            obj
