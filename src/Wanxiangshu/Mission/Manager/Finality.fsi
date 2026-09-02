namespace Wanxiangshu.Mission.Manager

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Manager.Life

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
        | Result of toolResult: string

    /// Execution capabilities the Tool adapter provides to the Finality-owned
    /// ending handler. The handler dispatches internally; the adapter only
    /// renders the boundary outcome.
    type FinalityEndingExecution =
        { AlreadyCompleted: unit -> string
          ResumeRequest: FinalityRequestProjection -> Task<string>
          RecoverEmptyMembers: FinalityRequestProjection -> Task<string>
          CompleteBlessedLife: BlessingEvidence -> Task<string>
          BeginFinality: unit -> Task<string> }

    /// Execute the ending action inside the Finality domain. The Tool adapter
    /// provides execution capabilities but never matches EndingDisposition cases.
    val handleEnding: disposition: EndingDisposition -> exec: FinalityEndingExecution -> Task<FinalityEndingOutcome>

    /// Admit ordinary Manager labor only when no open Finality request owns the
    /// current Life. Resolved historical requests do not block labor.
    val admitLabor: life: LifeProjection -> LaborAdmission

    /// Interpret one suicide call against the durable Life. Pre-T1 BlindPlan
    /// (no accepted plan commitment) stays at the Planning Table.
    val classifyEnding:
        toolCallId: ToolCallId option -> life: LifeProjection -> hasPlanCommitment: bool -> EndingDisposition
