namespace Wanxiangshu.Mission.Finality

open Wanxiangshu.Change
open Wanxiangshu.Mission.Obligation
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Strength.Persistence

open System
open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Session
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

module FinalityJournal =

    let appendLifecycle (journal: AgentJournal) (fact: ManagerLifecycleFact) : Task =
        task {
            let sessionId =
                match fact with
                | ManagerLifecycleFact.LifeOpened payload -> payload.SessionId
                | ManagerLifecycleFact.WorkActivated payload -> payload.SessionId
                | ManagerLifecycleFact.FinalityRequested payload -> payload.SessionId
                | ManagerLifecycleFact.FinalityReviewerEnlisted payload -> payload.SessionId
                | ManagerLifecycleFact.FinalityRejected payload -> payload.SessionId
                | ManagerLifecycleFact.FinalitySiblingSteered payload -> payload.SessionId
                | ManagerLifecycleFact.FinalityBlessed payload -> payload.SessionId
                | ManagerLifecycleFact.FinalityUndecided payload -> payload.SessionId
                | ManagerLifecycleFact.LifeCompleted payload -> payload.SessionId

            match! AgentJournal.appendManagerLifecycle (StreamId.Session sessionId) fact journal with
            | Ok _ -> return ()
            | Error failure ->
                return
                    raise (
                        InvalidOperationException(
                            sprintf "Finality append failed: %s" (JournalAppendFailure.describe failure)
                        )
                    )
        }

[<RequireQualifiedAccess>]
type RecordReadiness =
    | Ready of string
    | AwaitJournal
    | Unavailable of string

/// Canonical reviewer work-record materialization and journal-driven readiness.
module RecordWorkflow =

    let private hasRenderedChronicle (record: string) =
        let marker = "Chronicle\n"
        let start = record.IndexOf(marker, StringComparison.Ordinal)

        start >= 0
        && not (String.IsNullOrWhiteSpace(record.Substring(start + marker.Length)))

    let private materialize
        (journal: AgentJournal)
        (snapshot: ProjectionSet)
        (reviewerSessionId: SessionId)
        (requiresChronicle: bool)
        : Task<RecordReadiness> =
        task {
            let fullCanonicalCoverage = Some { IngestedThrough = XTraceCursor.originCursor }

            match!
                LifecycleWorkRecordProjection.lifecycleWorkRecordFromSnapshot
                    journal
                    snapshot
                    reviewerSessionId
                    false
                    fullCanonicalCoverage
            with
            | Some record when
                not (String.IsNullOrWhiteSpace record)
                && (not requiresChronicle || hasRenderedChronicle record)
                ->
                return RecordReadiness.Ready record
            | Some _ -> return RecordReadiness.Unavailable "canonical LWR has no rendered Chronicle"
            | None -> return RecordReadiness.Unavailable "canonical LWR is unavailable"
        }

    let private bloggerCoverageCanAdvance (snapshot: ProjectionSet) (bloggerSessionId: SessionId) =
        match Map.tryFind bloggerSessionId snapshot.AgentProjections.HandleByChildSession with
        | Some { Lifecycle = HandleLifecycle.Abandoned _ }
        | Some { Lifecycle = HandleLifecycle.Retired } -> false
        | _ -> true

    let private coverageCanAdvance (snapshot: ProjectionSet) (reviewerSessionId: SessionId) =
        SessionAssociationProjection.tryBloggerOf reviewerSessionId snapshot.AgentProjections.Associations
        |> Option.map (bloggerCoverageCanAdvance snapshot)
        |> Option.defaultValue true

    let private materializeWithCoverage
        (journal: AgentJournal)
        (snapshot: ProjectionSet)
        (reviewerSessionId: SessionId)
        (requiresChronicle: bool)
        : Task<RecordReadiness> =
        task {
            match! materialize journal snapshot reviewerSessionId requiresChronicle with
            | RecordReadiness.Ready record -> return RecordReadiness.Ready record
            | RecordReadiness.Unavailable _ when coverageCanAdvance snapshot reviewerSessionId ->
                return RecordReadiness.AwaitJournal
            | other -> return other
        }

    let private readinessForGuard
        (journal: AgentJournal)
        (snapshot: ProjectionSet)
        (reviewerSessionId: SessionId)
        (barrierId: ReviewBarrierId)
        (requiresTerminalFrontier: bool)
        (guard: ReviewGuardProjection)
        : Task<RecordReadiness> =
        task {
            match guard.CurrentBarrierId = Some barrierId, guard.TerminalFrontier with
            | false, _ -> return RecordReadiness.Unavailable "review barrier no longer matches the finality member"
            | true, Some frontier when frontier.BarrierId <> barrierId ->
                return RecordReadiness.Unavailable "terminal frontier no longer matches the finality barrier"
            | true, Some _ -> return! materializeWithCoverage journal snapshot reviewerSessionId true
            | true, None when requiresTerminalFrontier -> return RecordReadiness.AwaitJournal
            | true, None -> return! materialize journal snapshot reviewerSessionId false
        }

    let private readinessForSession
        (journal: AgentJournal)
        (snapshot: ProjectionSet)
        (reviewerSessionId: SessionId)
        (barrierId: ReviewBarrierId)
        (requiresTerminalFrontier: bool)
        (session: SessionAgentProjection)
        =
        task {
            match session.ReviewGuard with
            | None -> return RecordReadiness.Unavailable "review barrier is unavailable"
            | Some guard ->
                return! readinessForGuard journal snapshot reviewerSessionId barrierId requiresTerminalFrontier guard
        }

    let readiness
        (journal: AgentJournal)
        (snapshot: ProjectionSet)
        (reviewerSessionId: SessionId)
        (barrierId: ReviewBarrierId)
        (requiresTerminalFrontier: bool)
        : Task<RecordReadiness> =
        task {
            match AgentProjection.tryFind reviewerSessionId snapshot.AgentProjections with
            | None -> return RecordReadiness.Unavailable "reviewer projection is unavailable"
            | Some session ->
                return!
                    readinessForSession journal snapshot reviewerSessionId barrierId requiresTerminalFrontier session
        }

    let awaitCanonicalWorkRecord
        (journal: AgentJournal)
        (reviewerSessionId: SessionId)
        (barrierId: ReviewBarrierId)
        : Task<Result<string, string>> =
        let rec loop () =
            task {
                let snapshot, revision = AgentJournal.snapshotWithRevision journal

                match! readiness journal snapshot reviewerSessionId barrierId true with
                | RecordReadiness.Ready record -> return Ok record
                | RecordReadiness.Unavailable reason -> return Error reason
                | RecordReadiness.AwaitJournal ->
                    let! _ = AgentJournal.awaitChangeFrom revision journal
                    return! loop ()
            }

        loop ()

    let awaitCanonicalCohortRecords
        (journal: AgentJournal)
        (members: EnlistedMember list)
        : Task<Result<(int * string) list, string>> =
        let ordered = members |> List.sortBy (fun memberInfo -> memberInfo.ReviewerOrdinal)

        let rec loop () =
            task {
                let snapshot, revision = AgentJournal.snapshotWithRevision journal

                // DSL-MUTABLE: algorithm-scratch — enlisted member readiness accumulator
                let states = ResizeArray<EnlistedMember * RecordReadiness>()

                for memberInfo in ordered do
                    let! state = readiness journal snapshot memberInfo.ReviewerSessionId memberInfo.BarrierId false
                    states.Add(memberInfo, state)

                let states = states |> Seq.toList

                match
                    states
                    |> List.tryPick (fun (_, state) ->
                        match state with
                        | RecordReadiness.Unavailable reason -> Some reason
                        | _ -> None)
                with
                | Some reason -> return Error reason
                | None when states |> List.exists (fun (_, state) -> state = RecordReadiness.AwaitJournal) ->
                    let descriptor =
                        DiagnosticWait.create
                            "finality-blessing-records"
                            (CausalOwner.create "RecordWorkflow" [])
                            (states
                             |> List.choose (fun (memberInfo, state) ->
                                 if state = RecordReadiness.AwaitJournal then
                                     Some("reviewer", SessionId.value memberInfo.ReviewerSessionId)
                                 else
                                     None))
                            (ExternalProducer("journal-chronicle", []))
                            [ WaitEscape.ProcessLifetime; WaitEscape.OpenEndedExternal ]
                            "RecordWorkflow.awaitCanonicalCohortRecords"

                    let! _ =
                        CausalAwait.awaitTask
                            CausalWaitHub.observer
                            descriptor
                            (AgentJournal.awaitChangeFrom revision journal)

                    return! loop ()
                | None ->
                    return
                        states
                        |> List.choose (fun (memberInfo, state) ->
                            match state with
                            | RecordReadiness.Ready record -> Some(memberInfo.ReviewerOrdinal, record)
                            | _ -> None)
                        |> Ok
            }

        loop ()
