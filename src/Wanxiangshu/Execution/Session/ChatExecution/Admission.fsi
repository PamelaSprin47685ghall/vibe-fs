namespace Wanxiangshu.Execution.Session.ChatExecution

open Wanxiangshu.Foundation.Identity

type ChatAdmissionMessage =
    { SessionId: SessionId
      PhysicalUserMessageId: PhysicalUserMessageId
      ExplicitAgent: string option }

[<RequireQualifiedAccess>]
type ChatAdmissionIntent =
    | AlreadyTerminal of ChatExecutionTerminalDisposition
    | AlreadyStarted of ProviderStartedEvidence
    | ResumeAccepted of AcceptedChatExecutionEvidence
    | NeedAcceptance of AcceptedChatExecutionEvidence

[<RequireQualifiedAccess>]
type ChatAdmissionError =
    | StateKeyMismatch of suppliedStateKey: ChatExecutionKey * messageKey: ChatExecutionKey
    | AttemptKeyMismatch of attemptKey: ChatExecutionKey * messageKey: ChatExecutionKey
    | ExplicitAgentMismatch of explicitAgent: string * selectedAgent: string
    | AttemptEvidenceInvalid of reason: string
    | ExistingEvidenceConflict of established: AcceptedChatExecutionEvidence * attempted: AcceptedChatExecutionEvidence

[<RequireQualifiedAccess>]
module ChatAdmission =
    val decide:
        message: ChatAdmissionMessage ->
        attemptedEvidence: AcceptedChatExecutionEvidence ->
        suppliedState: ChatExecutionState option ->
            Result<ChatAdmissionIntent, ChatAdmissionError>
