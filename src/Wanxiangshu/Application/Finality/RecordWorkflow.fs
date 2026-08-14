namespace Wanxiangshu.Finality

open System
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Domain
open Wanxiangshu.Execution.Delegation
open System.Threading.Tasks
open Wanxiangshu.Execution.Session
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Session

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
            let fullCanonicalCoverage = Some { IngestedThrough = XTrace.originCursor }

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

    let private coverageCanAdvance (snapshot: ProjectionSet) (reviewerSessionId: SessionId) =
        match SessionAssociationProjection.tryBloggerOf reviewerSessionId snapshot.AgentProjections.Associations with
        | None -> true
        | Some bloggerSessionId ->
            match Map.tryFind bloggerSessionId snapshot.AgentProjections.HandleByChildSession with
            | Some { Lifecycle = HandleLifecycle.Abandoned _ }
            | Some { Lifecycle = HandleLifecycle.Retired } -> false
            | _ -> true

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
                match session.ReviewGuard with
                | None -> return RecordReadiness.Unavailable "review barrier is unavailable"
                | Some guard when guard.CurrentBarrierId <> Some barrierId ->
                    return RecordReadiness.Unavailable "review barrier no longer matches the finality member"
                | Some guard ->
                    match guard.TerminalFrontier with
                    | Some frontier when frontier.BarrierId <> barrierId ->
                        return RecordReadiness.Unavailable "terminal frontier no longer matches the finality barrier"
                    | Some _ ->
                        match! materialize journal snapshot reviewerSessionId true with
                        | RecordReadiness.Ready record -> return RecordReadiness.Ready record
                        | RecordReadiness.Unavailable _ when coverageCanAdvance snapshot reviewerSessionId ->
                            return RecordReadiness.AwaitJournal
                        | other -> return other
                    | None when requiresTerminalFrontier -> return RecordReadiness.AwaitJournal
                    | None -> return! materialize journal snapshot reviewerSessionId false
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
