namespace Wanxiangshu.Journal

open Wanxiangshu.Kernel.Identity

/// Rebuilt journal state: durable domain projections plus runtime frontier.
type ProjectionSet =
    { AgentProjections: AgentProjectionSet
      RuntimeId: RuntimeId option }

type RuntimeSnapshot =
    { Frontier: Frontier
      Projections: ProjectionSet
      OwnRuntimeId: RuntimeId option
      OwnLocalSeq: int64 }
