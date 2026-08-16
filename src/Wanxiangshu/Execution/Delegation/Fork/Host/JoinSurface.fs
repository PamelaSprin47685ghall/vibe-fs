namespace Wanxiangshu.Execution.Delegation.Fork.Host

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Execution.Session.Recovery

/// Join-admission owner surface. Permit validation observations are plain text;
/// the private FamilyRecoveryPermit and HostForkRuntime remain opaque.
[<RequireQualifiedAccess>]
module JoinSurface =
    let validatePermit
        (permitRoot: string)
        (permitSequence: int64)
        (currentRoot: string)
        (currentSequence: int64)
        (permitMembers: string array)
        (currentMembers: string array)
        : obj =
        if permitRoot <> currentRoot then
            box {| ok = false; error = sprintf "family recovery permit root mismatch: permit=%s runtime=%s" permitRoot currentRoot |}
        elif permitSequence > currentSequence then
            box {| ok = false; error = sprintf "family recovery permit journalSequence stale: permit=%d" permitSequence |}
        else
            let missing = Set.difference (Set.ofArray permitMembers) (Set.ofArray currentMembers) |> Set.toArray
            if missing.Length > 0 then
                box {| ok = false; error = sprintf "closure lost members: missing=%s" (String.concat "," missing) |}
            else
                box {| ok = true; error = "NothingToJoin" |}
