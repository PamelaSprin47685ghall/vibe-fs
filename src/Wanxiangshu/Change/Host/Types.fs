namespace Wanxiangshu.Change.Host

open System.Threading.Tasks
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Review
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

type OrchestratorHostDeps =
    { Sessions: ISessionHostPort
      RootWorkspace: IRootWorkspaceReader
      WaitObserver: IWaitObserver
      Journal: AgentJournal option
      SessionSnapshot: ISessionSnapshotPort option
      OnChildCreated: string -> Role -> SessionId -> unit
      RegisterChildDirectory: SessionId -> string -> unit
      RegisterReviewerTree: string -> GitTreePort -> unit
      OnRunStarted: SessionId -> Role -> string option -> unit
      RepoPath: string
      TargetBranch: string
      ParentWorkRecordFor: SessionId -> Task<string option>
      ChildWorkRecordFor: SessionId -> Task<string option> }
