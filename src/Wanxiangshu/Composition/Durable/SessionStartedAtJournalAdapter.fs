namespace Wanxiangshu.Composition.Durable

open System
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Session
open Wanxiangshu.Host
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
module SessionStartedAtJournalAdapter =
    let forDelegatedToolEstimate (journal: AgentJournal) : DelegatedToolEstimatePort =
        { TryState =
            fun sessionId ->
                AgentJournal.snapshot journal
                |> fun snapshot -> AgentProjection.tryFind sessionId snapshot.AgentProjections
                |> Option.bind (fun session -> session.DelegatedToolEstimate)
          Append =
            fun sessionId fact ->
                task {
                    let! result =
                        AgentJournal.appendAgent (StreamId.Session sessionId) None (AgentFact.Delegation fact) journal

                    return result |> Result.map ignore |> Result.mapError JournalAppendFailure.describe
                } }

    let forSessionStartedAt (journal: AgentJournal) : SessionStartedAtPort =
        { TryStartedAt =
            fun sessionId ->
                AgentJournal.snapshot journal
                |> fun snapshot -> AgentProjection.tryFind sessionId snapshot.AgentProjections
                |> Option.bind (fun session -> session.SessionStartedAt)
                |> Option.map SessionStartedAtProjection.startedAt
          Bind =
            fun sessionId candidate ->
                task {
                    let existing =
                        AgentJournal.snapshot journal
                        |> fun snapshot -> AgentProjection.tryFind sessionId snapshot.AgentProjections
                        |> Option.bind (fun session -> session.SessionStartedAt)
                        |> Option.map SessionStartedAtProjection.startedAt

                    match existing with
                    | Some dt -> return Ok dt
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
                        | Ok projectionSet ->
                            return
                                AgentProjection.tryFind sessionId projectionSet.AgentProjections
                                |> Option.bind (fun session -> session.SessionStartedAt)
                                |> Option.map SessionStartedAtProjection.startedAt
                                |> Option.defaultValue candidate
                                |> Ok
                        | Error failure -> return Error(JournalAppendFailure.describe failure)
                } }
