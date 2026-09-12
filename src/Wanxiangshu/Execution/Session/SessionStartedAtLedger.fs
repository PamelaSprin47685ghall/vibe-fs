namespace Wanxiangshu.Execution.Session

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
module SessionStartedAtLedger =

    let tryStartedAt (port: SessionStartedAtPort) sessionId = port.TryStartedAt sessionId

    let bind (port: SessionStartedAtPort) sessionId (candidate: DateTimeOffset) : Task<Result<DateTimeOffset, string>> =
        port.Bind sessionId candidate

    /// HOST-013: bind session start, returning Result for composition root to handle failure.
    let bindOrAbort
        (port: SessionStartedAtPort)
        (sessionId: SessionId)
        (candidate: DateTimeOffset)
        : Task<Result<DateTimeOffset option, string>> =
        task {
            match! bind port sessionId candidate with
            | Ok startedAt -> return Ok(Some startedAt)
            | Error reason -> return Error reason
        }

    /// HOST-013: try bind session started at from optional port/session/candidate.
    let tryBindOrAbort
        (port: SessionStartedAtPort option)
        (projectionSessionIdOpt: string option)
        (sessionStartCandidate: DateTimeOffset option)
        : Task<Result<DateTimeOffset option, string>> =
        match port, projectionSessionIdOpt, sessionStartCandidate with
        | Some p, Some sessionId, Some candidate -> bindOrAbort p (SessionId.create sessionId) candidate
        | _ -> Task.FromResult(Ok None)

    let private failSessionStartBind
        (terminateSession: SessionId -> string -> Task<Result<unit, string>>)
        (emitDiagnostic: string -> (string * string) list -> unit)
        (projectionSessionIdOpt: string option)
        (reason: string)
        : Task<DateTimeOffset option> =
        task {
            let sessionId = projectionSessionIdOpt |> Option.defaultValue ""
            emitDiagnostic "host-013-session-start-bind-failed" [ "session_id", sessionId; "result", reason ]
            let terminalReason = "HOST-013 SessionStartedAt bind failed: " + reason

            match projectionSessionIdOpt with
            | Some value ->
                let! _ = terminateSession (SessionId.create value) terminalReason
                return raise (InvalidOperationException terminalReason)
            | None -> return raise (InvalidOperationException terminalReason)
        }

    /// HOST-013: bind session start for transform boundary, logging diagnostics and terminating on error.
    let bindSessionStartedAt
        (journal: SessionStartedAtPort option)
        (clock: IClockPort)
        (terminateSession: SessionId -> string -> Task<Result<unit, string>>)
        (emitDiagnostic: string -> (string * string) list -> unit)
        (projectionSessionIdOpt: string option)
        : Task<DateTimeOffset option> =
        task {
            let sessionStartCandidate =
                projectionSessionIdOpt |> Option.map (fun _ -> clock.UtcNow())

            match! tryBindOrAbort journal projectionSessionIdOpt sessionStartCandidate with
            | Ok startedAt -> return startedAt
            | Error reason -> return! failSessionStartBind terminateSession emitDiagnostic projectionSessionIdOpt reason
        }
