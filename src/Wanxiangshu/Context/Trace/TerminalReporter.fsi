namespace Wanxiangshu.Context.Trace

open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Foundation
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
type XTraceTerminalCompletion =
    | Published of AgentRunResult
    | CaptureFailed of XTraceCaptureError
    | RejectedMissingRole
    | RejectedEmptyOutput

module TerminalReporter =
    val completeUsingTextEvidence:
        eventPort: IEventObservationPort ->
        journal: AgentJournal option ->
        turn: ReconciledTurn ->
        sessionWideText: string ->
            Task<XTraceTerminalCompletion>

    val completeWithEvidence:
        eventPort: IEventObservationPort ->
        journal: AgentJournal option ->
        turn: ReconciledTurn ->
            Task<XTraceTerminalCompletion>

    val complete:
        eventPort: IEventObservationPort -> journal: AgentJournal option -> turn: ReconciledTurn -> Task<bool * bool>
