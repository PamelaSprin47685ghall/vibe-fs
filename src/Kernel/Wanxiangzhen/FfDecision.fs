module Wanxiangshu.Kernel.Wanxiangzhen.FfDecision

open Wanxiangshu.Kernel.Wanxiangzhen.SquadTask

type FfResult =
    | Merged of masterSha: string
    | RebaseNeeded of masterSha: string
    | StaleCommit
    | CoordinatorNotReady of reason: string
    | NotSubmittable of currentStatus: SquadTaskStatus

type SubmitOutcome =
    | Response of FfResult
    | TaskNotFound
    | CoordinatorUnreachable
    | Unauthorized
    | LocalGitError of message: string

let ffResultLabel (r: FfResult) : string =
    match r with
    | Merged _ -> "merged"
    | RebaseNeeded _ -> "rebase_needed"
    | StaleCommit -> "stale_commit"
    | CoordinatorNotReady _ -> "coordinator_not_ready"
    | NotSubmittable _ -> "not_submittable"

let formatSubmitOutcome (masterBranch: string) (o: SubmitOutcome) : string =
    match o with
    | Response(Merged sha) -> sprintf "Merged into %s @ %s. Task complete. You may stop." masterBranch sha
    | Response(RebaseNeeded sha) ->
        sprintf
            "Cannot fast-forward. %s moved to %s. Run: git rebase %s. Resolve conflicts, re-run review if applicable, then call submit_to_squad again."
            masterBranch
            sha
            masterBranch
    | Response StaleCommit ->
        "Branch HEAD differs from reported commit. Commit your latest work, then call submit_to_squad again."
    | Response(CoordinatorNotReady _) ->
        sprintf "Coordinator not ready (not on %s or dirty). Wait and call submit_to_squad again shortly." masterBranch
    | Response(NotSubmittable s) ->
        sprintf "Task no longer submittable (status: %s). Report to user and stop." (statusToString s)
    | TaskNotFound ->
        "Task not found on coordinator. The coordinator may have restarted and lost state. Report to user and stop."
    | CoordinatorUnreachable -> "Coordinator unreachable (crashed or port changed). Report to user and wait."
    | Unauthorized ->
        "Authentication failed (token mismatch). The coordinator may have restarted. Report to user and stop."
    | LocalGitError msg -> sprintf "Local git error: %s. Fix the git issue and call submit_to_squad again." msg
