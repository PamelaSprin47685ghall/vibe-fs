namespace Wanxiangshu.Mission.Review.Barrier

open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
type ReviewBarrierOutcome =
    | Confirmed of reviewerSessionId: SessionId * barrierId: ReviewBarrierId * gitTreeHash: GitTreeHash
    | RevisionRequired of reviewerSessionId: SessionId * barrierId: ReviewBarrierId * gitTreeHash: GitTreeHash

type ReviewBarrierRequest =
    { ManagerSessionId: SessionId
      ManagerJobId: ManagerJobId option
      WorktreeIdentity: WorktreeIdentity option
      ReviewerSessionId: SessionId
      BarrierId: ReviewBarrierId
      GitTreeHash: GitTreeHash }

[<RequireQualifiedAccess>]
type ReviewBarrierFailure =
    | JournalUnavailable
    | CannotStartReviewer of string
    | CannotAwaitReviewer of string
    | CannotAwaitJudgement of string
    | CannotNudgeReviewer of string
    | CannotRecordJudgement of string
    | InvalidJudgement of string

/// Finality dual-PERFECT temporal owner. First/challenge/second exist only as CE
/// locals; durable review facts are outputs and are never read back to select a step.
module ReviewBarrierWorkflow =

    [<RequireQualifiedAccess>]
    type private DeliveryFailure =
        | Invalid of string
        | RecordFailed of string

    let private submission (request: ReviewBarrierRequest) (judgement: ReviewJudgement) : VerdictSubmission =
        { BarrierId = request.BarrierId
          GitTreeHash = request.GitTreeHash
          ManagerSessionId = request.ManagerSessionId
          ReviewerSessionId = judgement.ReviewerSessionId
          ProviderRun = judgement.ProviderRun
          ToolCallId = judgement.ToolCallId
          Verdict = judgement.Verdict }

    let private validateReviewer (request: ReviewBarrierRequest) (judgement: ReviewJudgement) =
        if judgement.ReviewerSessionId = request.ReviewerSessionId then
            Ok()
        else
            Error "judge delivery came from a different Reviewer session"

    let private validateExpectedPhysical expectedPhysical (judgement: ReviewJudgement) =
        match expectedPhysical with
        | None -> Ok()
        | Some physical when judgement.PhysicalUserMessageId = physical -> Ok()
        | Some _ -> Error "judgement came from a different physical review prompt than the owning CE expected"

    let private validateFirst request expectedPhysical judgement =
        validateReviewer request judgement
        |> Result.bind (fun () -> validateExpectedPhysical expectedPhysical judgement)

    let private validateSecond
        (request: ReviewBarrierRequest)
        (first: ReviewJudgement)
        (expectedPhysical: PhysicalUserMessageId)
        (second: ReviewJudgement)
        =
        if first.ProviderRun = second.ProviderRun then
            Error "second judgement reused the first ProviderRunIdentity"
        elif first.ToolCallId = second.ToolCallId then
            Error "second judgement reused the first ToolCallId"
        else
            validateReviewer request second
            |> Result.bind (fun () -> validateExpectedPhysical (Some expectedPhysical) second)

    let private validateDelivery
        (validation: ReviewJudgement -> Result<unit, string>)
        (delivery: ReviewJudgementDelivery)
        : Result<unit, DeliveryFailure> =
        match validation delivery.Judgement with
        | Ok() -> Ok()
        | Error error ->
            delivery.Reject()
            Error(DeliveryFailure.Invalid error)

    let private persistDelivery
        (journal: AgentJournal)
        (request: ReviewBarrierRequest)
        (delivery: ReviewJudgementDelivery)
        : Task<Result<ReviewJudgement, DeliveryFailure>> =
        task {
            match! VerdictWorkflow.recordJudgement journal (submission request delivery.Judgement) with
            | Ok() -> return Ok delivery.Judgement
            | Error error ->
                delivery.Reject()
                return Error(DeliveryFailure.RecordFailed error)
        }

    let private recordDelivery
        (journal: AgentJournal)
        (request: ReviewBarrierRequest)
        (validation: ReviewJudgement -> Result<unit, string>)
        (delivery: ReviewJudgementDelivery)
        : Task<Result<ReviewJudgement, DeliveryFailure>> =
        taskResult {
            do! validateDelivery validation delivery
            return! persistDelivery journal request delivery
        }

    let private deliveryFailure =
        function
        | DeliveryFailure.Invalid error -> ReviewBarrierFailure.InvalidJudgement error
        | DeliveryFailure.RecordFailed error -> ReviewBarrierFailure.CannotRecordJudgement error

    let private revision (request: ReviewBarrierRequest) =
        ReviewBarrierOutcome.RevisionRequired(request.ReviewerSessionId, request.BarrierId, request.GitTreeHash)

    let private confirmed (request: ReviewBarrierRequest) =
        ReviewBarrierOutcome.Confirmed(request.ReviewerSessionId, request.BarrierId, request.GitTreeHash)

    let rec private awaitRequiredJudgement
        (host: ReviewHostPort)
        (judgement: Task<Result<ReviewJudgementDelivery, string>>)
        (terminal: Task<Result<unit, string>>)
        (expectedPhysical: PhysicalUserMessageId option)
        : Task<
              Result<
                  ReviewJudgementDelivery * PhysicalUserMessageId option * Task<Result<unit, string>>,
                  ReviewBarrierFailure
               >
           >
        =
        task {
            let taggedJudgement: Task<obj> =
                task {
                    let! result = judgement
                    return box (Choice1Of2 result)
                }

            let taggedTerminal: Task<obj> =
                task {
                    let! result = terminal
                    return box (Choice2Of2 result)
                }

            let! winner = emitJsExpr (taggedJudgement, taggedTerminal) "Promise.race([$0, $1])": Task<obj>

            match unbox<Choice<Result<ReviewJudgementDelivery, string>, Result<unit, string>>> winner with
            | Choice1Of2(Ok delivery) -> return Ok(delivery, expectedPhysical, terminal)
            | Choice1Of2(Error error) -> return Error(ReviewBarrierFailure.CannotAwaitJudgement error)
            | Choice2Of2(Error error) -> return Error(ReviewBarrierFailure.CannotAwaitReviewer error)
            | Choice2Of2(Ok()) -> return! continueAfterCleanTerminal host judgement
        }

    and private continueAfterCleanTerminal
        (host: ReviewHostPort)
        (judgement: Task<Result<ReviewJudgementDelivery, string>>)
        =
        task {
            let nextTerminal = host.AwaitReviewer()

            match! host.NudgeMissingJudgement() with
            | Error error -> return Error(ReviewBarrierFailure.CannotNudgeReviewer error)
            | Ok physical -> return! awaitRequiredJudgement host judgement nextTerminal (Some physical)
        }

    let private finishAfterTerminal
        (finalTerminal: Task<Result<unit, string>>)
        (outcome: ReviewBarrierOutcome)
        : Task<Result<ReviewBarrierOutcome, ReviewBarrierFailure>> =
        taskResult {
            do! finalTerminal |> TaskResult.mapError ReviewBarrierFailure.CannotAwaitReviewer
            return outcome
        }

    let private recordConfirmation
        (journal: AgentJournal)
        (request: ReviewBarrierRequest)
        (expectedSecondPhysical: PhysicalUserMessageId)
        (first: ReviewJudgement)
        (secondDelivery: ReviewJudgementDelivery)
        (second: ReviewJudgement)
        : Task<Result<unit, ReviewBarrierFailure>> =
        task {
            match!
                VerdictWorkflow.recordConfirmation
                    journal
                    request.ManagerJobId
                    request.WorktreeIdentity
                    request.ManagerSessionId
                    request.BarrierId
                    request.GitTreeHash
                    expectedSecondPhysical
                    first
                    second
            with
            | Ok() ->
                secondDelivery.Accept()
                return Ok()
            | Error error ->
                secondDelivery.Reject()
                return Error(ReviewBarrierFailure.InvalidJudgement error)
        }

    let private concludeSecondPerfect
        (journal: AgentJournal)
        (request: ReviewBarrierRequest)
        (finalTerminal: Task<Result<unit, string>>)
        (expectedSecondPhysical: PhysicalUserMessageId)
        (first: ReviewJudgement)
        (secondDelivery: ReviewJudgementDelivery)
        (second: ReviewJudgement)
        : Task<Result<ReviewBarrierOutcome, ReviewBarrierFailure>> =
        taskResult {
            do! recordConfirmation journal request expectedSecondPhysical first secondDelivery second
            return! finishAfterTerminal finalTerminal (confirmed request)
        }

    let private concludeSecond
        (journal: AgentJournal)
        (request: ReviewBarrierRequest)
        (finalTerminal: Task<Result<unit, string>>)
        (expectedSecondPhysical: PhysicalUserMessageId)
        (first: ReviewJudgement)
        (secondDelivery: ReviewJudgementDelivery)
        : Task<Result<ReviewBarrierOutcome, ReviewBarrierFailure>> =
        taskResult {
            let! second =
                recordDelivery journal request (validateSecond request first expectedSecondPhysical) secondDelivery
                |> TaskResult.mapError deliveryFailure

            return!
                match second.Verdict with
                | ReviewGuardVerdict.Revise ->
                    secondDelivery.Accept()
                    finishAfterTerminal finalTerminal (revision request)
                | ReviewGuardVerdict.Perfect ->
                    concludeSecondPerfect
                        journal
                        request
                        finalTerminal
                        expectedSecondPhysical
                        first
                        secondDelivery
                        second
        }

    let private continueAfterFirstPerfect
        (journal: AgentJournal)
        (host: ReviewHostPort)
        (request: ReviewBarrierRequest)
        (finalTerminal: Task<Result<unit, string>>)
        (firstDelivery: ReviewJudgementDelivery)
        (first: ReviewJudgement)
        : Task<Result<ReviewBarrierOutcome, ReviewBarrierFailure>> =
        let secondAwait = host.AwaitJudgement()
        firstDelivery.Challenge()

        taskResult {
            let! secondDelivery, expectedPhysical, activeTerminal =
                awaitRequiredJudgement host secondAwait finalTerminal (Some first.PhysicalUserMessageId)

            return! concludeSecond journal request activeTerminal (Option.get expectedPhysical) first secondDelivery
        }

    let private continueAfterFirst
        (journal: AgentJournal)
        (host: ReviewHostPort)
        (request: ReviewBarrierRequest)
        (finalTerminal: Task<Result<unit, string>>)
        (firstDelivery: ReviewJudgementDelivery)
        (first: ReviewJudgement)
        : Task<Result<ReviewBarrierOutcome, ReviewBarrierFailure>> =
        match first.Verdict with
        | ReviewGuardVerdict.Revise ->
            firstDelivery.Accept()
            finishAfterTerminal finalTerminal (revision request)
        | ReviewGuardVerdict.Perfect -> continueAfterFirstPerfect journal host request finalTerminal firstDelivery first

    let private run
        (journal: AgentJournal)
        (host: ReviewHostPort)
        (request: ReviewBarrierRequest)
        : Task<Result<ReviewBarrierOutcome, ReviewBarrierFailure>> =
        let finalTerminal = host.AwaitReviewer()

        let firstAwait = host.AwaitJudgement()

        taskResult {
            do!
                host.StartReview()
                |> TaskResult.mapError ReviewBarrierFailure.CannotStartReviewer

            let! firstDelivery, expectedPhysical, activeTerminal =
                awaitRequiredJudgement host firstAwait finalTerminal None

            let! first =
                recordDelivery journal request (validateFirst request expectedPhysical) firstDelivery
                |> TaskResult.mapError deliveryFailure

            return! continueAfterFirst journal host request activeTerminal firstDelivery first
        }

    let reverify
        (journal: AgentJournal option)
        (host: ReviewHostPort)
        (request: ReviewBarrierRequest)
        : Task<Result<ReviewBarrierOutcome, ReviewBarrierFailure>> =
        match journal with
        | None -> Task.FromResult(Error ReviewBarrierFailure.JournalUnavailable)
        | Some durable -> run durable host request
