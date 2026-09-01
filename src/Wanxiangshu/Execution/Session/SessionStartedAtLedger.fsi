namespace Wanxiangshu.Execution.Session

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
module SessionStartedAtLedger =
    val tryStartedAt: journal: AgentJournal -> sessionId: SessionId -> DateTimeOffset option

    val bind:
        journal: AgentJournal ->
        sessionId: SessionId ->
        candidate: DateTimeOffset ->
            Task<Result<DateTimeOffset, string>>

    val bindOrAbort:
        durable: AgentJournal ->
        sessionId: SessionId ->
        candidate: DateTimeOffset ->
            Task<Result<DateTimeOffset option, string>>

    val tryBindOrAbort:
        journal: AgentJournal option ->
        projectionSessionIdOpt: string option ->
        sessionStartCandidate: DateTimeOffset option ->
            Task<Result<DateTimeOffset option, string>>

    val bindSessionStartedAt:
        journal: AgentJournal option ->
        clock: IClockPort ->
        terminateSession: (SessionId -> string -> Task<Result<unit, string>>) ->
        emitDiagnostic: (string -> (string * string) list -> unit) ->
        projectionSessionIdOpt: string option ->
            Task<DateTimeOffset option>
