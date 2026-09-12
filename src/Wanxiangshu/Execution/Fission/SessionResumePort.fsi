namespace Wanxiangshu.Execution.Fission

open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority

/// Domain-owned read port for explicit session resume.
/// Exposes only the authority profile interpretation and candidate child handles consumed by `/continue`.
type SessionResumeJournalPort =
    { TryResumeProfile: SessionId -> PromptAuthority.AuthorityExecutionProfile option
      CandidateRecords: SessionId -> HandleRecord list }
