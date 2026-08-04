namespace Wanxiangshu.OpenCode

open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Journal
open Wanxiangshu.Session

type OrchestratorHostDeps =
    { Sessions: ISessionHostPort
      Journal: AgentJournal option
      SessionSnapshot: ISessionSnapshotPort option
      OnChildCreated: string -> AgentRole -> SessionId -> unit
      RegisterChildDirectory: SessionId -> string -> unit
      RegisterReviewerTree: string -> GitTreePort -> unit
      OnRunStarted: SessionId -> AgentRole -> string option -> unit
      RepoPath: string
      TargetBranch: string
      ParentWorkRecordFor: SessionId -> string option
      ChildWorkRecordFor: SessionId -> string option }
