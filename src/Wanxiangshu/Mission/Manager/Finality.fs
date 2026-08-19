namespace Wanxiangshu.Mission.Manager

open Wanxiangshu.Change
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Obligation
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Persistence
open Wanxiangshu.Strength.Replica

open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Foundation.Identity

/// Manager-side ownership boundary while a Finality request is durably open.
///
/// ManagerWorkflow should not inspect ActiveFinality itself: whether an open
/// request temporarily owns the Life is one business question with one answer.
module ManagerFinality =

    [<RequireQualifiedAccess>]
    type LaborAdmission =
        | FinalityOwnsLife
        | LaborMayContinue

    [<RequireQualifiedAccess>]
    type EndingDisposition =
        | ContinuePlanning
        | AlreadyCompleted
        | ResumeRequest of FinalityRequestProjection
        | RecoverRequestWithoutReviewers of FinalityRequestProjection
        | WaitForCurrentRequest
        | CompleteBlessedLife of BlessingEvidence
        | BeginFinality

    /// Boundary result from Finality-owned ending execution. The Tool adapter
    /// only receives this — never the internal EndingDisposition to dispatch on.
    [<RequireQualifiedAccess>]
    type FinalityEndingOutcome =
        | Refused of path: string
        | Result of toolResult: obj

    /// Execution capabilities the Tool adapter provides to the Finality-owned
    /// ending handler. The handler dispatches internally; the adapter only
    /// renders the boundary outcome.
    type FinalityEndingExecution =
        { Refuse: string -> obj
          AlreadyCompleted: unit -> obj
          ResumeRequest: FinalityRequestProjection -> Task<obj>
          RecoverEmptyMembers: FinalityRequestProjection -> Task<obj>
          WaitForCurrentRequest: unit -> obj
          CompleteBlessedLife: BlessingEvidence -> Task<obj>
          BeginFinality: unit -> Task<obj> }

    /// Execute the ending action inside the Finality domain. The Tool adapter
    /// provides execution capabilities but never matches EndingDisposition cases.
    let handleEnding (disposition: EndingDisposition) (exec: FinalityEndingExecution) : Task<FinalityEndingOutcome> =
        task {
            match disposition with
            | EndingDisposition.ContinuePlanning -> return FinalityEndingOutcome.Refused "tool/suicide/continue-working"
            | EndingDisposition.AlreadyCompleted -> return FinalityEndingOutcome.Result(exec.AlreadyCompleted())
            | EndingDisposition.ResumeRequest request ->
                let! result = exec.ResumeRequest request
                return FinalityEndingOutcome.Result result
            | EndingDisposition.RecoverRequestWithoutReviewers request ->
                let! result = exec.RecoverEmptyMembers request
                return FinalityEndingOutcome.Result result
            | EndingDisposition.WaitForCurrentRequest ->
                return FinalityEndingOutcome.Refused "tool/suicide/wait-for-current-ending"
            | EndingDisposition.CompleteBlessedLife blessing ->
                let! result = exec.CompleteBlessedLife blessing
                return FinalityEndingOutcome.Result result
            | EndingDisposition.BeginFinality ->
                let! result = exec.BeginFinality()
                return FinalityEndingOutcome.Result result
        }

    /// Admit ordinary Manager labor only when no open Finality request owns the
    /// current Life. Resolved historical requests do not block labor.
    let admitLabor (life: LifeProjection) : LaborAdmission =
        match life.ActiveFinality with
        | Some request when ManagerLifecycleProjection.isOpen request -> LaborAdmission.FinalityOwnsLife
        | _ -> LaborAdmission.LaborMayContinue

    let private classifyOpenFinalityRequest (toolCallId: ToolCallId option) (request: FinalityRequestProjection) =
        match toolCallId with
        | Some callId when callId = request.ToolCallId -> EndingDisposition.ResumeRequest request
        | _ when Map.isEmpty request.Members -> EndingDisposition.RecoverRequestWithoutReviewers request
        | _ -> EndingDisposition.WaitForCurrentRequest

    let private classifyCompletedLife (life: LifeProjection) =
        match life.LastBlessing with
        | Some blessing -> EndingDisposition.CompleteBlessedLife blessing
        | None -> EndingDisposition.BeginFinality

    /// Interpret one suicide call against the durable Life. Pre-T1 BlindPlan
    /// (no accepted plan commitment) stays at the Planning Table.
    let classifyEnding
        (toolCallId: ToolCallId option)
        (life: LifeProjection)
        (hasPlanCommitment: bool)
        : EndingDisposition =
        match hasPlanCommitment, life.Completed, life.ActiveFinality with
        | false, _, _ -> EndingDisposition.ContinuePlanning
        | true, true, _ -> EndingDisposition.AlreadyCompleted
        | true, false, Some request when ManagerLifecycleProjection.isOpen request ->
            classifyOpenFinalityRequest toolCallId request
        | true, false, Some request when toolCallId = Some request.ToolCallId -> EndingDisposition.ResumeRequest request
        | true, false, _ -> classifyCompletedLife life
