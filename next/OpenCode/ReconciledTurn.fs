namespace Wanxiangshu.Next.OpenCode

open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session

type TurnOutcome =
    | TurnInProgress
    | TurnNeedsContinuation of reason: string
    | TurnCompleted
    | TurnAborted of reason: string
    | TurnFailed of error: string
    | TurnUnknown

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
        AgentRole: AgentRole option
        Directory: string
        Parts: MessagePart array
        Finish: string option
        ErrorName: string option
        Model: OpencodeModel option
        Outcome: TurnOutcome
    }

type ActiveRunBinding =
    {
        SessionId: SessionId
        RunId: string option
        AuthorityRootUserMessageId: AuthorityRootUserMessageId option
        /// Latest physical user message for the active logical run.
        PhysicalUserMessageId: PhysicalUserMessageId option
        ContinuationMessageIds: Set<string>
        AgentRole: AgentRole option
        Directory: string
    }
