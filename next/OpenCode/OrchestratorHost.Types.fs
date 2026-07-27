namespace Wanxiangshu.Next.OpenCode

open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session

type OrchestratorHostDeps =
    { Sessions: ISessionHostPort
      Journal: AgentJournal option
      ModelConfig: ModelResolver.ModelConfig option
      OnChildCreated: string -> AgentRole -> SessionId -> unit
      RegisterChildDirectory: SessionId -> string -> unit
      RegisterReviewerTree: string -> GitTreePort -> unit
      RepoPath: string
      TargetBranch: string }
