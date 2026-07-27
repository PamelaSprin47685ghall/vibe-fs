namespace Wanxiangshu.Next.Orchestrator

open System
open System.Collections.Generic
open System.Threading.Tasks
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
      FfMerge: string -> string -> string option -> Task<Result<string, string>>
      ConflictedFiles: string -> Task<Result<string list, string>>
      RemoveWorktree: string -> Task<Result<unit, string>>
      HasRebaseHead: string -> Task<bool>
      ListWorktrees: unit -> Task<Result<(string * string option) list, string>>
      ListManagerBranches: unit -> Task<Result<string list, string>>
      DeleteBranch: string -> Task<Result<unit, string>> }

type ManagerPort =
    { RunManager: string -> string -> string -> Task<Result<unit, string>>
      Reverify: string -> string -> string -> Task<Result<unit, string>> }

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

module OrchestratorConstants =
    [<Literal>]
    let targetRefMovedError = "target ref moved"
