namespace Wanxiangshu.Finality

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Review

[<RequireQualifiedAccess>]
type CohortJudgement =
    | RevisionRequired of
        rejectingReviewer: SessionId *
        barrierId: ReviewBarrierId *
        siblings: (SessionId * ReviewBarrierId) list
    | AllConfirmed
    | Undecided

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

    let private hasDurableRevisionRequired
        (journal: AgentJournal)
        (reviewerSessionId: SessionId)
        (barrierId: ReviewBarrierId)
        =
        AgentProjection.tryFind reviewerSessionId (AgentJournal.snapshot journal).AgentProjections
        |> Option.bind (fun session -> session.ReviewGuard)
        |> Option.exists (fun guard ->
            match guard.CurrentBarrierId, guard.Witness with
            | Some current, ReviewWitness.RevisionWitness _ when current = barrierId -> true
            | _ -> false)

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
                let promoteCancelled () =
                    if hasDurableRevisionRequired journal memberInfo.ReviewerSessionId memberInfo.BarrierId then
                        Ok()
                    else
                        Error "review attempt cancelled"

                match! raceWithCancel cancel (reviewerPort.AwaitTerminal memberInfo.ReviewerSessionId) with
                | None -> return promoteCancelled ()
                | Some(Error error) when cancel.IsCancelled ->
                    if hasDurableRevisionRequired journal memberInfo.ReviewerSessionId memberInfo.BarrierId then
                        return Ok()
                    else
                        return Error error
                | Some(Error error) -> return Error error
                | Some(Ok()) when cancel.IsCancelled -> return promoteCancelled ()
                | Some(Ok()) -> return Ok()
            }

        ReviewBarrierWorkflow.reverify
            (Some journal)
            { ForkReviewer = fun () -> Task.FromResult(Ok memberInfo.ReviewerSessionId)
              AwaitReviewer = awaitOrCancel }
            managerSessionId
            memberInfo.BarrierId
            tree

    let private concurrentAllOrShortCircuit
        (cancel: CancelToken)
        (isShortCircuit: 'a -> bool)
        (tasks: Task<'a> list)
        : Task<Choice<'a * 'a list, 'a list>> =
        task {
            let tcs =
                TaskCompletionSource<Choice<'a * 'a list, 'a list>>(TaskCreationOptions.RunContinuationsAsynchronously)

            let remaining = ref (List.length tasks)
            let results = ResizeArray<'a>()
            let shortCircuitWinner = ref None

            let decide (result: 'a) =
                results.Add result

                if isShortCircuit result then
                    match shortCircuitWinner.Value with
                    | None ->
                        shortCircuitWinner.Value <- Some result
                        cancel.Cancel()
                    | Some _ -> ()

                remaining.Value <- remaining.Value - 1

                if remaining.Value = 0 then
                    let all = List.ofSeq results

                    match shortCircuitWinner.Value with
                    | Some winner -> AsyncSupport.trySetResult tcs (Choice1Of2(winner, all)) |> ignore
                    | None -> AsyncSupport.trySetResult tcs (Choice2Of2 all) |> ignore

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
        |> List.fold
            (fun (acc: Task<Result<EnlistedMember list, string>>) slot ->
                task {
                    match! acc with
                    | Error error -> return Error error
                    | Ok members ->
                        let existingMember =
                            slot.ReviewerSessionId
                            |> Option.bind (fun sid -> Map.tryFind sid request.Members)

                        let barrierId =
                            existingMember
                            |> Option.map (fun memberRef -> memberRef.BarrierId)
                            |> Option.defaultWith (fun () -> ReviewBarrierId.create (Guid.NewGuid().ToString("N")))

                        let physicalRequest =
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
                            if existingMember.IsNone then
                                do!
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

                            match!
                                ReviewBarrier.openBarrier
                                    (Some journal)
                                    managerSessionId
                                    prepared.ReviewerSessionId
                                    barrierId
                                    request.GitTreeHash
                            with
                            | Error error -> return Error error
                            | Ok() ->
                                let memberInfo =
                                    { ReviewerSessionId = prepared.ReviewerSessionId
                                      BarrierId = barrierId
                                      ReviewerOrdinal = slot.ReviewerOrdinal
                                      AgentId = slot.AgentId
                                      IsNew = prepared.IsNew }

                                match! reviewerPort.StartReview memberInfo with
                                | Error error -> return Error error
                                | Ok() -> return Ok(members @ [ memberInfo ])
                })
            (Task.FromResult(Ok []))

    let reviewUntilFirstRevisionOrAllConfirmed
        (reviewerPort: FinalityReviewerPort)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (members: EnlistedMember list)
        (requestTree: GitTreeHash)
        : Task<CohortJudgement> =
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

                return CohortJudgement.RevisionRequired(reviewerId, barrierId, siblings)
            | Choice2Of2 results when
                List.forall
                    (function
                    | Ok(ReviewBarrierOutcome.Confirmed _) -> true
                    | _ -> false)
                    results
                ->
                return CohortJudgement.AllConfirmed
            | _ -> return CohortJudgement.Undecided
        }
