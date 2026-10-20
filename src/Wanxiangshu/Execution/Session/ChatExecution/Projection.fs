namespace Wanxiangshu.Execution.Session.ChatExecution

open Fable.Core
open Fable.Core.JsInterop
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

[<RequireQualifiedAccess>]
type ChatExecutionLawFold =
    | SetAccepted
    | SetStarted
    | SetEndedBeforeStart of PreStartOutcome
    | SetEndedAfterStart of ChatExecutionTerminalDisposition
    | Keep
    | Reject of reason: string

type ChatExecutionLawDecision =
    { Verdict: string
      Established: ChatExecutionTerminalDisposition option }

[<RequireQualifiedAccess>]
module ChatExecutionPhaseLaw =

    [<Import("ChatExecutionLaw", "wanxiangli/chat-execution")>]
    let private lawClass: obj = jsNative

    [<Emit("new $0()")>]
    let private construct (cls: obj) : obj = jsNative

    let private law: obj = construct lawClass

    let private dispositionName (disposition: ChatExecutionTerminalDisposition) : string =
        match disposition with
        | ChatExecutionTerminalDisposition.Completed -> "Completed"
        | ChatExecutionTerminalDisposition.Cancelled -> "Cancelled"
        | ChatExecutionTerminalDisposition.Rejected -> "Rejected"
        | ChatExecutionTerminalDisposition.Failed -> "Failed"

    let private dispositionOf (value: obj) : ChatExecutionTerminalDisposition =
        match unbox<string> value with
        | "Completed" -> ChatExecutionTerminalDisposition.Completed
        | "Cancelled" -> ChatExecutionTerminalDisposition.Cancelled
        | "Rejected" -> ChatExecutionTerminalDisposition.Rejected
        | "Failed" -> ChatExecutionTerminalDisposition.Failed
        | other -> invalidOp ("wanxiangli/chat-execution returned an unknown disposition: " + other)

    let private phase (state: ChatExecutionState option) : obj =
        match state with
        | None -> null
        | Some(ChatExecutionState.Accepted _) -> createObj [ "phase" ==> "Accepted" ]
        | Some(ChatExecutionState.Started _) -> createObj [ "phase" ==> "ProviderStarted" ]
        | Some(ChatExecutionState.EndedBeforeStart(_, outcome)) ->
            createObj
                [ "phase" ==> "EndedBeforeStart"
                  "disposition" ==> dispositionName (PreStartOutcome.disposition outcome) ]
        | Some(ChatExecutionState.EndedAfterStart(_, disposition)) ->
            createObj [ "phase" ==> "EndedAfterStart"; "disposition" ==> dispositionName disposition ]

    let private foldVerdict (verdict: obj) : ChatExecutionLawFold =
        match unbox<string> verdict?verdict with
        | "keep" -> ChatExecutionLawFold.Keep
        | "reject" -> ChatExecutionLawFold.Reject(unbox<string> verdict?reason)
        | "set" ->
            let next: obj = verdict?next

            match unbox<string> next?phase with
            | "Accepted" -> ChatExecutionLawFold.SetAccepted
            | "ProviderStarted" -> ChatExecutionLawFold.SetStarted
            | "EndedBeforeStart" ->
                match PreStartOutcome.ofDisposition (dispositionOf next?disposition) with
                | Some outcome -> ChatExecutionLawFold.SetEndedBeforeStart outcome
                | None -> invalidOp "wanxiangli/chat-execution set a Completed pre-start terminal"
            | "EndedAfterStart" -> ChatExecutionLawFold.SetEndedAfterStart(dispositionOf next?disposition)
            | other -> invalidOp ("wanxiangli/chat-execution returned an unknown phase: " + other)
        | other -> invalidOp ("wanxiangli/chat-execution returned an unknown fold verdict: " + other)

    let private decision (verdict: obj) : ChatExecutionLawDecision =
        let established: obj = verdict?established

        { Verdict = unbox<string> verdict?verdict
          Established =
            if isNull established then
                None
            else
                Some(dispositionOf established) }

    let schemaRejection (schemaVersion: int) : string option =
        let reason: obj = law?schemaRejection schemaVersion
        if isNull reason then None else Some(unbox<string> reason)

    let foldAccepted (current: ChatExecutionState option) (sameAccepted: bool) : ChatExecutionLawFold =
        law?fold (
            phase current,
            createObj
                [ "kind" ==> "Accepted"
                  "schemaVersion" ==> 1
                  "sameAccepted" ==> sameAccepted ]
        )
        |> foldVerdict

    let foldProviderStarted
        (current: ChatExecutionState option)
        (sameAccepted: bool)
        (sameStarted: bool)
        : ChatExecutionLawFold =
        law?fold (
            phase current,
            createObj
                [ "kind" ==> "ProviderStarted"
                  "schemaVersion" ==> 1
                  "sameAccepted" ==> sameAccepted
                  "sameStarted" ==> sameStarted ]
        )
        |> foldVerdict

    let foldTerminal
        (current: ChatExecutionState option)
        (afterProviderStart: bool)
        (sameAccepted: bool)
        (sameStarted: bool)
        (disposition: ChatExecutionTerminalDisposition)
        : ChatExecutionLawFold =
        law?fold (
            phase current,
            createObj
                [ "kind" ==> "Terminal"
                  "schemaVersion" ==> 1
                  "evidence"
                  ==> (if afterProviderStart then
                           "AfterProviderStart"
                       else
                           "PreProvider")
                  "disposition" ==> dispositionName disposition
                  "sameAccepted" ==> sameAccepted
                  "sameStarted" ==> sameStarted ]
        )
        |> foldVerdict

    let decideStart (current: ChatExecutionState) (sameStarted: bool) : ChatExecutionLawDecision =
        law?decideStart (phase (Some current), sameStarted) |> decision

    let decideTerminal
        (current: ChatExecutionState)
        (sameStarted: bool)
        (disposition: ChatExecutionTerminalDisposition)
        : ChatExecutionLawDecision =
        law?decideTerminal (phase (Some current), sameStarted, dispositionName disposition)
        |> decision

    let settlePreProvider
        (current: ChatExecutionState option)
        (sameAccepted: bool)
        (disposition: ChatExecutionTerminalDisposition)
        : ChatExecutionLawDecision =
        law?settlePreProvider (phase current, sameAccepted, dispositionName disposition)
        |> decision
