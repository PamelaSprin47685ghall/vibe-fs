namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Journal
open Wanxiangshu.Review
open Wanxiangshu.Session

type OrchestratorHostDeps =
    { Sessions: ISessionHostPort
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
