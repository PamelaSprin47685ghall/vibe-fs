namespace Wanxiangshu.Execution.Session

open System
open System.Threading.Tasks
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Host
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
module SessionStartedAtLedger =

    let tryStartedAt (journal: AgentJournal) sessionId =
        AgentJournal.snapshot journal
        |> fun snapshot -> AgentProjection.tryFind sessionId snapshot.AgentProjections
        |> Option.bind (fun session -> session.SessionStartedAt)
        |> Option.map SessionStartedAtProjection.startedAt

    let bind (journal: AgentJournal) sessionId (candidate: DateTimeOffset) : Task<Result<DateTimeOffset, string>> =
        task {
            match tryStartedAt journal sessionId with
            | Some existing -> return Ok existing
            | None ->
                match!
                    AgentJournal.appendAgent
                        (StreamId.Session sessionId)
                        None
                        (HostFact.SessionStartedAtBound
                            {| SessionId = sessionId
                               StartedAt = candidate |})
                        journal
                with
                | Error error -> return Error(sprintf "%A" error)
                | Ok projection ->
                    match
                        AgentProjection.tryFind sessionId projection.AgentProjections
                        |> Option.bind (fun session -> session.SessionStartedAt)
                        |> Option.map SessionStartedAtProjection.startedAt
                    with
                    | Some startedAt -> return Ok startedAt
                    | None -> return Error "SessionStartedAtBound did not materialize its projection"
        }

    /// HOST-013: bind session start, returning Result for composition root to handle failure.
    let bindOrAbort
        (durable: AgentJournal)
        (sessionId: SessionId)
        (candidate: DateTimeOffset)
        : Task<Result<DateTimeOffset option, string>> =
        task {
            match! bind durable sessionId candidate with
            | Ok startedAt -> return Ok(Some startedAt)
            | Error reason -> return Error reason
        }

    /// HOST-013: try bind session started at from optional journal/session/candidate.
    let tryBindOrAbort
        (journal: AgentJournal option)
        (projectionSessionIdOpt: string option)
        (sessionStartCandidate: DateTimeOffset option)
        : Task<Result<DateTimeOffset option, string>> =
        match journal, projectionSessionIdOpt, sessionStartCandidate with
        | Some durable, Some sessionId, Some candidate -> bindOrAbort durable (SessionId.create sessionId) candidate
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
        (journal: AgentJournal option)
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
            | Error reason ->
                return! failSessionStartBind terminateSession emitDiagnostic projectionSessionIdOpt reason
        }
