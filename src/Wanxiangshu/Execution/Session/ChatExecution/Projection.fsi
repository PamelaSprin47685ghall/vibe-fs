namespace Wanxiangshu.Execution.Session.ChatExecution

[<RequireQualifiedAccess>]
type ChatExecutionLifecycle =
    | Accepted
    | ProviderStarted
    | Terminal of ChatExecutionTerminalDisposition

type ChatExecutionState =
    { Key: ChatExecutionKey
      Evidence: AcceptedChatExecutionEvidence
      ProviderStarted: ProviderStartedEvidence option
      TerminalEvidence: ChatExecutionTerminalEvidence option
      Lifecycle: ChatExecutionLifecycle }

type ChatExecutionProjectionState =
    { ByKey: Map<ChatExecutionKey, ChatExecutionState> }

[<RequireQualifiedAccess>]
module ChatExecutionProjection =
    val empty: ChatExecutionProjectionState
    val current: projection: ChatExecutionProjectionState -> ChatExecutionState list
    val byKey: key: ChatExecutionKey -> projection: ChatExecutionProjectionState -> ChatExecutionState option
    val nonTerminal: projection: ChatExecutionProjectionState -> ChatExecutionState list
