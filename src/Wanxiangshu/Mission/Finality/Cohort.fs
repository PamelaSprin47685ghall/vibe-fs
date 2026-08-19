namespace Wanxiangshu.Mission.Finality

open Wanxiangshu.Change
open Wanxiangshu.Mission.Obligation
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength.Persistence

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
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
open Wanxiangshu.Composition.Bridges.FinalityReview
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Review

[<RequireQualifiedAccess>]
type CohortJudgement =
    | RevisionRequired of
        rejectingReviewer: SessionId *
        barrierId: ReviewBarrierId *
        siblings: (SessionId * ReviewBarrierId) list
    | AllConfirmed

/// Finality cohort enlistment and temporal short-circuit vocabulary.
module CohortWorkflow =

    type private CancelToken() =
        // DSL-MUTABLE: cancellation — cooperative cancellation token state
        let mutable cancelled = false

        let tcs =
            TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

        member _.Task = tcs.Task
        member _.IsCancelled = cancelled

        member _.Cancel() =
            cancelled <- true
            AsyncSupport.trySetResult tcs () |> ignore

    let private raceWithCancel (cancel: CancelToken) (work: Task<'a>) : Task<'a option> =
        task {
            let taggedWork: Task<obj> =
                emitJsExpr work "$0.then(function (r) { return { kind: 0, r: r }; })"

            let taggedCancel: Task<obj> =
                emitJsExpr cancel.Task "$0.then(function () { return { kind: 1 }; })"

            let! winner = emitJsExpr (taggedWork, taggedCancel) "Promise.race([$0, $1])": Task<obj>
            let kind: int = emitJsExpr winner "$0.kind"

            if kind = 0 then
                return Some(emitJsExpr winner "$0.r": 'a)
            else
                return None
        }

    let private runMemberChannel
        (journal: AgentJournal)
        (channel: ReviewJudgementChannel)
        (host: ReviewHostPort)
        (request: ReviewBarrierRequest)
        : Task<Result<ReviewBarrierOutcome, ReviewBarrierFailure>> =
        task {
            try
                return! ReviewBarrierWorkflow.reverify (Some journal) host request
            finally
                channel.Dispose()
        }

    let private driveMember
        (reviewerPort: FinalityReviewerPort)
        (journal: AgentJournal)
        (cancel: CancelToken)
        (managerSessionId: SessionId)
        (memberInfo: EnlistedMember)
        (tree: GitTreeHash)
        : Task<Result<ReviewBarrierOutcome, ReviewBarrierFailure>> =
        let awaitOrCancel () =
            task {
                let! outcome = raceWithCancel cancel (reviewerPort.AwaitTerminal memberInfo.ReviewerSessionId)
                return outcome |> Option.defaultValue (Error "review attempt cancelled")
            }

        match reviewerPort.OpenJudgementChannel memberInfo.ReviewerSessionId with
        | Error error -> Task.FromResult(Error(ReviewBarrierFailure.CannotAwaitJudgement error))
        | Ok channel ->
            let host =
                { StartReview = fun () -> reviewerPort.StartReview memberInfo
                  AwaitJudgement = channel.AwaitJudgement
                  AwaitReviewer = awaitOrCancel
                  NudgeMissingJudgement = fun () -> reviewerPort.NudgeMissingJudgement memberInfo.ReviewerSessionId }

            let request =
                { ManagerSessionId = managerSessionId
                  ManagerJobId = None
                  WorktreeIdentity = None
                  ReviewerSessionId = memberInfo.ReviewerSessionId
                  BarrierId = memberInfo.BarrierId
                  GitTreeHash = tree }

            runMemberChannel journal channel host request

    let private recordWinner (cancel: CancelToken) (winnerOpt: 'a option ref) (result: 'a) =
        match winnerOpt.Value with
        | None ->
            winnerOpt.Value <- Some result
            cancel.Cancel()
        | Some _ -> ()

    let private finishRemaining tcs (winnerOpt: 'a option ref) (all: 'a list) =
        match winnerOpt.Value with
        | Some winner -> AsyncSupport.trySetResult tcs (Choice1Of2(winner, all)) |> ignore
        | None -> AsyncSupport.trySetResult tcs (Choice2Of2 all) |> ignore

    let private concurrentAllOrShortCircuit
        (cancel: CancelToken)
        (isShortCircuit: 'a -> bool)
        (tasks: Task<'a> list)
        : Task<Choice<'a * 'a list, 'a list>> =
        task {
            let tcs =
                TaskCompletionSource<Choice<'a * 'a list, 'a list>>(TaskCreationOptions.RunContinuationsAsynchronously)

            // DSL-MUTABLE: algorithm-scratch — remaining task counter
            let remaining = ref (List.length tasks)
            let results = ResizeArray<'a>()
            // DSL-MUTABLE: algorithm-scratch — short-circuit winner cell
            let shortCircuitWinner = ref None

            let decide (result: 'a) =
                results.Add result

                if isShortCircuit result then
                    recordWinner cancel shortCircuitWinner result

                remaining.Value <- remaining.Value - 1

                if remaining.Value = 0 then
                    finishRemaining tcs shortCircuitWinner (List.ofSeq results)

            if List.isEmpty tasks then
                AsyncSupport.trySetResult tcs (Choice2Of2 []) |> ignore
            else
                tasks
                |> List.iter (fun work ->
                    async {
                        let! result = Async.AwaitTask work
                        decide result
                    }
                    |> Async.StartImmediate)

            return! tcs.Task
        }

    let private openBarrierAndRememberMember
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (requestTree: GitTreeHash)
        (prepared: PreparedReviewer)
        (barrierId: ReviewBarrierId)
        (slot: FinalityReviewCohort.CohortSlot)
        (members: EnlistedMember list)
        =
        task {
            match!
                ReviewBarrier.openBarrier
                    (Some journal)
                    managerSessionId
                    prepared.ReviewerSessionId
                    barrierId
                    requestTree
            with
            | Error error -> return Error error
            | Ok() ->
                return
                    Ok(
                        members
                        @ [ { ReviewerSessionId = prepared.ReviewerSessionId
                              BarrierId = barrierId
                              ReviewerOrdinal = slot.ReviewerOrdinal
                              AgentId = slot.AgentId
                              IsNew = prepared.IsNew } ]
                    )
        }

    let private recordEnlistedFact
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (life: LifeProjection)
        (request: FinalityRequestProjection)
        (slot: FinalityReviewCohort.CohortSlot)
        (prepared: PreparedReviewer)
        (barrierId: ReviewBarrierId)
        =
        FinalityJournal.appendLifecycle
            journal
            (ManagerLifecycleFact.FinalityReviewerEnlisted
                {| SessionId = managerSessionId
                   LifeId = life.LifeId
                   RequestId = request.RequestId
                   ReviewerSessionId = prepared.ReviewerSessionId
                   ReviewerOrdinal = slot.ReviewerOrdinal
                   BarrierId = barrierId
                   GitTreeHash = request.GitTreeHash
                   IsNewReviewer = prepared.IsNew |})

    let private recordEnlistmentIfNew
        existingMember
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        life
        request
        slot
        prepared
        (barrierId: ReviewBarrierId)
        : Task =
        match existingMember with
        | Some _ -> AsyncSupport.completedTask ()
        | None -> recordEnlistedFact journal managerSessionId life request slot prepared barrierId

    let private prepareSlotAndOpen
        (reviewerPort: FinalityReviewerPort)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (life: LifeProjection)
        (request: FinalityRequestProjection)
        (members: EnlistedMember list)
        (slot: FinalityReviewCohort.CohortSlot)
        =
        task {
            let existingMember =
                slot.ReviewerSessionId
                |> Option.bind (fun sid -> Map.tryFind sid request.Members)

            let barrierId =
                existingMember
                |> Option.map (fun memberRef -> memberRef.BarrierId)
                |> Option.defaultWith (fun () -> ReviewBarrierId.create (Guid.NewGuid().ToString("N")))

            let physicalRequest: FinalityReviewerRequest =
                { ManagerSessionId = managerSessionId
                  LifeId = life.LifeId
                  RequestId = request.RequestId
                  RequestTree = request.GitTreeHash
                  AgentId = slot.AgentId
                  ReviewerSessionId = slot.ReviewerSessionId
                  ReviewerOrdinal = slot.ReviewerOrdinal
                  IsNew = slot.IsNew }

            match! reviewerPort.PrepareSession physicalRequest with
            | Error error -> return Error error
            | Ok prepared ->
                do! recordEnlistmentIfNew existingMember journal managerSessionId life request slot prepared barrierId

                return!
                    openBarrierAndRememberMember
                        journal
                        managerSessionId
                        request.GitTreeHash
                        prepared
                        barrierId
                        slot
                        members
        }

    let private foldEnlistSlot
        reviewerPort
        journal
        managerSessionId
        life
        request
        (acc: Task<Result<EnlistedMember list, string>>)
        (slot: FinalityReviewCohort.CohortSlot)
        =
        task {
            match! acc with
            | Error error -> return Error error
            | Ok members -> return! prepareSlotAndOpen reviewerPort journal managerSessionId life request members slot
        }

    let enlistRequiredReviewers
        (reviewerPort: FinalityReviewerPort)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (life: LifeProjection)
        (request: FinalityRequestProjection)
        : Task<Result<EnlistedMember list, string>> =
        let slots =
            FinalityReviewCohort.rosterOf (AgentJournal.snapshot journal).AgentProjections life request

        slots
        |> List.fold (foldEnlistSlot reviewerPort journal managerSessionId life request) (Task.FromResult(Ok []))

    let reviewUntilFirstRevisionOrAllConfirmed
        (reviewerPort: FinalityReviewerPort)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (members: EnlistedMember list)
        (requestTree: GitTreeHash)
        : Task<Result<CohortJudgement, ReviewBarrierFailure>> =
        task {
            let cancel = CancelToken()

            let memberTasks =
                members
                |> List.map (fun memberInfo ->
                    driveMember reviewerPort journal cancel managerSessionId memberInfo requestTree)

            let! outcome =
                concurrentAllOrShortCircuit
                    cancel
                    (function
                    | Ok(ReviewBarrierOutcome.RevisionRequired _) -> true
                    | _ -> false)
                    memberTasks

            cancel.Cancel()

            match outcome with
            | Choice1Of2(Ok(ReviewBarrierOutcome.RevisionRequired(reviewerId, barrierId, _)), allResults) ->
                let siblings =
                    allResults
                    |> List.choose (function
                        | Ok(ReviewBarrierOutcome.RevisionRequired(sid, bid, _)) when sid <> reviewerId ->
                            Some(sid, bid)
                        | _ -> None)
                    |> List.distinctBy fst

                return Ok(CohortJudgement.RevisionRequired(reviewerId, barrierId, siblings))
            | Choice2Of2 results when
                List.forall
                    (function
                    | Ok(ReviewBarrierOutcome.Confirmed _) -> true
                    | _ -> false)
                    results
                ->
                return Ok CohortJudgement.AllConfirmed
            | Choice1Of2(Error failure, _)
            | Choice2Of2(Error failure :: _) -> return Error failure
            | Choice1Of2(Ok _, _) ->
                return invalidOp "Finality cohort short-circuited on a non-revision judgement"
            | Choice2Of2 results ->
                return
                    results
                    |> List.tryPick (function
                        | Error failure -> Some(Error failure)
                        | Ok _ -> None)
                    |> Option.defaultWith (fun () -> invalidOp "Finality cohort produced neither judgement nor failure")
        }
