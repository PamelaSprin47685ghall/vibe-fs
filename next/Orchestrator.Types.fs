namespace Wanxiangshu.Next.Orchestrator

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal

type OrchestratorVerdict =
    | Published of managerId: string * headCommit: string
    | RejectedDirty of reason: string
    | NeedsReview of managerId: string * reviewDetails: string
    | IntegrationFailed of managerId: string * errorDetails: string
    | Empty

type OrchestratorHandle =
    { ManagerId: string
      WorktreePath: string }

type ManagerCompletion =
    { Handle: OrchestratorHandle
      Result: Result<unit, string> }

type GitPort =
    { IsDirty: string -> Task<bool>
      CreateWorktree: string -> string -> string -> Task<Result<unit, string>>
      Rebase: string -> string -> Task<Result<unit, string>>
      FfMerge: string -> string -> Task<Result<string, string>>
      ConflictedFiles: string -> Task<Result<string list, string>>
      RemoveWorktree: string -> Task<Result<unit, string>> }

type ManagerPort =
    { RunManager: string -> string -> string -> Task<Result<unit, string>>
      Reverify: string -> string -> Task<Result<unit, string>> }

type OrchestratorJournalPort =
    { AppendFact: StreamId -> AgentFact -> Result<ProjectionSet, string>
      Snapshot: unit -> ProjectionSet }

module OrchestratorJournalPort =
    let fromAgentJournal (journal: AgentJournal) : OrchestratorJournalPort =
        { AppendFact =
            fun stream fact ->
                match AgentJournal.appendAgent stream None fact journal with
                | Ok projection -> Ok projection
                | Error failure -> Error(sprintf "%A" failure.Failure)
          Snapshot = fun () -> AgentJournal.snapshot journal }

type GitAuthorityPort =
    { GetHead: string -> Task<Result<string, string>>
      GetTargetHead: string -> string -> Task<Result<string, string>> }

/// Pure publish-chain pipeline: given the Manager completion and the Orchestrator's
/// dependency surface, run rebase/review/ff/publish. Lives here (compiles before
/// Orchestrator.fs) so Orchestrator.JoinPublished can call it directly (no runtime
/// bridge), while it still references GitPort/ManagerPort/OrchestratorVerdict etc.
module PublishChain =
    type Deps =
        { Git: GitPort
          Manager: ManagerPort
          AppendFact: StreamId -> AgentFact -> Result<unit, string>
          ReverifyTwice: string -> string -> Task<Result<unit, string>>
          ReadHead: string -> string -> Task<Result<string, string>>
          ReconcileTarget: unit -> Task<Result<unit, string>>
          TargetBranch: string
          Prompts: Dictionary<string, string> }

    let run (deps: Deps) (completion: ManagerCompletion) : Task<OrchestratorVerdict> =
        task {
            let managerId = completion.Handle.ManagerId
            let worktreePath = completion.Handle.WorktreePath

            match! deps.ReconcileTarget() with
            | Error err ->
                return OrchestratorVerdict.IntegrationFailed(managerId, sprintf "Git reconcile failed: %s" err)
            | Ok() ->
                match! deps.ReverifyTwice managerId worktreePath with
                | Error err -> return OrchestratorVerdict.NeedsReview(managerId, err)
                | Ok() ->
                    let candidateId = sprintf "candidate-%s" managerId
                    let! candidateHeadResult = deps.ReadHead worktreePath ""

                    match candidateHeadResult with
                    | Error err ->
                        return
                            OrchestratorVerdict.IntegrationFailed(managerId, sprintf "Git head lookup failed: %s" err)
                    | Ok candidateHead ->
                        match
                            deps.AppendFact
                                (StreamId.Workspace)
                                (AgentFact.OrchestratorCandidateRegistered
                                    {| ManagerId = managerId
                                       CandidateId = candidateId
                                       Branch = sprintf "manager/%s" managerId
                                       CommitHash = candidateHead |})
                        with
                        | Error err ->
                            return
                                OrchestratorVerdict.IntegrationFailed(
                                    managerId,
                                    sprintf "Failed to persist candidate: %s" err
                                )
                        | Ok() ->
                            let! rebaseResult = deps.Git.Rebase worktreePath deps.TargetBranch

                            let! finalRebase =
                                match rebaseResult with
                                | Ok() -> Task.FromResult(Ok())
                                | Error conflict ->
                                    task {
                                        // A conflict is a continuation of this ManagerJob, never a new manager.
                                        // Resume the SAME Manager session with the original prompt plus a structured
                                        // conflict-continuation context; do not restart the original task.
                                        let! conflicted = deps.Git.ConflictedFiles worktreePath

                                        let conflictList =
                                            match conflicted with
                                            | Ok files when not (List.isEmpty files) -> String.concat "\n  " files
                                            | _ -> "<unable to enumerate conflicted files>"

                                        let basePrompt =
                                            match deps.Prompts.TryGetValue managerId with
                                            | true, saved -> saved
                                            | false, _ -> ""

                                        let prompt =
                                            sprintf
                                                "%s\n\n[CONFLICT RESUMPTION] An in-progress rebase hit conflicts. Conflicted files:\n  %s\nYou are RESUMING an in-progress rebase for the same Manager session \u2014 do NOT restart the original task. Resolve the conflicts, then continue and finish the rebase."
                                                basePrompt
                                                conflictList

                                        match! deps.Manager.RunManager managerId worktreePath prompt with
                                        | Error err ->
                                            return
                                                Error(
                                                    sprintf
                                                        "Rebase conflict (%s); manager continuation failed: %s"
                                                        conflict
                                                        err
                                                )
                                        | Ok() -> return! deps.Git.Rebase worktreePath deps.TargetBranch
                                    }

                            match finalRebase with
                            | Error err ->
                                return OrchestratorVerdict.IntegrationFailed(managerId, sprintf "Rebase failed: %s" err)
                            | Ok() ->
                                match! deps.ReconcileTarget() with
                                | Error err ->
                                    return
                                        OrchestratorVerdict.IntegrationFailed(
                                            managerId,
                                            sprintf "Git reconcile failed after rebase: %s" err
                                        )
                                | Ok() ->
                                    match! deps.ReverifyTwice managerId worktreePath with
                                    | Error err -> return OrchestratorVerdict.NeedsReview(managerId, err)
                                    | Ok() ->
                                        // FfMerge is the only write to the target ref: the Git port performs
                                        // `git merge --ff-only`, keeping Git authoritative on reconcile.
                                        match! deps.Git.FfMerge worktreePath deps.TargetBranch with
                                        | Error err ->
                                            return
                                                OrchestratorVerdict.IntegrationFailed(
                                                    managerId,
                                                    sprintf "FF merge failed: %s" err
                                                )
                                        | Ok commitHash ->
                                            match
                                                deps.AppendFact
                                                    StreamId.Workspace
                                                    (AgentFact.OrchestratorPublished
                                                        {| ManagerId = managerId
                                                           CandidateId = candidateId
                                                           CommitHash = commitHash |})
                                            with
                                            | Error err ->
                                                return
                                                    OrchestratorVerdict.IntegrationFailed(
                                                        managerId,
                                                        sprintf "Failed to persist published fact: %s" err
                                                    )
                                            | Ok() ->
                                                let! _ = deps.Git.RemoveWorktree worktreePath
                                                return OrchestratorVerdict.Published(managerId, commitHash)
        }
