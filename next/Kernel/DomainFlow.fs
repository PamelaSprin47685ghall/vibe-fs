namespace Wanxiangshu.Next.Kernel

open System.Threading
open System.Threading.Tasks

// ================================================================
// Domain-specific context and error types (KISS-N01 §3)
// ================================================================

/// Agent domain: Manager, Coder, Inspector, Browser, Meditator, DevOps, etc.
[<RequireQualifiedAccess>]
type AgentError =
    | HostFailure of string
    | SessionDead of string
    | InvalidFork of string
    | ParentCancelled

/// Companion domain: Blogger/Projection operations for session X.
[<RequireQualifiedAccess>]
type CompanionError =
    | ProjectionFailed of string
    | BloggerFailed of string

/// Review domain: single-worktree peer review with double PERFECT barrier.
[<RequireQualifiedAccess>]
type ReviewError =
    | ReviewerFailed of string
    | ParentCancelled

/// Orchestrator domain: worktree lifecycle, rebase, integration, publish.
[<RequireQualifiedAccess>]
type OrchestratorError =
    | DirtyWorkspace of string list
    | RebaseFailed of string
    | PublishFailed of string

type AgentContext = { SessionId: string; AgentName: string }
type CompanionContext = { SessionId: string }
type ReviewContext = { SessionId: string; BarrierId: string }
type OrchestratorContext = { TargetBranch: string; WorktreePath: string }

// ================================================================
// Domain-specific flow type aliases (KISS-N01 §3 — five aliases)
// ================================================================

type AgentFlow<'a> = Flow<AgentContext, AgentError, 'a>
type CompanionFlow<'a> = Flow<CompanionContext, CompanionError, 'a>
type ReviewFlow<'a> = Flow<ReviewContext, ReviewError, 'a>
type OrchestratorFlow<'a> = Flow<OrchestratorContext, OrchestratorError, 'a>

// ================================================================
// Domain-specific builder instances
// ================================================================

[<AutoOpen>]
module DomainFlowBuilders =

    /// Computation expression builder for AgentFlow — manager, coder, inspector
    /// etc. The canonical name matches the AgentRole semantic domain.
    let agent = FlowBuilder<AgentContext, AgentError>(None)

    /// Computation expression builder for CompanionFlow — projection, delta,
    /// blogger step, prefix-epoch management.
    let companion = FlowBuilder<CompanionContext, CompanionError>(None)

    /// Computation expression builder for ReviewFlow — verdict confirmation,
    /// tree-hash binding, double-PERFECT sequencing.
    let review = FlowBuilder<ReviewContext, ReviewError>(None)

    /// Computation expression builder for OrchestratorFlow — clean gates,
    /// parallel ManagerJobs, serial integration, rebase/ff.
    let orchestrator = FlowBuilder<OrchestratorContext, OrchestratorError>(None)
