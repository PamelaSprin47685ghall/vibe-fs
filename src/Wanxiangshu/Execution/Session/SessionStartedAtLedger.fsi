namespace Wanxiangshu.Execution.Session

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
module SessionStartedAtLedger =
    val tryStartedAt: port: SessionStartedAtPort -> sessionId: SessionId -> DateTimeOffset option

    val bind:
        port: SessionStartedAtPort ->
        sessionId: SessionId ->
        candidate: DateTimeOffset ->
            Task<Result<DateTimeOffset, string>>

    val bindOrAbort:
        port: SessionStartedAtPort ->
        sessionId: SessionId ->
        candidate: DateTimeOffset ->
            Task<Result<DateTimeOffset option, string>>

    val tryBindOrAbort:
        port: SessionStartedAtPort option ->
        projectionSessionIdOpt: string option ->
        sessionStartCandidate: DateTimeOffset option ->
            Task<Result<DateTimeOffset option, string>>

    val bindSessionStartedAt:
        journal: SessionStartedAtPort option ->
        clock: IClockPort ->
        terminateSession: (SessionId -> string -> Task<Result<unit, string>>) ->
        emitDiagnostic: (string -> (string * string) list -> unit) ->
        projectionSessionIdOpt: string option ->
            Task<DateTimeOffset option>
