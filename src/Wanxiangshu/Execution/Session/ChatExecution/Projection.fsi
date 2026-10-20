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

/// What the CUT-12 phase law (wanxiangli/chat-execution) says about one durable
/// fact: replace the phase, keep the established fact, or reject it.
[<RequireQualifiedAccess>]
type ChatExecutionLawFold =
    | SetAccepted
    | SetStarted
    | SetEndedBeforeStart of PreStartOutcome
    | SetEndedAfterStart of ChatExecutionTerminalDisposition
    | Keep
    | Reject of reason: string

/// A start/terminal/settlement decision of the phase law. `Established` is the
/// already assigned terminal disposition the verdict reports, when it has one.
type ChatExecutionLawDecision =
    { Verdict: string
      Established: ChatExecutionTerminalDisposition option }

/// CUT-12: the single-assignment law of one exact chat execution has one owner,
/// wanxiangli/chat-execution. This adapter only projects the typed phase and the
/// two value-equality facts to it and maps its verdict back.
[<RequireQualifiedAccess>]
module ChatExecutionPhaseLaw =
    val schemaRejection: schemaVersion: int -> string option

    val foldAccepted: current: ChatExecutionState option -> sameAccepted: bool -> ChatExecutionLawFold

    val foldProviderStarted:
        current: ChatExecutionState option -> sameAccepted: bool -> sameStarted: bool -> ChatExecutionLawFold

    val foldTerminal:
        current: ChatExecutionState option ->
        afterProviderStart: bool ->
        sameAccepted: bool ->
        sameStarted: bool ->
        disposition: ChatExecutionTerminalDisposition ->
            ChatExecutionLawFold

    val decideStart: current: ChatExecutionState -> sameStarted: bool -> ChatExecutionLawDecision

    val decideTerminal:
        current: ChatExecutionState ->
        sameStarted: bool ->
        disposition: ChatExecutionTerminalDisposition ->
            ChatExecutionLawDecision

    val settlePreProvider:
        current: ChatExecutionState option ->
        sameAccepted: bool ->
        disposition: ChatExecutionTerminalDisposition ->
            ChatExecutionLawDecision
