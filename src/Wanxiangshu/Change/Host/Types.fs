namespace Wanxiangshu.Change.Host

open System.Threading.Tasks
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Mission.Relay
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Context.Trace

type OrchestratorHostDeps =
    { Sessions: ISessionHostPort
      RootWorkspace: IRootWorkspaceReader
      WaitObserver: IWaitObserver
      Journal: AgentJournal option
      SessionSnapshot: ISessionSnapshotPort option
      OnChildCreated: string -> Role -> SessionId -> unit
      RegisterChildDirectory: SessionId -> string -> unit
      OnRunStarted: SessionId -> Role -> string option -> unit
      SendGateContinuation:
          SessionId
              -> string
              -> PromptAuthority.ContinuationKind
              -> string option
              -> AgentJournal option
              -> string
              -> ProviderRunIdentity
              -> Task<Result<Wanxiangshu.Foundation.Identity.PhysicalUserMessageId, string>>
      ContinueManagerLoop: SessionId -> string -> Task<Result<unit, string>>
      CaptureWorktreeSnapshot: WorktreePath -> Result<WorkspaceSnapshotId, string>
      RepoPath: string
      TargetBranch: string
      ParentWorkRecordFor: SessionId -> Task<string option>
      ChildWorkRecordFor: SessionId -> Task<string option>
      ChildWorkRecordForRun: SessionId -> XTraceRange -> ProviderRunIdentity -> Task<string option> }
