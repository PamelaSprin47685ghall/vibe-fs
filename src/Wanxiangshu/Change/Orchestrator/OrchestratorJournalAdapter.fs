namespace Wanxiangshu.Change

open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Relay
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation

module OrchestratorJournalAdapter =
    let forSweep (journal: AgentJournal) : OrchestratorSweepPort =
        { ActiveJobs =
            fun () -> OrchestratorProjection.activeJobs (AgentJournal.snapshot journal).AgentProjections.Orchestrator
          TryJob =
            fun jobId ->
                OrchestratorProjection.tryFind jobId (AgentJournal.snapshot journal).AgentProjections.Orchestrator
          RecoveryView =
            fun orchestratorId ->
                let snapshot = AgentJournal.snapshot journal

                { ActiveJobs = OrchestratorProjection.activeJobs snapshot.AgentProjections.Orchestrator
                  Handles =
                    Map.tryFind orchestratorId snapshot.AgentProjections.Sessions
                    |> Option.bind (fun s -> s.Handles) } }

    let forRelay (journal: AgentJournal) : OrchestratorRelayPort =
        let roadIdOf (record: ManagerJobProjection) =
            RoadId.create (SessionId.value record.ManagerSessionId)

        let roadOfRecord (record: ManagerJobProjection) (projection: ProjectionSet) =
            AgentProjection.tryFind record.ManagerSessionId projection.AgentProjections
            |> Option.bind (fun session -> session.Relay)
            |> Option.bind (fun relay -> Fold.view relay (roadIdOf record))

        { RoadSnapshot =
            fun record ->
                let projection, revision = AgentJournal.snapshotWithRevision journal
                roadOfRecord record projection, revision
          AwaitChangeFrom =
            fun revision ->
                task {
                    let! _ = AgentJournal.awaitChangeFrom revision journal
                    return ()
                }
          AppendRelay =
            fun record transaction ->
                task {
                    let! result =
                        AgentJournal.appendAgent
                            (StreamId.Session record.ManagerSessionId)
                            None
                            (AgentFact.Relay(
                                RelayFactCases.TransactionCommitted
                                    {| RoadId = roadIdOf record
                                       Transaction = transaction |}
                            ))
                            journal

                    return
                        result
                        |> Result.map (fun _ -> ())
                        |> Result.mapError JournalAppendFailure.describe
                } }

    let forEngine (journal: AgentJournal) : OrchestratorJournalPort =
        { AppendFact =
            fun stream fact ->
                task {
                    match! AgentJournal.appendAgent stream None fact journal with
                    | Ok projection -> return Ok projection
                    | Error failure -> return Error(JournalAppendFailure.describe failure)
                }
          Snapshot = fun () -> AgentJournal.snapshot journal }
