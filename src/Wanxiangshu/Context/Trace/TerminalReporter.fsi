namespace Wanxiangshu.Context.Trace

open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Foundation
open Wanxiangshu.OpenCode

[<RequireQualifiedAccess>]
type XTraceTerminalCompletion =
    | Published of AgentRunResult
    | CaptureFailed of XTraceCaptureError
    | RejectedMissingRole
    | RejectedEmptyOutput
    | RejectedUndelivered

module TerminalReporter =
    val completeUsingTextEvidence:
        eventPort: IEventObservationPort ->
        port: TerminalTracePort option ->
        turn: ReconciledTurn ->
        sessionWideText: string ->
        deliveryWitness: bool ->
            Task<XTraceTerminalCompletion>

    val completeWithEvidence:
        eventPort: IEventObservationPort ->
        port: TerminalTracePort option ->
        turn: ReconciledTurn ->
        deliveryWitness: bool ->
            Task<XTraceTerminalCompletion>

    val complete:
        eventPort: IEventObservationPort ->
        port: TerminalTracePort option ->
        turn: ReconciledTurn ->
        deliveryWitness: bool ->
            Task<bool * bool>
