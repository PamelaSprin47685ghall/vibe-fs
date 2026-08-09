namespace Wanxiangshu.OpenCode

open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session
open Wanxiangshu.Domain

type ReconciledTurn =
    {
        SessionId: SessionId
        /// Physical user message that caused this provider run.
        PhysicalUserMessageId: PhysicalUserMessageId
        /// Semantic authority root; continuations never replace this identity.
        AuthorityRootUserMessageId: AuthorityRootUserMessageId
        /// HOST-010/HOST-011: one assistant message is one provider request is
        /// one turn, so the run identity IS the assistant message id. Naming the
        /// field `AssistantMessageId` invited a second identity for the same
        /// thing — and FALLBACK-003 deduplicates failed attempts by this value.
        ProviderRun: ProviderRunIdentity
        Role: Role option
        /// The worktree this run executed in, when it has one. A Manager child runs
        /// in its own worktree; a top-level session has none.
        Directory: string option
        Parts: MessagePart array
        Finish: string option
        ErrorName: string option
        Model: OpencodeModel option
        /// Publishable provider-turn classification (HOST-004). Never TurnUnknown.
        Outcome: ReconcileProgram.TurnOutcome
        /// Reconciliation-private finish=None observation. When Some, evidence and
        /// missing-final-report repair consult this — not Outcome.
        Observation: ReconcileProgram.SnapshotObservation option
    }

/// The reconciled turn plus the quiescence evidence of the pass that published
/// it (HOST-004). Only an IdleWake carries `Some permit`; retry / failure
/// wakes carry None. The side-effect boundary re-checks the permit with the
/// gate immediately before any physical idle-derived send.
type ReconciledTurnContext =
    { Turn: ReconciledTurn
      Quiescence: QuiescencePermit option }

type ActiveRunBinding =
    {
        SessionId: SessionId
        RunId: string option
        AuthorityRootUserMessageId: AuthorityRootUserMessageId option
        /// Latest physical user message for the active logical run.
        PhysicalUserMessageId: PhysicalUserMessageId option
        ContinuationMessageIds: Set<string>
        Role: Role option
        Directory: string option
    }
