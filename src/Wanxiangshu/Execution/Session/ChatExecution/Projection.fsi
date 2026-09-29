namespace Wanxiangshu.Execution.Session.ChatExecution

open Wanxiangshu.Interaction.Authority

[<RequireQualifiedAccess>]
type PreStartOutcome =
    | Cancelled
    | Rejected
    | Failed

[<RequireQualifiedAccess>]
module PreStartOutcome =
    val disposition: outcome: PreStartOutcome -> ChatExecutionTerminalDisposition
    val ofDisposition: disposition: ChatExecutionTerminalDisposition -> PreStartOutcome option

[<RequireQualifiedAccess>]
type ChatExecutionState =
    | Accepted of AcceptedChatExecutionEvidence
    | Started of ProviderStartedEvidence
    | EndedBeforeStart of AcceptedChatExecutionEvidence * PreStartOutcome
    | EndedAfterStart of ProviderStartedEvidence * ChatExecutionTerminalDisposition

    member key: ChatExecutionKey
    member acceptedEvidence: AcceptedChatExecutionEvidence
    member startedEvidence: ProviderStartedEvidence option
    member terminalDisposition: ChatExecutionTerminalDisposition option
    member lifecycleName: string
    member origin: PromptOrigin

type ChatExecutionProjectionState =
    { ByKey: Map<ChatExecutionKey, ChatExecutionState> }

[<RequireQualifiedAccess>]
module ChatExecutionProjection =
    val empty: ChatExecutionProjectionState
    val current: projection: ChatExecutionProjectionState -> ChatExecutionState list
    val byKey: key: ChatExecutionKey -> projection: ChatExecutionProjectionState -> ChatExecutionState option
    val nonTerminal: projection: ChatExecutionProjectionState -> ChatExecutionState list
