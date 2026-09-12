namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// Consumer-side journal reads needed by TerminalPolicy and OrdinaryTurnWorkflow.
/// Each member performs exactly one journal snapshot read at call time;
/// multiple fields within one member share that snapshot.
type TerminalPolicyPort =
    { IsPoisoned: unit -> bool
      HasListableHandles: SessionId -> bool
      HasActiveOrchestratorJobs: unit -> bool
      IsLinkedChild: SessionId -> bool
      TryCanonicalRole: SessionId -> Role option }
