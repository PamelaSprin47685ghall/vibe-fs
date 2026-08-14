namespace Wanxiangshu.OpenCode

open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Kernel.Identity

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

    /// Admit ordinary Manager labor only when no open Finality request owns the
    /// current Life. Resolved historical requests do not block labor.
    let admitLabor (life: LifeProjection) : LaborAdmission =
        match life.ActiveFinality with
        | Some request when ManagerLifecycleProjection.isOpen request -> LaborAdmission.FinalityOwnsLife
        | _ -> LaborAdmission.LaborMayContinue

    /// Interpret one suicide call against the durable Life. Pre-T1 BlindPlan
    /// (no accepted plan commitment) stays at the Planning Table.
    let classifyEnding
        (toolCallId: ToolCallId option)
        (life: LifeProjection)
        (hasPlanCommitment: bool)
        : EndingDisposition =
        if not hasPlanCommitment then
            EndingDisposition.ContinuePlanning
        elif life.Completed then
            EndingDisposition.AlreadyCompleted
        else
            match life.ActiveFinality with
            | Some request when ManagerLifecycleProjection.isOpen request ->
                match toolCallId with
                | Some callId when callId = request.ToolCallId -> EndingDisposition.ResumeRequest request
                | _ when Map.isEmpty request.Members -> EndingDisposition.RecoverRequestWithoutReviewers request
                | _ -> EndingDisposition.WaitForCurrentRequest
            | Some request when toolCallId = Some request.ToolCallId -> EndingDisposition.ResumeRequest request
            | _ ->
                match life.LastBlessing with
                | Some blessing -> EndingDisposition.CompleteBlessedLife blessing
                | None -> EndingDisposition.BeginFinality
