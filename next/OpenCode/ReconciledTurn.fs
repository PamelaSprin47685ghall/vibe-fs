namespace Wanxiangshu.Next.OpenCode

open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session

type TurnOutcome =
    | TurnCompleted
    | TurnAborted of reason: string
    | TurnFailed of error: string
    | TurnUnknown

type ReconciledTurn =
    { SessionId: SessionId
      UserMessageId: MessageId
      AssistantMessageId: MessageId
      AgentRole: AgentRole option
      Directory: string
      Parts: obj array
      Finish: string option
      ErrorName: string option
      Model: OpencodeModel option
      Outcome: TurnOutcome }

type ActiveRunBinding =
    { SessionId: SessionId
      UserMessageId: MessageId option
      AgentRole: AgentRole option
      Directory: string }
