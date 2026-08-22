namespace Wanxiangshu.Mission.Review.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

/// Physical Reviewer terminal semantics shared by Finality and Change review.
/// InterruptAttempt retires one Host attempt; it is deliberately not an Agent
/// terminal. The durable exact ReviewAttemptClosed fact authorizes that later
/// Host Abort to close the review occasion cleanly. Every other abort/failure
/// remains an error.
module ReviewerTerminalAwait =

    let hasDurablyClosedJudgement
        (journal: AgentJournal option)
        (reviewerSessionId: SessionId)
        (barrierId: ReviewBarrierId)
        =
        match journal with
        | None -> false
        | Some durable ->
            AgentProjection.tryFind reviewerSessionId (AgentJournal.snapshot durable).AgentProjections
            |> Option.bind (fun session -> session.ReviewGuard)
            |> Option.exists (fun guard ->
                guard.CurrentBarrierId = Some barrierId
                && (guard
                    |> ReviewProjection.latestObservedAttempt
                    |> Option.filter (fun attempt -> attempt.ReviewBarrierId = barrierId)
                    |> Option.bind (fun attempt -> ReviewProjection.closedAttemptOf attempt guard)
                    |> Option.isSome))

    let private terminalResult
        (journal: AgentJournal option)
        (reviewerSessionId: SessionId)
        (barrierId: ReviewBarrierId)
        (completed: TaskCompletionSource<TerminalOutcome>)
        : Task<Result<unit, string>> =
        task {
            match! completed.Task with
            | TerminalOutcome.Completed _ -> return Ok()
            | TerminalOutcome.Failed stop -> return Error stop.Reason
            | TerminalOutcome.Aborted _ when hasDurablyClosedJudgement journal reviewerSessionId barrierId ->
                return Ok()
            | TerminalOutcome.Aborted stop -> return Error stop.Reason
        }

    let awaitFuture
        (journal: AgentJournal option)
        (sessions: ISessionHostPort)
        (occasion: ReviewerTerminalOccasion)
        (timeoutMs: int)
        : Task<Result<unit, string>> =
        task {
            let reviewerSessionId = occasion.ReviewerSessionId
            let barrierId = occasion.BarrierId

            let completed =
                TaskCompletionSource<TerminalOutcome>(TaskCreationOptions.RunContinuationsAsynchronously)

            // DSL-MUTABLE: cancellation — subscription acceptance flag.
            let accepting = ref false

            use subscription =
                sessions.SubscribeFutureTerminal(
                    reviewerSessionId,
                    fun _ outcome ->
                        if accepting.Value then
                            AsyncSupport.trySetResult completed outcome |> ignore
                )

            accepting.Value <- true

            let! completedBeforeDeadline = Wanxiangshu.Process.PtyTiming.raceExit (completed.Task :> Task) timeoutMs

            if not completedBeforeDeadline then
                return Error "await reviewer timed out"
            else
                return! terminalResult journal reviewerSessionId barrierId completed
        }
