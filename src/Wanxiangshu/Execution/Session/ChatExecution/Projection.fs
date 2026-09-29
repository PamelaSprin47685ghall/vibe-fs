namespace Wanxiangshu.Execution.Session.ChatExecution

open Wanxiangshu.Interaction.Authority

/// The outcomes an execution can reach without its provider ever starting.
/// `Completed` is not one of them: an execution that never reached its
/// provider cannot have completed.
[<RequireQualifiedAccess>]
type PreStartOutcome =
    | Cancelled
    | Rejected
    | Failed

[<RequireQualifiedAccess>]
module PreStartOutcome =

    /// The durable terminal disposition of a pre-start outcome.
    let disposition (outcome: PreStartOutcome) : ChatExecutionTerminalDisposition =
        match outcome with
        | PreStartOutcome.Cancelled -> ChatExecutionTerminalDisposition.Cancelled
        | PreStartOutcome.Rejected -> ChatExecutionTerminalDisposition.Rejected
        | PreStartOutcome.Failed -> ChatExecutionTerminalDisposition.Failed

    /// The pre-start outcome of a durable terminal disposition. `Completed`
    /// requires a started provider, so it names no pre-start outcome.
    let ofDisposition (disposition: ChatExecutionTerminalDisposition) : PreStartOutcome option =
        match disposition with
        | ChatExecutionTerminalDisposition.Cancelled -> Some PreStartOutcome.Cancelled
        | ChatExecutionTerminalDisposition.Rejected -> Some PreStartOutcome.Rejected
        | ChatExecutionTerminalDisposition.Failed -> Some PreStartOutcome.Failed
        | ChatExecutionTerminalDisposition.Completed -> None

/// The durable projection of one managed chat execution. Every branch carries
/// exactly the evidence its phase has, so the impossible combinations — a
/// provider-started phase without started evidence, a terminal phase without
/// terminal evidence, a pre-start completion — are unrepresentable. The key,
/// the accepted evidence and the started evidence are derived from the carried
/// evidence; nothing is stored twice.
[<RequireQualifiedAccess>]
type ChatExecutionState =
    | Accepted of AcceptedChatExecutionEvidence
    | Started of ProviderStartedEvidence
    | EndedBeforeStart of AcceptedChatExecutionEvidence * PreStartOutcome
    | EndedAfterStart of ProviderStartedEvidence * ChatExecutionTerminalDisposition

    /// The exact execution key derived from the carried accepted evidence.
    member this.key: ChatExecutionKey =
        let accepted = this.acceptedEvidence

        { SessionId = accepted.SessionId
          PhysicalUserMessageId = accepted.PhysicalUserMessageId }

    /// The accepted evidence every phase carries.
    member this.acceptedEvidence: AcceptedChatExecutionEvidence =
        match this with
        | ChatExecutionState.Accepted accepted -> accepted
        | ChatExecutionState.Started started -> started.Accepted
        | ChatExecutionState.EndedBeforeStart(accepted, _) -> accepted
        | ChatExecutionState.EndedAfterStart(started, _) -> started.Accepted

    /// The started evidence, present exactly after `ProviderStarted`.
    member this.startedEvidence: ProviderStartedEvidence option =
        match this with
        | ChatExecutionState.Started started -> Some started
        | ChatExecutionState.EndedAfterStart(started, _) -> Some started
        | ChatExecutionState.Accepted _
        | ChatExecutionState.EndedBeforeStart _ -> None

    /// The terminal disposition, present exactly in a terminal phase.
    member this.terminalDisposition: ChatExecutionTerminalDisposition option =
        match this with
        | ChatExecutionState.EndedBeforeStart(_, outcome) -> Some(PreStartOutcome.disposition outcome)
        | ChatExecutionState.EndedAfterStart(_, disposition) -> Some disposition
        | ChatExecutionState.Accepted _
        | ChatExecutionState.Started _ -> None

    /// The phase name used by the observable surfaces: `Accepted`,
    /// `ProviderStarted` or `Terminal`.
    member this.lifecycleName: string =
        match this with
        | ChatExecutionState.Accepted _ -> "Accepted"
        | ChatExecutionState.Started _ -> "ProviderStarted"
        | ChatExecutionState.EndedBeforeStart _
        | ChatExecutionState.EndedAfterStart _ -> "Terminal"

    /// The prompt origin of the carried accepted evidence.
    member this.origin: PromptOrigin = this.acceptedEvidence.Origin

type ChatExecutionProjectionState =
    { ByKey: Map<ChatExecutionKey, ChatExecutionState> }

[<RequireQualifiedAccess>]
module ChatExecutionProjection =

    let empty: ChatExecutionProjectionState = { ByKey = Map.empty }

    let current (projection: ChatExecutionProjectionState) : ChatExecutionState list =
        projection.ByKey |> Map.toList |> List.map snd

    let byKey (key: ChatExecutionKey) (projection: ChatExecutionProjectionState) : ChatExecutionState option =
        Map.tryFind key projection.ByKey

    let nonTerminal (projection: ChatExecutionProjectionState) : ChatExecutionState list =
        current projection
        |> List.filter (fun execution -> execution.terminalDisposition.IsNone)
