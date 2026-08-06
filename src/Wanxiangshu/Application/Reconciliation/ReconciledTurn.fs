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
        Outcome: ReconcileProgram.TurnOutcome
    }

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
