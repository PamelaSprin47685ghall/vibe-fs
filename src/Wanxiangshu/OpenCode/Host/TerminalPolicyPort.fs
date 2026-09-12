namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// Consumer-side journal reads needed by TerminalPolicy and OrdinaryTurnWorkflow.
/// Each member derives from exactly one Journal Snapshot revision captured
/// when the port is built (see AgentJournalPortAdapter.forTerminalPolicy).
type TerminalPolicyPort =
    { IsPoisoned: unit -> bool
      HasListableHandles: SessionId -> bool
      HasActiveOrchestratorJobs: unit -> bool
      IsLinkedChild: SessionId -> bool
      TryCanonicalRole: SessionId -> Role option }
