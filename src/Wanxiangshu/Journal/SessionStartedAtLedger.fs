namespace Wanxiangshu.Journal

open System
open System.Threading.Tasks
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity

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
