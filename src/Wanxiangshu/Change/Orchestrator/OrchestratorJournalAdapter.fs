namespace Wanxiangshu.Change

open Wanxiangshu.Persistence.Journal.JournalOutcome
open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Foundation.Identity
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
        { AwaitChangeFrom =
            fun revision ->
                task {
                    let! _ = AgentJournal.awaitChangeFrom revision journal
                    return ()
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
