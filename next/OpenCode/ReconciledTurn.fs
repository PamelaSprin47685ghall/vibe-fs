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
        UserMessageId: MessageId
        /// Semantic authority root; continuations never replace this identity.
        RootUserMessageId: MessageId
        AssistantMessageId: MessageId
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
        RootUserMessageId: MessageId option
        /// Latest physical user message for the active logical run.
        PhysicalUserMessageId: MessageId option
        ContinuationMessageIds: Set<string>
        AgentRole: AgentRole option
        Directory: string
    }
