namespace Wanxiangshu.Change.Host

open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Resources
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence

open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Change

/// Remove worktrees and branches no active ManagerJob owns.
///
/// Cleanup only. It never derives a recovery action from what it finds on disk —
/// ORCH-007 forbids substituting filesystem state for a durable fact, and the
/// active-job set here comes from the projection.
module OrchestratorSweep =
    let private removeOne git path (cont: Task<Result<unit, string>>) =
        task {
            match! git.RemoveWorktree path with
            | Ok() -> return! cont
            | Error error ->
                return Error(sprintf "stale worktree cleanup failed for %s: %s" (WorktreePath.value path) error)
        }

    let private removeWorktrees git paths =
        let rec loop remaining =
            task {
                match remaining with
                | [] -> return Ok()
                | path :: tail -> return! removeOne git path (loop tail)
            }

        loop paths

    let private deleteOne git identity (cont: Task<Result<unit, string>>) =
        task {
            match! git.DeleteBranch identity with
            | Ok() -> return! cont
            | Error error ->
                return
                    Error(
                        sprintf
                            "stale manager branch cleanup failed for %s: %s"
                            (WorktreeIdentity.value identity)
                            error
                    )
        }

    let private deleteBranches git identities =
        let rec loop remaining =
            task {
                match remaining with
                | [] -> return Ok()
                | identity :: tail -> return! deleteOne git identity (loop tail)
            }

        loop identities

    /// `git worktree list` reports a branch as `refs/heads/manager/<job>`, while
    /// `git branch --list` reports it as `manager/<job>`. Both name the same
    /// identity, so stripping the ref prefix is the only normalisation needed.
    ///
    /// The job id is never parsed back out. The previous version cut at
    /// `refs/heads/manager/` and at the first `/`, reconstructing an id to compare
    /// against a set of ids — two different string surgeries for one question, and
    /// ORCH-006 makes the identity opaque precisely so it is compared whole.
    let private normalize (identity: WorktreeIdentity) =
        let value = WorktreeIdentity.value identity
        let prefix = "refs/heads/"

        if value.StartsWith prefix then
            WorktreeIdentity.create (value.Substring prefix.Length)
        else
            identity

    let private afterRemoveWorktrees
        (git: GitPort)
        (isStale: WorktreeIdentity -> bool)
        : Task<Result<unit, string>> =
        task {
            match! git.ListManagerBranches() with
            | Error error -> return Error(sprintf "cannot list manager branches for cleanup: %s" error)
            | Ok branches -> return! deleteBranches git (branches |> List.filter isStale)
        }

    let private afterListWorktrees
        (git: GitPort)
        (isManagerBranch: WorktreeIdentity -> bool)
        (isStale: WorktreeIdentity -> bool)
        (entries: (WorktreePath * WorktreeIdentity option) list)
        : Task<Result<unit, string>> =
        task {
            let staleWorktrees =
                entries
                |> List.choose (fun (path, identity) ->
                    match identity with
                    | Some value when isManagerBranch value && isStale value -> Some path
                    | _ -> None)

            match! removeWorktrees git staleWorktrees with
            | Error error -> return Error error
            | Ok() -> return! afterRemoveWorktrees git isStale
        }

    let sweepStaleArtifacts (git: GitPort) (activeJobs: ManagerJobProjection list) : Task<Result<unit, string>> =
        task {
            let owned = activeJobs |> List.map (fun job -> job.WorktreeIdentity) |> Set.ofList

            let isStale identity =
                not (Set.contains (normalize identity) owned)

            // Only manager-branch worktrees are ours to clean. The MAIN working tree
            // carries `branch refs/heads/main` in `worktree list --porcelain`, so a
            // stale test alone would classify the repository itself as stale and
            // `git worktree remove` would fail with "is a main working tree" —
            // measured on Host 1.18.9: every orchestrator canary died in init.
            let isManagerBranch identity =
                let value = WorktreeIdentity.value identity
                value.StartsWith "refs/heads/manager/" || value.StartsWith "manager/"

            match! git.ListWorktrees() with
            | Error error -> return Error(sprintf "cannot list worktrees for cleanup: %s" error)
            | Ok entries -> return! afterListWorktrees git isManagerBranch isStale entries
        }

    let sweepLocked
        (lockPath: string)
        (git: GitPort)
        (activeJobs: ManagerJobProjection list)
        : Task<Result<unit, string>> =
        task {
            let! gate = IntegrationGate.acquire lockPath

            let! outcome =
                task {
                    try
                        return! sweepStaleArtifacts git activeJobs
                    with ex ->
                        return Error ex.Message
                }

            do! gate.Release()
            return outcome
        }
